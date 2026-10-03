[CmdletBinding()]
param(
    [ValidateSet('Prepare', 'Test', 'Android', 'WindowsSmoke')]
    [string] $Task = 'Test',
    [string] $UnityPath = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe',
    [string] $ProjectPath = (Split-Path -Parent $PSScriptRoot),
    [string] $OutputPath,
    [ValidateRange(26, 99)] [int] $AndroidApi = 35,
    [ValidateRange(0, 7200)] [int] $TimeoutSeconds = 0
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity Editor was not found at $UnityPath. Use -UnityPath to select Unity 2022.3.62f3c1."
}
$projectRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'Packages\manifest.json'))) {
    throw "Not a Unity project: $projectRoot"
}
$resultsRoot = Join-Path $projectRoot 'Builds\Validation'
New-Item -ItemType Directory -Path $resultsRoot -Force | Out-Null
$logPath = Join-Path $resultsRoot ("{0}-editor.log" -f $Task.ToLowerInvariant())
$unityArguments = @('-batchmode', '-projectPath', $projectRoot, '-logFile', $logPath)
# EditMode tests may exercise graphics initialization even in batch mode. Do not
# force NullGfx for tests; the hidden Editor retains its normal graphics backend.
if ($Task -ne 'Test') { $unityArguments += '-nographics' }
if ($TimeoutSeconds -eq 0) {
    $TimeoutSeconds = if ($Task -eq 'Test') { 180 } else { 900 }
}

switch ($Task) {
    'Prepare' {
        $unityArguments += @('-quit', '-executeMethod', 'RingGame.Editor.BuildCommands.EnsureProject')
    }
    'Test' {
        # This project currently has plain, single-frame NUnit EditMode tests.
        # A synchronous executeMethod completes and saves XML before batch -quit.
        $testResultsPath = Join-Path $resultsRoot 'editmode-results.xml'
        # A stale XML must never be accepted as evidence for this run.
        if (Test-Path -LiteralPath $testResultsPath) {
            Move-Item -LiteralPath $testResultsPath -Destination ($testResultsPath + '.previous') -Force
        }
        $unityArguments += @('-quit', '-executeMethod', 'RingGame.Editor.TestCommands.RunEditMode',
            '-ringTestResults', $testResultsPath)
    }
    'Android' {
        if (-not $OutputPath) { $OutputPath = Join-Path $projectRoot 'Builds\Android\RingGame-debug.apk' }
        $unityArguments += @('-quit', '-buildTarget', 'Android', '-executeMethod',
            'RingGame.Editor.BuildCommands.BuildAndroid', '-ringBuildPath', $OutputPath,
            '-ringAndroidApi', $AndroidApi.ToString())
    }
    'WindowsSmoke' {
        if (-not $OutputPath) { $OutputPath = Join-Path $projectRoot 'Builds\WindowsSmoke\RingGame.exe' }
        $unityArguments += @('-quit', '-buildTarget', 'Win64', '-executeMethod',
            'RingGame.Editor.BuildCommands.BuildWindowsSmoke', '-ringBuildPath', $OutputPath)
    }
}

if ($OutputPath -and -not [IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath = Join-Path $projectRoot $OutputPath
    # Replace the custom argument with its resolved path, used by artifact verification too.
    $outputIndex = [Array]::IndexOf($unityArguments, '-ringBuildPath')
    if ($outputIndex -ge 0) { $unityArguments[$outputIndex + 1] = $OutputPath }
}

# Start-Process does not use a shell. These arguments are paths or fixed switches.
# Quotes/newlines cannot occur in valid Windows paths and are rejected explicitly.
foreach ($argument in $unityArguments) {
    if ($argument -match '["\r\n]' -or $argument.EndsWith('\')) {
        throw "Unsupported Unity argument (quote, newline or trailing separator): $argument"
    }
}
$processArguments = ($unityArguments | ForEach-Object { '"' + $_ + '"' }) -join ' '
Write-Host "Running Unity task $Task; log: $logPath"
$process = Start-Process -FilePath $UnityPath -ArgumentList $processArguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
# Cache the process handle and wait only for this Editor, rather than for shared
# Unity licensing/helper descendants that may stay alive after the Editor exits.
$null = $process.Handle
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    # Kill only the exact Editor started by this invocation, never unrelated Editors
    # or shared licensing processes. A completed XML alone cannot turn a hang green.
    if (-not $process.HasExited) {
        $process.Kill()
        $null = $process.WaitForExit(10000)
    }
    throw "Unity task $Task timed out after $TimeoutSeconds seconds. This invocation's Editor was terminated; the command failed even if an XML already reports passing tests. Inspect $logPath"
}
$process.Refresh()
$unityExitCode = $process.ExitCode
if ($unityExitCode -ne 0) {
    throw "Unity exited with code $unityExitCode. Inspect $logPath"
}
if ($Task -eq 'Test') {
    if (-not (Test-Path -LiteralPath $testResultsPath -PathType Leaf)) {
        throw "Unity returned without producing test results. Inspect $logPath"
    }
    [xml] $testReport = Get-Content -LiteralPath $testResultsPath -Raw
    $testRun = $testReport.'test-run'
    if ($null -eq $testRun -or [int] $testRun.total -eq 0 -or $testRun.result -ne 'Passed') {
        throw "Test run did not pass, or discovered zero tests. Inspect $testResultsPath"
    }
    Write-Host "Passed $($testRun.total) EditMode tests; results: $testResultsPath"
}
if ($Task -in @('Android', 'WindowsSmoke')) {
    if (-not (Test-Path -LiteralPath $OutputPath -PathType Leaf)) {
        throw "Unity returned without producing $OutputPath"
    }
    Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256 | Format-List
}
