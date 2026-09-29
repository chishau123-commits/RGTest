$dir = "D:\MyDataInD\Unity\RhythmGame\research\_raw\osu-src"
New-Item -ItemType Directory -Force -Path $dir | Out-Null

$paths = @(
    'osu.Game/Storyboards/Commands/StoryboardLoopingGroup.cs',
    'osu.Game/Storyboards/Commands/StoryboardTriggerGroup.cs',
    'osu.Game/Storyboards/Commands/StoryboardCommandGroup.cs',
    'osu.Game/Storyboards/Commands/StoryboardAlphaCommand.cs',
    'osu.Game/Storyboards/IStoryboardElement.cs',
    'osu.Game/Storyboards/IStoryboardElementWithDuration.cs',
    'osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs',
    'osu.Game/Storyboards/Commands/StoryboardPositionCommand.cs',
    'osu.Game/Storyboards/Commands/StoryboardXCommand.cs',
    'osu.Game/Storyboards/Commands/StoryboardYCommand.cs',
    'osu.Game/Storyboards/Commands/StoryboardFlipHCommand.cs',
    'osu.Game/Storyboards/Commands/StoryboardFlipVCommand.cs',
    'osu.Game/Storyboards/Commands/StoryboardBlendingParametersCommand.cs',
    'osu.Game/Storyboards/Commands/StoryboardAdditiveCommand.cs',
    'osu.Game/Storyboards/Commands/StoryboardTriggerController.cs',
    'osu.Game/Storyboards/StoryboardTriggerController.cs',
    'osu.Game/Storyboards/Drawables/DrawableStoryboardTrigger.cs',
    'osu.Game/Storyboards/StoryboardReversibleCommand.cs',
    'osu.Game/Storyboards/Commands/StoryboardCommandExtensions.cs'
)

foreach ($p in $paths) {
    $name = ($p -replace '[/\\]', '__')
    $out = Join-Path $dir $name
    if (Test-Path $out) { Write-Output "CACH $p"; continue }
    try {
        $r = Invoke-WebRequest -Uri ("https://ghproxy.net/https://raw.githubusercontent.com/ppy/osu/master/" + $p) -TimeoutSec 30 -UseBasicParsing -ErrorAction Stop
        [System.IO.File]::WriteAllText($out, $r.Content)
        Write-Output ("OK   {0} ({1} bytes)" -f $p, $r.Content.Length)
    } catch {
        $m = $_.Exception.Message; if ($m -match '404') { Write-Output "404  $p" } else { Write-Output "ERR  $p" }
    }
    Start-Sleep -Milliseconds 200
}
