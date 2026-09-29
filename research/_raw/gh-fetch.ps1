$dir = "D:\MyDataInD\Unity\RhythmGame\research\_raw"
New-Item -ItemType Directory -Force -Path "$dir\osu-src" | Out-Null

$paths = @(
    'osu.Game/Storyboards/Storyboard.cs',
    'osu.Game/Storyboards/StoryboardSprite.cs',
    'osu.Game/Storyboards/StoryboardAnimation.cs',
    'osu.Game/Storyboards/StoryboardElement.cs',
    'osu.Game/Storyboards/StoryboardLayer.cs',
    'osu.Game/Storyboards/CommandTimeline.cs',
    'osu.Game/Storyboards/CommandTimelineGroup.cs',
    'osu.Game/Storyboards/StoryboardTriggerGroup.cs',
    'osu.Game/Storyboards/Commands/StoryboardCommand.cs',
    'osu.Game/Storyboards/Commands/StoryboardFadeCommand.cs',
    'osu.Game/Storyboards/Commands/StoryboardMoveCommand.cs',
    'osu.Game/Storyboards/Commands/StoryboardMoveXCommand.cs',
    'osu.Game/Storyboards/Commands/StoryboardMoveYCommand.cs',
    'osu.Game/Storyboards/Commands/StoryboardScaleCommand.cs',
    'osu.Game/Storyboards/Commands/StoryboardVectorScaleCommand.cs',
    'osu.Game/Storyboards/Commands/StoryboardRotationCommand.cs',
    'osu.Game/Storyboards/Commands/StoryboardColourCommand.cs',
    'osu.Game/Storyboards/Commands/StoryboardParameterCommand.cs',
    'osu.Game/Storyboards/Drawables/DrawableStoryboard.cs',
    'osu.Game/Storyboards/Drawables/DrawableStoryboardSprite.cs',
    'osu.Game/Storyboards/Drawables/DrawableStoryboardAnimation.cs',
    'osu.Game/IO/Legacy/LegacyStoryboardDecoder.cs',
    'osu.Game/Beatmaps/Formats/LegacyBeatmapDecoder.cs',
    'osu.Game/Beatmaps/Formats/LegacyDecoder.cs',
    'osu.Game/Storyboards/Storyboards.cs'
)

$proxies = @('https://ghproxy.net/', 'https://gh-proxy.com/')
$base = 'https://raw.githubusercontent.com/ppy/osu/master/'

foreach ($p in $paths) {
    $name = ($p -replace '[/\\]', '__')
    $out = Join-Path "$dir\osu-src" $name
    if (Test-Path $out) { Write-Output "CACH $p"; continue }
    $ok = $false
    foreach ($px in $proxies) {
        try {
            $r = Invoke-WebRequest -Uri ($px + $base + $p) -TimeoutSec 45 -UseBasicParsing -ErrorAction Stop
            [System.IO.File]::WriteAllText($out, $r.Content)
            Write-Output ("OK   {0}  ({1} bytes)" -f $p, $r.Content.Length)
            $ok = $true
            break
        } catch {
            $msg = $_.Exception.Message
            if ($msg.Length -gt 45) { $msg = $msg.Substring(0, 45) }
            Write-Output ("  ..  {0} via {1} -> {2}" -f $p, $px, $msg)
        }
    }
    if (-not $ok) { Write-Output "FAIL $p" }
    Start-Sleep -Milliseconds 250
}
