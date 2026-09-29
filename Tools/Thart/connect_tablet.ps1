<#
.SYNOPSIS
    连接 Thart 平板触控录制器：配置 adb 反向端口转发，可选安装并启动 APK。

.DESCRIPTION
    平板端录制器默认连 127.0.0.1:28765，靠 adb reverse 把 USB 上的这个端口
    转发到电脑端的监听端口（电脑端 TcpListener 绑 IPAddress.Any，回环可通）。

    adb 反向转发在「重新插拔 USB、adb 重启、平板重启」后都会丢失，而它丢了
    的表现就是「平板一直连不上、电脑端看不到设备」。所以连不上时先跑这个脚本。

.EXAMPLE
    .\connect_tablet.ps1
    只配转发，装/启动都不做。

.EXAMPLE
    .\connect_tablet.ps1 -Install -Launch
    配转发 + 覆盖安装录制器 + 启动录制器。
#>
param(
    [int]$Port = 28765,
    [string]$AdbPath,
    [switch]$Install,
    [switch]$Launch,
    [string]$ApkPath = "$PSScriptRoot\..\..\Builds\ThartTouchRecorder\ThartTouchRecorder.apk",
    [string]$PackageName = "com.geometryrhythm.tharttouch"
)

$ErrorActionPreference = "Stop"

function Find-Adb {
    param([string]$Explicit)

    if ($Explicit) {
        if (Test-Path $Explicit) { return (Resolve-Path $Explicit).Path }
        throw "指定的 adb 不存在: $Explicit"
    }

    $cmd = Get-Command adb -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $candidates = @(
        "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe",
        "${env:ProgramFiles}\Unity\Hub\Editor",
        "${env:ProgramFiles(x86)}\Unity\Hub\Editor"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c -PathType Leaf) { return $c }
        if (Test-Path $c -PathType Container) {
            $hit = Get-ChildItem -Path $c -Filter "adb.exe" -Recurse -ErrorAction SilentlyContinue |
                   Select-Object -First 1
            if ($hit) { return $hit.FullName }
        }
    }

    throw "找不到 adb.exe。Unity 自带路径示例：C:\Program Files\Unity\Hub\Editor\<版本>\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe，可用 -AdbPath 指定。"
}

$adb = Find-Adb -Explicit $AdbPath
Write-Host "adb: $adb"

# adb 会把「listener not found」之类的提示写到 stderr，而 PS5 在
# ErrorActionPreference=Stop 下把原生命令的 stderr 当成终止性错误。
# 统一包一层：屏蔽 stderr 的异常语义，只按退出码判断成功与否。
function Invoke-Adb {
    param([string[]]$Arguments, [switch]$ShowStderr)

    $prev = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        if ($ShowStderr) { $output = & $script:adb @Arguments 2>&1 }
        else { $output = & $script:adb @Arguments 2>$null }
        $script:AdbExitCode = $LASTEXITCODE
        return $output
    }
    finally {
        $ErrorActionPreference = $prev
    }
}

$devices = @(Invoke-Adb -Arguments @("devices") | Select-String -Pattern "^\S+\s+device$")
if ($devices.Count -eq 0) {
    Write-Host "没有已授权的设备。" -ForegroundColor Yellow
    Write-Host "请插好 USB、在平板上允许 USB 调试（小米还需允许「USB 调试（安全设置）」），再重跑本脚本。"
    Invoke-Adb -Arguments @("devices", "-l") | ForEach-Object { Write-Host "  $_" }
    exit 1
}
Write-Host "已连接设备: $($devices.Count) 台"

# 先删再加，避免反复执行时转发条目累积。没有旧条目时 adb 会返回非零，属正常情况。
Invoke-Adb -Arguments @("reverse", "--remove", "tcp:$Port") | Out-Null
Invoke-Adb -Arguments @("reverse", "tcp:$Port", "tcp:$Port") | Out-Null
if ($script:AdbExitCode -ne 0) { throw "adb reverse 失败" }

$list = @(Invoke-Adb -Arguments @("reverse", "--list"))
Write-Host "反向转发:" -ForegroundColor Green
$list | ForEach-Object { Write-Host "  $_" }
if (-not ($list | Select-String "tcp:$Port")) {
    throw "转发未生效，adb reverse --list 里没有 tcp:$Port"
}

if ($Install) {
    if (-not (Test-Path $ApkPath)) { throw "APK 不存在: $ApkPath（先在 Unity 里跑「构建 Android 触控录制器」）" }
    $apk = (Resolve-Path $ApkPath).Path
    Write-Host "安装 $apk"
    Invoke-Adb -Arguments @("install", "-r", $apk) -ShowStderr | ForEach-Object { Write-Host "  $_" }
    if ($script:AdbExitCode -ne 0) {
        throw "安装失败。小米设备会弹一次确认框，误点拒绝会返回 INSTALL_FAILED_USER_RESTRICTED，允许后重跑即可。"
    }
}

if ($Launch) {
    Invoke-Adb -Arguments @("shell", "am", "start", "-n", "$PackageName/com.unity3d.player.UnityPlayerActivity") | Out-Null
}

Write-Host "完成：平板端 $PackageName 现在可以通过 127.0.0.1:$Port 连到电脑端。" -ForegroundColor Green