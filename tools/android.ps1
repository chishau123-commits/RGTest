[CmdletBinding()]
param(
    [ValidateSet('Devices', 'Install', 'Launch', 'CaptureLogs', 'PushChart', 'PullDiagnostics', 'Screenshot', 'DeviceInfo')]
    [string] $Task = 'Devices',
    [string] $AdbPath = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe',
    [string] $Serial,
    [string] $ApkPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Builds\Android\RingGame-debug.apk'),
    [string] $ChartPath,
    [string] $EvidencePath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Builds\AndroidEvidence'),
    [ValidatePattern('^[a-zA-Z][a-zA-Z0-9_]*(\.[a-zA-Z][a-zA-Z0-9_]*)+$')]
    [string] $ApplicationId = 'com.chishau.ringgame.prototype'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not (Test-Path -LiteralPath $AdbPath -PathType Leaf)) {
    throw "ADB not found: $AdbPath. Install Unity Android Build Support or provide -AdbPath."
}

function Invoke-Adb {
    param([string[]] $Arguments)
    $output = & $AdbPath @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "ADB failed ($LASTEXITCODE): $($output -join [Environment]::NewLine)"
    }
    return $output
}

$deviceListing = Invoke-Adb -Arguments @('devices', '-l')
if ($Task -eq 'Devices') {
    $deviceListing | ForEach-Object { Write-Output $_ }
    return
}
$devices = @($deviceListing | ForEach-Object {
    if ($_ -match '^(\S+)\s+device(?:\s|$)') { $Matches[1] }
})
if (-not $Serial) {
    if ($devices.Count -ne 1) {
        throw "Found $($devices.Count) authorized devices. Connect/authorize a device; for multiple devices specify -Serial. ADB output: $($deviceListing -join ' | ')"
    }
    $Serial = $devices[0]
}
if ($Serial -notin $devices) { throw "Device $Serial is not online and authorized. Run -Task Devices." }
$deviceArguments = @('-s', $Serial)
$deviceFiles = "/sdcard/Android/data/$ApplicationId/files"

switch ($Task) {
    'Install' {
        $apk = (Resolve-Path -LiteralPath $ApkPath).Path
        if ([IO.Path]::GetExtension($apk) -ne '.apk') { throw 'ApkPath must name an APK.' }
        # Retains app data; no uninstall, clear, downgrade or permission change.
        Invoke-Adb -Arguments ($deviceArguments + @('install', '-r', '-t', $apk))
    }
    'Launch' {
        $launchOutput = Invoke-Adb -Arguments ($deviceArguments + @('shell', 'am', 'start', '-W', '-n',
            "$ApplicationId/com.unity3d.player.UnityPlayerActivity"))
        $launchOutput
        if (($launchOutput -join [Environment]::NewLine) -match '(?im)^Error:|^Exception' -or
            ($launchOutput -join [Environment]::NewLine) -notmatch '(?im)^Status:\s*ok\s*$') {
            throw 'Android Activity launch did not report Status: ok. Inspect the output and device log.'
        }
    }
    'CaptureLogs' {
        New-Item -ItemType Directory -Path $EvidencePath -Force | Out-Null
        $output = Invoke-Adb -Arguments ($deviceArguments + @('logcat', '-d', '-v', 'threadtime',
            'Unity:D', 'AndroidRuntime:E', '*:S'))
        $output | Set-Content -LiteralPath (Join-Path $EvidencePath 'logcat.txt') -Encoding UTF8
        Write-Host "Saved bounded logcat snapshot to $EvidencePath\logcat.txt"
    }
    'PushChart' {
        if (-not $ChartPath) { throw 'Provide -ChartPath with a prototype JSON chart.' }
        $chart = (Resolve-Path -LiteralPath $ChartPath).Path
        Get-Content -LiteralPath $chart -Raw | ConvertFrom-Json | Out-Null
        Invoke-Adb -Arguments ($deviceArguments + @('shell', 'mkdir', '-p', $deviceFiles))
        Invoke-Adb -Arguments ($deviceArguments + @('push', $chart, "$deviceFiles/prototype-chart.json"))
        Write-Host 'Chart copied. Restart the chart/app to load it; invalid charts are rejected by the runtime.'
    }
    'PullDiagnostics' {
        New-Item -ItemType Directory -Path $EvidencePath -Force | Out-Null
        Invoke-Adb -Arguments ($deviceArguments + @('pull', "$deviceFiles/diagnostics", $EvidencePath))
    }
    'Screenshot' {
        New-Item -ItemType Directory -Path $EvidencePath -Force | Out-Null
        # Pull a file to preserve PNG bytes on Windows PowerShell 5.1 as well as PowerShell 7.
        $remotePng = '/data/local/tmp/ringgame-review.png'
        Invoke-Adb -Arguments ($deviceArguments + @('shell', 'screencap', '-p', $remotePng))
        Invoke-Adb -Arguments ($deviceArguments + @('pull', $remotePng, (Join-Path $EvidencePath 'screen.png')))
    }
    'DeviceInfo' {
        New-Item -ItemType Directory -Path $EvidencePath -Force | Out-Null
        $record = [ordered] @{
            capturedUtc = [DateTime]::UtcNow.ToString('o')
            serial = $Serial
            model = (Invoke-Adb -Arguments ($deviceArguments + @('shell', 'getprop', 'ro.product.model'))) -join ''
            android = (Invoke-Adb -Arguments ($deviceArguments + @('shell', 'getprop', 'ro.build.version.release'))) -join ''
            sdk = (Invoke-Adb -Arguments ($deviceArguments + @('shell', 'getprop', 'ro.build.version.sdk'))) -join ''
            abi = (Invoke-Adb -Arguments ($deviceArguments + @('shell', 'getprop', 'ro.product.cpu.abilist'))) -join ''
            display = (Invoke-Adb -Arguments ($deviceArguments + @('shell', 'wm', 'size'))) -join ' '
        }
        $record | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $EvidencePath 'device-info.json') -Encoding UTF8
        Write-Host "Saved device information to $EvidencePath\device-info.json"
    }
}
