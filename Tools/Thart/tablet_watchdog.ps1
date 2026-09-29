<#
.SYNOPSIS
    Thart 平板连接看门狗：adb 反向转发丢了就自动补回来。

.DESCRIPTION
    USB 重新插拔、adb server 重启、平板重新枚举，都会清掉 adb reverse 条目，
    表现就是「平板一直连不上、电脑端看不到设备」。这个脚本常驻后台，
    每隔几秒检查一次，发现 tcp:28765 的转发不在了就立刻重新建立。

    转发正常时它什么都不做，不会干扰已经建立的连接。

.EXAMPLE
    .\tablet_watchdog.ps1
    前台运行，Ctrl+C 退出。

.EXAMPLE
    .\tablet_watchdog.ps1 -IntervalSeconds 5 -LogPath .\watchdog.log
#>
param(
    [int]$Port = 28765,
    [string]$AdbPath,
    [int]$IntervalSeconds = 3,
    [string]$LogPath = "$PSScriptRoot\tablet_watchdog.log"
)

$ErrorActionPreference = "Continue"

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

    throw "找不到 adb.exe，可用 -AdbPath 指定。"
}

$adb = Find-Adb -Explicit $AdbPath

function Write-Log {
    param([string]$Message)
    $line = "{0}  {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Message
    Write-Host $line
    try { Add-Content -Path $LogPath -Value $line -Encoding UTF8 } catch { }
}

# adb 会把提示写到 stderr，而 PS5 在 Stop 语义下会把它当成终止性错误。
# 统一屏蔽 stderr，只看退出码。
function Invoke-Adb {
    param([string[]]$Arguments)

    $prev = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & $script:adb @Arguments 2>$null
        $script:AdbExitCode = $LASTEXITCODE
        return $output
    }
    finally {
        $ErrorActionPreference = $prev
    }
}

Write-Log "看门狗启动: adb=$adb port=$Port 间隔=${IntervalSeconds}s"

$lastState = ""
while ($true) {
    $devices = @(Invoke-Adb -Arguments @("devices") | Select-String -Pattern "^\S+\s+device$")

    if ($devices.Count -eq 0) {
        if ($lastState -ne "no-device") { Write-Log "没有已授权设备，等待 USB 连接/授权" }
        $lastState = "no-device"
        Start-Sleep -Seconds $IntervalSeconds
        continue
    }

    $list = @(Invoke-Adb -Arguments @("reverse", "--list"))
    $hasForward = [bool]($list | Select-String "tcp:$Port")

    if ($hasForward) {
        if ($lastState -ne "ok") { Write-Log "转发正常: tcp:$Port" }
        $lastState = "ok"
    }
    else {
        Write-Log "转发丢失，重新建立 tcp:$Port"
        Invoke-Adb -Arguments @("reverse", "--remove", "tcp:$Port") | Out-Null
        Invoke-Adb -Arguments @("reverse", "tcp:$Port", "tcp:$Port") | Out-Null

        if ($script:AdbExitCode -eq 0) {
            Write-Log "已重建转发: tcp:$Port"
            $lastState = "ok"
        }
        else {
            Write-Log "重建失败（退出码 $script:AdbExitCode），下一轮重试"
            $lastState = "failed"
        }
    }

    Start-Sleep -Seconds $IntervalSeconds
}