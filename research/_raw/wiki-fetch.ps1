$dir = "D:\MyDataInD\Unity\RhythmGame\research\_raw\wiki-raw"
New-Item -ItemType Directory -Force -Path $dir | Out-Null

$pages = @(
    'Client/Playfield',
    'Gameplay/Hit_object',
    'Gameplay/Hit_object/Hit_circle',
    'Gameplay/Hit_object/Slider',
    'Gameplay/Hit_object/Spinner',
    'Gameplay/Hit_object/Approach_circle',
    'Gameplay/Hit_object/Slider/Slider_tick',
    'Gameplay/Hit_object/Slider/Slider_velocity',
    'Gameplay/Hit_object/Slider/Sliderhead',
    'Gameplay/Hit_object/Slider/Slidertail',
    'Gameplay/Hit_object/Slider/Sliderbody',
    'Gameplay/Hit_object/Slider/Repeat_slider',
    'Gameplay/Hit_window',
    'Gameplay/Judgement',
    'Gameplay/Judgement/osu!',
    'Gameplay/Judgement/Notelock',
    'Gameplay/Judgement/Slider_break',
    'Gameplay/Accuracy',
    'Gameplay/Score/ScoreV1/osu!',
    'Beatmap/Overall_difficulty',
    'Beatmap/Circle_size',
    'Beatmap/Approach_rate',
    'Beatmap/Difficulty',
    'Beatmapping/Slider_tick_rate',
    'Ranking_criteria/osu!',
    'Storyboard',
    'Storyboard/Scripting',
    'Storyboard/Scripting/General_Rules',
    'Storyboard/Scripting/Objects',
    'Storyboard/Scripting/Commands',
    'Storyboard/Scripting/osu!_File_Toggles',
    'Beatmap/Background',
    'Client/Beatmap_editor/Song_setup',
    'Client/Beatmap_editor/Design',
    'Client/File_formats/osu_(file_format)',
    'Client/File_formats/osb_(file_format)',
    'Client/Interface/Visual_settings',
    'Skinning/osu!',
    'Gameplay/Game_modifier/No_Video',
    'Beatmapping/Mapping_techniques/Spinners',
    'Guides/Making_properly_centred_spinners',
    'Beatmapping/Mapping_techniques/Sliders',
    'Beatmapping/Mapping_techniques/Making_good_sliders'
)

$base = 'https://ghproxy.net/https://raw.githubusercontent.com/ppy/osu-wiki/master/wiki/'

foreach ($p in $pages) {
    $slug = ($p -replace '[^A-Za-z0-9]', '_')
    $out = Join-Path $dir "$slug.md"
    if (Test-Path $out) { Write-Output "CACH $p"; continue }
    $url = $base + ($p -replace '\(', '%28' -replace '\)', '%29') + '/en.md'
    try {
        $r = Invoke-WebRequest -Uri $url -TimeoutSec 45 -UseBasicParsing -ErrorAction Stop
        [System.IO.File]::WriteAllText($out, $r.Content)
        Write-Output ("OK   {0}  ({1} bytes)" -f $p, $r.Content.Length)
    } catch {
        Write-Output ("FAIL {0}  {1}" -f $p, $_.Exception.Message)
    }
    Start-Sleep -Milliseconds 150
}
