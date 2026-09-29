$dir = "D:\MyDataInD\Unity\RhythmGame\research\_raw"
New-Item -ItemType Directory -Force -Path $dir | Out-Null

$pages = @(
    'Storyboard',
    'Storyboard/Scripting',
    'Storyboard/Scripting/General_Rules',
    'Storyboard/Scripting/Objects',
    'Storyboard/Scripting/Commands',
    'Storyboard/Scripting/Compound_Commands',
    'Storyboard/Scripting/Events',
    'Storyboard/Scripting/Audio',
    'Storyboard/Scripting/Variables',
    'Storyboard/Scripting/Shorthand',
    'Storyboard/Scripting/Trigger',
    'Client/Beatmap_editor',
    'Client/Beatmap_editor/Design',
    'Beatmap/Background',
    'Beatmap/Video',
    'Beatmap/osu%21_File_Formats',
    'Beatmap/Background_video'
)

foreach ($p in $pages) {
    $slug = ($p -replace '[^A-Za-z0-9]', '_')
    $url = "https://osu.ppy.sh/wiki/en/$p"
    $html = Join-Path $dir "$slug.html"
    $md   = Join-Path $dir "$slug.md"
    if (-not (Test-Path $html)) {
        try {
            $r = Invoke-WebRequest -Uri $url -TimeoutSec 60 -UseBasicParsing
            [System.IO.File]::WriteAllText($html, $r.Content)
            Write-Output "OK   $p  (http $($r.StatusCode), $($r.Content.Length) bytes)"
        } catch {
            Write-Output "FAIL $p  -> $($_.Exception.Message)"
            continue
        }
        Start-Sleep -Milliseconds 400
    } else {
        Write-Output "CACH $p"
    }
    try {
        & (Join-Path $dir 'convert.ps1') -In $html -Out $md
        Write-Output ("     converted -> {0} bytes" -f (Get-Item $md).Length)
    } catch {
        Write-Output "     convert error: $($_.Exception.Message)"
    }
}
