param(
    [string]$OutDir = "d:\MyDataInD\Unity\RhythmGame\Builds\verify",
    [string]$Shot = "",
    [string]$Mode = "",
    [double]$WheelX = -1,
    [double]$WheelY = -1,
    [int]$WheelNotches = 0,
    [double]$ClickX = -1,
    [double]$ClickY = -1,
    [int]$WaitMs = 1200
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32A {
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

[Win32A]::SetProcessDPIAware() | Out-Null
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

$p = Get-Process ThartEditor -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $p) { Write-Output "NO_PROCESS"; exit 1 }
$hwnd = $p.MainWindowHandle
if ($hwnd -eq [IntPtr]::Zero) { Write-Output "NO_WINDOW"; exit 1 }

[Win32A]::ShowWindow($hwnd, 9) | Out-Null
Start-Sleep -Milliseconds 500
[Win32A]::SetWindowPos($hwnd, [IntPtr]::new(-1), 0, 0, 0, 0, 0x0013) | Out-Null
[Win32A]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 500

$origin = New-Object Win32A+POINT
[Win32A]::ClientToScreen($hwnd, [ref]$origin) | Out-Null
$cr = New-Object Win32A+RECT
[Win32A]::GetClientRect($hwnd, [ref]$cr) | Out-Null
$cw = $cr.Right - $cr.Left
$ch = $cr.Bottom - $cr.Top
if ($cw -le 0 -or $ch -le 0) { Write-Output "BAD_CLIENT"; exit 1 }
Write-Output "CLIENT ${cw}x${ch} ORIGIN $($origin.X),$($origin.Y)"

function Get-Pt([double]$fx, [double]$fy) {
    $pt = New-Object Win32A+POINT
    $pt.X = $origin.X + [int]($cw * $fx)
    $pt.Y = $origin.Y + [int]($ch * $fy)
    return $pt
}

function DoClick([double]$fx, [double]$fy) {
    $pt = Get-Pt $fx $fy
    [Win32A]::SetCursorPos($pt.X, $pt.Y) | Out-Null
    Start-Sleep -Milliseconds 200
    [Win32A]::mouse_event($LDOWN, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 70
    [Win32A]::mouse_event($LUP, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 400
    Write-Output "CLICK $($pt.X),$($pt.Y)"
}

function DoWheel([double]$fx, [double]$fy, [int]$notches) {
    $pt = Get-Pt $fx $fy
    [Win32A]::SetCursorPos($pt.X, $pt.Y) | Out-Null
    Start-Sleep -Milliseconds 200
    [Win32A]::keybd_event($VK_CONTROL, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 100
    $delta = if ($notches -gt 0) { 120 } else { -120 }
    for ($i = 0; $i -lt [Math]::Abs($notches); $i++) {
        [Win32A]::mouse_event($WHEEL, 0, 0, $delta, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 200
    }
    [Win32A]::keybd_event($VK_CONTROL, 0, $KEYUP, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 500
    Write-Output "CTRL+WHEEL $notches @ $($pt.X),$($pt.Y)"
}

function DoShot([string]$name) {
    $bmp = New-Object System.Drawing.Bitmap $cw, $ch
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($origin.X, $origin.Y, 0, 0, $bmp.Size)
    $path = Join-Path $OutDir ($name + ".png")
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Output "SAVED $path"
}

if ($Mode -ne "") {
    foreach ($k in $Mode.ToCharArray()) {
        [System.Windows.Forms.SendKeys]::SendWait([string]$k)
        Start-Sleep -Milliseconds 900
    }
}

if ($ClickX -ge 0 -and $ClickY -ge 0) { DoClick $ClickX $ClickY }
if ($WheelNotches -ne 0 -and $WheelX -ge 0 -and $WheelY -ge 0) { DoWheel $WheelX $WheelY $WheelNotches }

Start-Sleep -Milliseconds $WaitMs
if ($Shot -ne "") { DoShot $Shot }
Write-Output "OK"