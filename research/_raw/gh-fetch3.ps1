$dir = "D:\MyDataInD\Unity\RhythmGame\research\_raw\osu-src"
New-Item -ItemType Directory -Force -Path $dir | Out-Null

$paths = @(
    'osu.Game/Rulesets/UI/Playfield.cs',
    'osu.Game/Rulesets/UI/PlayfieldAdjustmentContainer.cs',
    'osu.Game/Rulesets/Objects/HitObject.cs',
    'osu.Game/Rulesets/Objects/Drawables/DrawableHitObject.cs',
    'osu.Game/Rulesets/Objects/Types/IHasXPosition.cs',
    'osu.Game/Rulesets/Scoring/HitWindows.cs',
    'osu.Game.Rulesets.Osu/OsuRuleset.cs',
    'osu.Game.Rulesets.Osu/UI/OsuPlayfield.cs',
    'osu.Game.Rulesets.Osu/UI/OsuPlayfieldAdjustmentContainer.cs',
    'osu.Game.Rulesets.Osu/Objects/OsuHitObject.cs',
    'osu.Game.Rulesets.Osu/Objects/HitCircle.cs',
    'osu.Game.Rulesets.Osu/Objects/Slider.cs',
    'osu.Game.Rulesets.Osu/Objects/Spinner.cs',
    'osu.Game.Rulesets.Osu/Objects/SliderTick.cs',
    'osu.Game.Rulesets.Osu/Objects/Drawables/DrawableOsuHitObject.cs',
    'osu.Game.Rulesets.Osu/Objects/Drawables/DrawableHitCircle.cs',
    'osu.Game.Rulesets.Osu/Objects/Drawables/ApproachCircle.cs',
    'osu.Game.Rulesets.Osu/Objects/Drawables/DrawableSlider.cs',
    'osu.Game.Rulesets.Osu/Objects/Drawables/DrawableSliderHead.cs',
    'osu.Game.Rulesets.Osu/Objects/Drawables/DrawableSliderTail.cs',
    'osu.Game.Rulesets.Osu/Objects/Drawables/DrawableSpinner.cs',
    'osu.Game.Rulesets.Osu/Scoring/OsuHitWindows.cs',
    'osu.Game.Rulesets.Osu/Beatmaps/OsuBeatmapProcessor.cs',
    'osu.Game.Rulesets.Osu/Skinning/Default/DefaultApproachCircle.cs',
    'osu.Game/Configuration/OsuConfigManager.cs',
    'osu.Game/Beatmaps/WorkingBeatmap.cs',
    'osu.Game/Beatmaps/Beatmap.cs',
    'osu.Game/Screens/Play/Player.cs',
    'osu.Game/Screens/Play/PlayerLoader.cs',
    'osu.Game/Storyboards/StoryboardVideo.cs',
    'osu.Game/Beatmaps/Formats/LegacyBeatmapDecoder.cs',
    'osu.Game.Rulesets.Osu/Skinning/Argon/ArgonApproachCircle.cs',
    'osu.Game/Rulesets/UI/ScalingContainer.cs',
    'osu.Game/Rulesets/Objects/Types/IHasPosition.cs'
)

$proxies = @('https://ghproxy.net/', 'https://gh-proxy.com/')
$base = 'https://raw.githubusercontent.com/ppy/osu/master/'

foreach ($p in $paths) {
    $name = ($p -replace '[/\\]', '__')
    $out = Join-Path $dir $name
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
    Start-Sleep -Milliseconds 200
}
