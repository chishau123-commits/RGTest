param(
    [string]$Exe = "d:\MyDataInD\Unity\RhythmGame\Builds\ThartEditor\ThartEditor.exe",
    [string]$Out = "d:\MyDataInD\Unity\RhythmGame\Builds\thart-verify.png",
    [int]$WaitMs = 15000,
    [int]$SettleMs = 6000,
    [string]$ClickPoints = "0.279,0.286,0.293,0.300,0.307",
    [double]$ClickY = 0.806,
    [string]$Keys = "3 ",
    [int]$PostKeyWaitMs = 1500,
    [int]$BurstCount = 1,
    [int]$BurstIntervalMs = 900
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32 {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
}
"@

$LEFTDOWN = 0x0002
$LEFTUP = 0x0004

[Win32]::SetProcessDPIAware() | Out-Null

$p = Start-Process -FilePath $Exe -PassThru
$hwnd = [IntPtr]::Zero
$deadline = (Get-Date).AddMilliseconds($WaitMs)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 300
    $p.Refresh()
    if ($p.MainWindowHandle -ne [IntPtr]::Zero) { $hwnd = $p.MainWindowHandle; break }
}
if ($hwnd -eq [IntPtr]::Zero) { Write-Output "NO_WINDOW"; exit 1 }

[Win32]::ShowWindow($hwnd, 9) | Out-Null
[Win32]::SetWindowPos($hwnd, [IntPtr]::new(-1), 0, 0, 0, 0, 0x13) | Out-Null
[Win32]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds $SettleMs

# client origin in screen coords
$origin = [Win32+POINT]::new()
[Win32]::ClientToScreen($hwnd, [ref]$origin) | Out-Null
$cr = [Win32+RECT]::new()
[Win32]::GetClientRect($hwnd, [ref]$cr) | Out-Null
$cw = $cr.Right - $cr.Left
$ch = $cr.Bottom - $cr.Top
Write-Output "CLIENT ${cw}x${ch} ORIGIN $($origin.X),$($origin.Y)"

# click notes into the P1 row
if ($ClickPoints -ne "" -and $ClickPoints -ne "none") {
    foreach ($fxStr in $ClickPoints.Split(",")) {
        $fx = [double]$fxStr
        $sx = $origin.X + [int]($cw * $fx)
        $sy = $origin.Y + [int]($ch * $ClickY)
        [Win32]::SetCursorPos($sx, $sy) | Out-Null
        Start-Sleep -Milliseconds 150
        [Win32]::mouse_event($LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 60
        [Win32]::mouse_event($LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 350
        Write-Output "CLICK $sx,$sy"
    }
}

# keys: 3 = preview, space = play
if ($Keys -ne "" -and $Keys -ne "none") {
    foreach ($k in $Keys.ToCharArray()) {
        [System.Windows.Forms.SendKeys]::SendWait([string]$k)
        Start-Sleep -Milliseconds 700
    }
}
Start-Sleep -Milliseconds $PostKeyWaitMs

$r = [Win32+RECT]::new()
[Win32]::GetWindowRect($hwnd, [ref]$r) | Out-Null
$w = $r.Right - $r.Left
$h = $r.Bottom - $r.Top

for ($i = 0; $i -lt $BurstCount; $i++) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
    if ($BurstCount -le 1) {
        $path = $Out
    } else {
        $dir = [System.IO.Path]::GetDirectoryName($Out)
        $base = [System.IO.Path]::GetFileNameWithoutExtension($Out)
        $ext = [System.IO.Path]::GetExtension($Out)
        $path = Join-Path $dir ("{0}_{1}{2}" -f $base, $i, $ext)
    }
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Output "SAVED $path ${w}x${h}"
    if ($i -lt $BurstCount - 1) { Start-Sleep -Milliseconds $BurstIntervalMs }
}