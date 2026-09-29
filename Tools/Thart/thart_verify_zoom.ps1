param(
    [string]$Exe = "d:\MyDataInD\Unity\RhythmGame\Builds\ThartEditor\ThartEditor.exe",
    [string]$OutDir = "d:\MyDataInD\Unity\RhythmGame\Builds\verify",
    [int]$WaitMs = 15000,
    [int]$SettleMs = 7000
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32Z {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, int data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
}
"@

$LDOWN = 0x0002
$LUP = 0x0004
$WHEEL = 0x0800
$VK_CONTROL = 0x11
$KEYUP = 0x0002

[Win32Z]::SetProcessDPIAware() | Out-Null
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

$p = Start-Process -FilePath $Exe -PassThru
$hwnd = [IntPtr]::Zero
$deadline = (Get-Date).AddMilliseconds($WaitMs)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 300
    $p.Refresh()
    if ($p.MainWindowHandle -ne [IntPtr]::Zero) { $hwnd = $p.MainWindowHandle; break }
}
if ($hwnd -eq [IntPtr]::Zero) { Write-Output "NO_WINDOW"; exit 1 }

[Win32Z]::ShowWindow($hwnd, 9) | Out-Null
[Win32Z]::SetWindowPos($hwnd, [IntPtr]::new(-1), 0, 0, 0, 0, 0x13) | Out-Null
[Win32Z]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds $SettleMs

$origin = [Win32Z+POINT]::new()
[Win32Z]::ClientToScreen($hwnd, [ref]$origin) | Out-Null
$cr = [Win32Z+RECT]::new()
[Win32Z]::GetClientRect($hwnd, [ref]$cr) | Out-Null
$cw = $cr.Right - $cr.Left
$ch = $cr.Bottom - $cr.Top
Write-Output "CLIENT ${cw}x${ch} ORIGIN $($origin.X),$($origin.Y)"

function Get-Pt([double]$fx, [double]$fy) {
    $pt = [Win32Z+POINT]::new()
    $pt.X = $origin.X + [int]($cw * $fx)
    $pt.Y = $origin.Y + [int]($ch * $fy)
    return $pt
}

function Click([double]$fx, [double]$fy) {
    $pt = Get-Pt $fx $fy
    [Win32Z]::SetCursorPos($pt.X, $pt.Y) | Out-Null
    Start-Sleep -Milliseconds 150
    [Win32Z]::mouse_event($LDOWN, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [Win32Z]::mouse_event($LUP, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 350
    Write-Output "CLICK $($pt.X),$($pt.Y)"
}

function Wheel([double]$fx, [double]$fy, [int]$notches) {
    $pt = Get-Pt $fx $fy
    [Win32Z]::SetCursorPos($pt.X, $pt.Y) | Out-Null
    Start-Sleep -Milliseconds 150
    [Win32Z]::keybd_event($VK_CONTROL, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 80
    $delta = if ($notches -gt 0) { 120 } else { -120 }
    for ($i = 0; $i -lt [Math]::Abs($notches); $i++) {
        [Win32Z]::mouse_event($WHEEL, 0, 0, $delta, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 180
    }
    [Win32Z]::keybd_event($VK_CONTROL, 0, $KEYUP, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 500
    Write-Output "WHEEL $notches @ $($pt.X),$($pt.Y)"
}

function Shot([string]$name) {
    $r = [Win32Z+RECT]::new()
    [Win32Z]::GetWindowRect($hwnd, [ref]$r) | Out-Null
    $w = $r.Right - $r.Left
    $h = $r.Bottom - $r.Top
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
    $path = Join-Path $OutDir ($name + ".png")
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Output "SAVED $path"
}

# warm up: first click after launch is sometimes swallowed while focus settles
Click 0.5 0.975

# switch to Preview mode
[System.Windows.Forms.SendKeys]::SendWait("3")
Start-Sleep -Milliseconds 1500
Shot "01-preview"

# requirement 3: click the far right end of the speed slider -> should read ~48x
Click 0.218 0.5374
Shot "02-speed-max"

# requirement 1: add notes in the P1 row, then scrub the playhead onto one of them
Click 0.305 0.8115
Click 0.326 0.8115
Click 0.346 0.8115
Shot "03-notes-added"

Click 0.305 0.6951
Shot "04-preview-note"

# requirement 4: Ctrl + wheel over the timeline to zoom in
Wheel 0.55 0.6951 5
Shot "05-zoom-in"

# and back out
Wheel 0.55 0.6951 -10
Shot "06-zoom-out"

Write-Output "DONE"