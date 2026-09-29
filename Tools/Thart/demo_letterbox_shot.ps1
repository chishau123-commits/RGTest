param(
    [string]$Exe = "d:\MyDataInD\Unity\RhythmGame\Builds\GeometryRhythm169\GeometryRhythmDemo.exe",
    [string]$AppArgs = "-demoSmoke -demoCapture d:\MyDataInD\Unity\RhythmGame\Builds\verify\demo169",
    [string]$Out = "d:\MyDataInD\Unity\RhythmGame\Builds\verify\demo-169-letterbox.png",
    [int]$ScreenW = 1200,
    [int]$ScreenH = 1000,
    [int]$ShotDelayMs = 4000,
    [int]$WaitMs = 40000
)

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class PlayShot {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
}
"@

[PlayShot]::SetProcessDPIAware() | Out-Null
Get-Process GeometryRhythmDemo -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800

$full = "$AppArgs -screen-width $ScreenW -screen-height $ScreenH -screen-fullscreen 0"
$p = Start-Process -FilePath $Exe -ArgumentList $full -PassThru

$hwnd = [IntPtr]::Zero
$deadline = (Get-Date).AddMilliseconds(20000)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 300
    $p.Refresh()
    if ($p.HasExited) { Write-Output "EXITED"; exit 1 }
    if ($p.MainWindowHandle -ne [IntPtr]::Zero) { $hwnd = $p.MainWindowHandle; break }
}
if ($hwnd -eq [IntPtr]::Zero) { Write-Output "NO_WINDOW"; exit 1 }

[PlayShot]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds $ShotDelayMs

$origin = New-Object PlayShot+POINT
[PlayShot]::ClientToScreen($hwnd, [ref]$origin) | Out-Null
$cr = New-Object PlayShot+RECT
[PlayShot]::GetClientRect($hwnd, [ref]$cr) | Out-Null
$cw = $cr.Right - $cr.Left
$ch = $cr.Bottom - $cr.Top

# The smoke ends quickly and the splash covers the first seconds: take a burst.
$base = [System.IO.Path]::GetFileNameWithoutExtension($Out)
$dir = [System.IO.Path]::GetDirectoryName($Out)
for ($i = 0; $i -lt 5; $i++) {
    $p.Refresh()
    if ($p.HasExited) { Write-Output "EXITED_AT_$i"; break }
    $bmp = New-Object System.Drawing.Bitmap $cw, $ch
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($origin.X, $origin.Y, 0, 0, $bmp.Size)
    $path = Join-Path $dir ("{0}-{1}.png" -f $base, $i)
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Output "SAVED $path client ${cw}x${ch}"
    Start-Sleep -Milliseconds 700
}

# let the smoke finish on its own (it writes player-smoke.txt and quits)
$p.WaitForExit($WaitMs) | Out-Null
if (-not $p.HasExited) { $p.Kill() }
Write-Output "PLAYER_EXIT=$($p.ExitCode)"
