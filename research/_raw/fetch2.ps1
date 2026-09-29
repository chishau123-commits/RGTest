$dir = "D:\MyDataInD\Unity\RhythmGame\research\_raw"

$pages = @(
    'Client/File_formats/osb_%28file_format%29',
    'Client/File_formats/osu_%28file_format%29',
    'Storyboard/Scripting/osu%21_File_Toggles',
    'Storyboard/Scripting/Cheat_Sheet',
    'Client/Beatmap_editor/SB_load',
    'Beatmap/Video',
    'Client/File_formats'
)

foreach ($p in $pages) {
    $slug = ($p -replace '[^A-Za-z0-9]', '_')
    $url = "https://osu.ppy.sh/wiki/en/$p"
    $html = Join-Path $dir "$slug.html"
    $md   = Join-Path $dir "$slug.md"
    if (-not (Test-Path $html)) {
        $done = $false
        for ($try = 1; $try -le 3 -and -not $done; $try++) {
            try {
                $r = Invoke-WebRequest -Uri $url -TimeoutSec 60 -UseBasicParsing -ErrorAction Stop
                [System.IO.File]::WriteAllText($html, $r.Content)
                Write-Output "OK   $p (http $($r.StatusCode), $($r.Content.Length) bytes)"
                $done = $true
            } catch {
                Write-Output "  retry$try $p -> $($_.Exception.Message)"
                Start-Sleep -Seconds 3
            }
        }
        if (-not $done) { continue }
    } else { Write-Output "CACH $p" }
    try {
        & (Join-Path $dir 'convert.ps1') -In $html -Out $md
        Write-Output ("     -> {0} bytes" -f (Get-Item $md).Length)
    } catch { Write-Output "     convert error: $($_.Exception.Message)" }
}
