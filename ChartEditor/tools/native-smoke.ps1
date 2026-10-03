param([string]$Executable, [string]$EvidenceDirectory)
$ErrorActionPreference = 'Stop'
$editorRoot = Split-Path -Parent $PSScriptRoot
$repositoryRoot = Split-Path -Parent $editorRoot
if (-not $Executable) { $Executable = Join-Path $repositoryRoot 'Builds/ChartEditor/win-unpacked/Ring Chart Editor.exe' }
if (-not $EvidenceDirectory) { $EvidenceDirectory = Join-Path $repositoryRoot 'Builds/ChartEditorEvidence/native-profile' }
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$EvidenceDirectory = [IO.Path]::GetFullPath($EvidenceDirectory)
New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
$reportFile = Join-Path $EvidenceDirectory 'startup-report.json'
if (Test-Path -LiteralPath $reportFile) { Remove-Item -LiteralPath $reportFile }
$argumentList = @('--verify-startup', ('--verification-dir="' + $EvidenceDirectory + '"'))
$editorProcess = Start-Process -FilePath $Executable -ArgumentList $argumentList -WindowStyle Hidden -PassThru
if (-not $editorProcess.WaitForExit(60000)) { Stop-Process -Id $editorProcess.Id; throw 'Native startup exceeded 60 seconds' }
if (-not (Test-Path -LiteralPath $reportFile)) { throw 'Renderer did not produce a startup report' }
$report = Get-Content -LiteralPath $reportFile -Raw | ConvertFrom-Json
if ($report.status -ne 'ready' -or -not $report.nativeBridge -or $report.noteCount -ne 38 -or $report.validationErrors -ne 0 -or $report.audioDuration -le 35 -or $report.stageWidth -le 0 -or [Math]::Abs($report.stageWidth / $report.stageHeight - 16/9) -gt 0.015) {
    throw ('Native startup verification failed: ' + ($report | ConvertTo-Json -Compress))
}
$report | ConvertTo-Json
if ($report.tabletRoutes -and @($report.tabletRoutes.PSObject.Properties | Where-Object { $_.Value -ne 200 }).Count -gt 0) { throw 'Packaged tablet assets failed HTTP checks' }
