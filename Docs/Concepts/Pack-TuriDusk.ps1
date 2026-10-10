param([Parameter(Mandatory=$true)][string]$SourceSheet)
Add-Type -AssemblyName System.Drawing
$dest=Join-Path $PSScriptRoot '../../Assets/RhythmDemo/Resources/Dusk'
$pack=Join-Path $PSScriptRoot 'TuriDuskSprites'
New-Item -ItemType Directory -Force $dest,$pack | Out-Null
$src=[System.Drawing.Bitmap]::new($SourceSheet)
$atlas=[System.Drawing.Bitmap]::new(384,128)
$ag=[System.Drawing.Graphics]::FromImage($atlas)
$ag.Clear([System.Drawing.Color]::Transparent)
$names=@('idle-01','idle-02','walk-01','walk-02','walk-03','walk-04','walk-05','walk-06')
for($i=0;$i -lt 8;$i++) {
    $x0=[int][Math]::Round($i*$src.Width/8.0)
    $x1=[int][Math]::Round(($i+1)*$src.Width/8.0)
    $minX=$x1;$maxX=$x0;$minY=$src.Height;$maxY=0
    for($y=0;$y -lt $src.Height;$y++) {for($x=$x0;$x -lt $x1;$x++) {
        if($src.GetPixel($x,$y).A -gt 100) {
            $minX=[Math]::Min($minX,$x);$maxX=[Math]::Max($maxX,$x)
            $minY=[Math]::Min($minY,$y);$maxY=[Math]::Max($maxY,$y)
        }
    }}
    if($maxX -le $minX){throw "Empty frame $i"}
    $w=$maxX-$minX+1;$h=$maxY-$minY+1
    $tw=[int][Math]::Round(54*$w/$h)
    $frame=[System.Drawing.Bitmap]::new(48,64)
    # Nearest-neighbor sampling preserves a fixed 54px body and 60px foot baseline.
    for($dy=0;$dy -lt 54;$dy++){for($dx=0;$dx -lt $tw;$dx++){
        $sx=$minX+[int][Math]::Floor(($dx+.5)*$w/$tw)
        $sy=$minY+[int][Math]::Floor(($dy+.5)*$h/54)
        $frame.SetPixel([int][Math]::Floor((48-$tw)/2)+$dx,6+$dy,$src.GetPixel($sx,$sy))
    }}
    $frame.Save((Join-Path $pack ('turi-right-'+$names[$i]+'.png')))
    $ag.DrawImageUnscaled($frame,$i*48,0)
    $frame.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipX)
    $frame.Save((Join-Path $pack ('turi-left-'+$names[$i]+'.png')))
    $ag.DrawImageUnscaled($frame,$i*48,64)
    $frame.Dispose()
}
$atlas.Save((Join-Path $dest 'turi-walk.png'))
$atlas.Save((Join-Path $pack 'turi-walk.png'))
$ag.Dispose();$atlas.Dispose();$src.Dispose()
@'
Turi / Dusk sprite pack
16 transparent PNG frames; 48 x 64 each. Atlas: 384 x 128.
Top row: RIGHT. Bottom row: LEFT (mirrored).
Columns: idle 01, idle 02, walk 01..06. Walk: 9 fps; idle: 1.5 fps.
Feet baseline: top-down y=60. Unity pivot: (0.5, 0.0625), PPU=32.
Point filtering, no mipmaps, no compression. Both directions share matching timing.
Source artwork: AI generated for this project, based on the existing Turi design.
'@ | Set-Content (Join-Path $pack 'README.txt') -Encoding utf8
Compress-Archive -Path (Join-Path $pack '*') -DestinationPath (Join-Path $PSScriptRoot 'TuriDuskSprites.zip') -Force
