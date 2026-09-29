param(
    [string]$OutDir = "d:\MyDataInD\Unity\RhythmGame\Builds\verify",
    [string]$Shot = "cap",
    [int]$ModeKey = 0,
    [double]$ClickX = -1,
    [double]$ClickY = -1,
    [double]$WheelX = -1,
    [double]$WheelY = -1,
    [int]$WheelNotches = 0,
    [int]$ClickCount = 1,
    [int]$SettleMs = 1500
)

Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32P {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wp, IntPtr lp);
    [DllImport("user32.dll")] public static extern IntPtr PostMessage(IntPtr hWnd, uint msg, IntPtr wp, IntPtr lp);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, int data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int index);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
}
"@

[Win32P]::SetProcessDPIAware() | Out-Null
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

$p = Get-Process ThartEditor -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $p) { Write-Output "NO_PROCESS"; exit 1 }
$hwnd = $p.MainWindowHandle
if ($hwnd -eq [IntPtr]::Zero) { Write-Output "NO_WINDOW"; exit 1 }
Write-Output "HWND $hwnd"

[Win32P]::ShowWindow($hwnd, 9) | Out-Null
Start-Sleep -Milliseconds 700
[Win32P]::SetWindowPos($hwnd, [IntPtr]::new(-1), 0, 0, 0, 0, 0x0053) | Out-Null
[Win32P]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 700

$WM_LBUTTONDOWN = 0x0201
$WM_LBUTTONUP = 0x0202
$WM_MOUSEMOVE = 0x0200
$WM_MOUSEWHEEL = 0x020A
$WM_KEYDOWN = 0x0100
$WM_KEYUP = 0x0101
$MK_LBUTTON = 0x0001
$VK_CONTROL = 0x11
$LDOWN = 0x0002
$LUP = 0x0004
$WHEEL = 0x0800
$KEYUP = 0x0002

function Get-LParam([int]$x, [int]$y) {
    return [IntPtr](($y -shl 16) -bor ($x -band 0xFFFF))
}

$cr = New-Object Win32P+RECT
[Win32P]::GetClientRect($hwnd, [ref]$cr) | Out-Null
$cw = $cr.Right - $cr.Left
$ch = $cr.Bottom - $cr.Top
Write-Output "CLIENT ${cw}x${ch}"

function DoPostClick([double]$fx, [double]$fy) {
    $x = [int]($cw * $fx); $y = [int]($ch * $fy)
    $pt = New-Object Win32P+POINT
    $pt.X = $x; $pt.Y = $y
    [Win32P]::ClientToScreen($hwnd, [ref]$pt) | Out-Null
    [Win32P]::SetCursorPos($pt.X, $pt.Y) | Out-Null
    Start-Sleep -Milliseconds 220
    $n = if ($ClickCount -lt 1) { 1 } else { $ClickCount }
    for ($c = 0; $c -lt $n; $c++) {
        [Win32P]::mouse_event($LDOWN, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 70
        [Win32P]::mouse_event($LUP, 0, 0, 0, [UIntPtr]::Zero)
        if ($c -lt $n - 1) { Start-Sleep -Milliseconds 90 }
    }
    Start-Sleep -Milliseconds 500
    Write-Output "REALCLICK x$n client=$x,$y screen=$($pt.X),$($pt.Y)"
}

function DoPostWheel([double]$fx, [double]$fy, [int]$notches) {
    $x = [int]($cw * $fx); $y = [int]($ch * $fy)
    $pt = New-Object Win32P+POINT
    $pt.X = $x; $pt.Y = $y
    [Win32P]::ClientToScreen($hwnd, [ref]$pt) | Out-Null
    [Win32P]::SetCursorPos($pt.X, $pt.Y) | Out-Null
    Start-Sleep -Milliseconds 220
    $fg = [Win32P]::GetForegroundWindow()
    Write-Output "FG=$fg HWND=$hwnd match=$($fg -eq $hwnd)"
    [Win32P]::keybd_event($VK_CONTROL, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    $delta = if ($notches -gt 0) { 120 } else { -120 }
    for ($i = 0; $i -lt [Math]::Abs($notches); $i++) {
        [Win32P]::mouse_event($WHEEL, 0, 0, $delta, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 220
    }
    [Win32P]::keybd_event($VK_CONTROL, 0, $KEYUP, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 500
    Write-Output "REALWHEEL $notches @ client=$x,$y screen=$($pt.X),$($pt.Y)"
}

function DoCapture([string]$name) {
    $bmp = New-Object System.Drawing.Bitmap $cw, $ch
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    $ok = [Win32P]::PrintWindow($hwnd, $hdc, 2)
    $g.ReleaseHdc($hdc)
    $g.Dispose()
    $path = Join-Path $OutDir ($name + ".png")
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "PRINTWINDOW $ok SAVED $path"
}

if ($ModeKey -gt 0) {
    [Win32P]::PostMessage($hwnd, $WM_KEYDOWN, [IntPtr]$ModeKey, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 120
    [Win32P]::PostMessage($hwnd, $WM_KEYUP, [IntPtr]$ModeKey, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 900
    Write-Output "KEY $ModeKey"
}

if ($ClickX -ge 0 -and $ClickY -ge 0) { DoPostClick $ClickX $ClickY }
if ($WheelNotches -ne 0 -and $WheelX -ge 0 -and $WheelY -ge 0) { DoPostWheel $WheelX $WheelY $WheelNotches }

Start-Sleep -Milliseconds $SettleMs
DoCapture $Shot
Write-Output "OK"