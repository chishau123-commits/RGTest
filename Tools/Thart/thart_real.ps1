param(
    [string]$OutDir = "d:\MyDataInD\Unity\RhythmGame\Builds\verify",
    [string]$Shot = "",
    [double]$ClickX = -1,
    [double]$ClickY = -1,
    [int]$ClickCount = 1,
    [double]$WheelX = -1,
    [double]$WheelY = -1,
    [int]$WheelNotches = 0,
    [int]$SettleMs = 1200
)

Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
[StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
[StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public MOUSEINPUT mi; }
public class Win32S {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
}
"@

[Win32S]::SetProcessDPIAware() | Out-Null
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

$p = Get-Process ThartEditor -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $p) { Write-Output "NO_PROCESS"; exit 1 }
$hwnd = $p.MainWindowHandle
if ($hwnd -eq [IntPtr]::Zero) { Write-Output "NO_WINDOW"; exit 1 }

[Win32S]::ShowWindow($hwnd, 9) | Out-Null
Start-Sleep -Milliseconds 400
[Win32S]::SetWindowPos($hwnd, [IntPtr]::new(-1), 0, 0, 0, 0, 0x0053) | Out-Null
[Win32S]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[Win32S]::keybd_event(0x12, 0, 0x0002, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 120
[Win32S]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 500

$fg = [Win32S]::GetForegroundWindow()
Write-Output "FOREGROUND=$fg HWND=$hwnd MATCH=$($fg -eq $hwnd)"

$cr = New-Object Win32S+RECT
[Win32S]::GetClientRect($hwnd, [ref]$cr) | Out-Null
$cw = $cr.Right - $cr.Left
$ch = $cr.Bottom - $cr.Top
$sw = [Win32S]::GetSystemMetrics(0)
$sh = [Win32S]::GetSystemMetrics(1)
Write-Output "CLIENT ${cw}x${ch} SCREEN ${sw}x${sh}"

$MOVE = 0x0001
$LDOWN = 0x0002
$LUP = 0x0004
$WHEEL = 0x0800
$ABSOLUTE = 0x8000
$VK_CONTROL = 0x11
$KEYUP = 0x0002

function Send-Mouse([int]$flag, [int]$data, [int]$absX, [int]$absY, [bool]$absolute) {
    $mi = New-Object MOUSEINPUT
    $mi.dx = $absX
    $mi.dy = $absY
    $mi.mouseData = [uint32]$data
    $mi.dwFlags = [uint32]($flag -bor $(if ($absolute) { $ABSOLUTE } else { 0 }))
    $mi.time = 0
    $mi.dwExtraInfo = [IntPtr]::Zero
    $inp = New-Object INPUT
    $inp.type = 0
    $inp.mi = $mi
    [Win32S]::SendInput(1, @($inp), [System.Runtime.InteropServices.Marshal]::SizeOf($inp)) | Out-Null
}

function Get-Abs([int]$sx, [int]$sy) {
    $ax = [int](($sx * 65535) / $sw)
    $ay = [int](($sy * 65535) / $sh)
    return @($ax, $ay)
}

function DoClick([double]$fx, [double]$fy, [int]$n) {
    $x = [int]($cw * $fx); $y = [int]($ch * $fy)
    $pt = New-Object Win32S+POINT
    $pt.X = $x; $pt.Y = $y
    [Win32S]::ClientToScreen($hwnd, [ref]$pt) | Out-Null
    $abs = Get-Abs $pt.X $pt.Y
    # move to target (absolute), then a tiny jiggle, then click
    Send-Mouse $MOVE 0 $abs[0] $abs[1] $true
    Start-Sleep -Milliseconds 250
    $abs2 = Get-Abs ($pt.X - 4) ($pt.Y - 4)
    Send-Mouse $MOVE 0 $abs2[0] $abs2[1] $true
    Start-Sleep -Milliseconds 150
    Send-Mouse $MOVE 0 $abs[0] $abs[1] $true
    Start-Sleep -Milliseconds 300
    $fg2 = [Win32S]::GetForegroundWindow()
    Write-Output "PRE-CLICK FG=$fg2 MATCH=$($fg2 -eq $hwnd)"
    for ($c = 0; $c -lt $n; $c++) {
        Send-Mouse $LDOWN 0 0 0 $false
        Start-Sleep -Milliseconds 90
        Send-Mouse $LUP 0 0 0 $false
        if ($c -lt $n - 1) { Start-Sleep -Milliseconds 110 }
    }
    Start-Sleep -Milliseconds 600
    Write-Output "SENDINPUT-CLICK x$n client=$x,$y screen=$($pt.X),$($pt.Y)"
}

function DoWheel([double]$fx, [double]$fy, [int]$notches) {
    $x = [int]($cw * $fx); $y = [int]($ch * $fy)
    $pt = New-Object Win32S+POINT
    $pt.X = $x; $pt.Y = $y
    [Win32S]::ClientToScreen($hwnd, [ref]$pt) | Out-Null
    $abs = Get-Abs $pt.X $pt.Y
    Send-Mouse $MOVE 0 $abs[0] $abs[1] $true
    Start-Sleep -Milliseconds 300
    [Win32S]::keybd_event($VK_CONTROL, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 200
    $delta = if ($notches -gt 0) { 120 } else { -120 }
    for ($i = 0; $i -lt [Math]::Abs($notches); $i++) {
        Send-Mouse $WHEEL $delta 0 0 $false
        Start-Sleep -Milliseconds 300
    }
    [Win32S]::keybd_event($VK_CONTROL, 0, $KEYUP, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 600
    Write-Output "SENDINPUT-WHEEL $notches @ $($pt.X),$($pt.Y)"
}

function DoShot([string]$name) {
    $bmp = New-Object System.Drawing.Bitmap $cw, $ch
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    $ok = [Win32S]::PrintWindow($hwnd, $hdc, 2)
    $g.ReleaseHdc($hdc)
    $g.Dispose()
    $path = Join-Path $OutDir ($name + ".png")
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "SAVED $path ok=$ok"
}

if ($ClickX -ge 0 -and $ClickY -ge 0) { DoClick $ClickX $ClickY $ClickCount }
if ($WheelNotches -ne 0 -and $WheelX -ge 0 -and $WheelY -ge 0) { DoWheel $WheelX $WheelY $WheelNotches }

Start-Sleep -Milliseconds $SettleMs
if ($Shot -ne "") { DoShot $Shot }
Write-Output "OK"