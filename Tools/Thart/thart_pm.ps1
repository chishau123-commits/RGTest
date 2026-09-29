param(
    [string]$OutDir = "d:\MyDataInD\Unity\RhythmGame\Builds\verify",
    [string]$Shot = "",
    [double]$ClickX = -1,
    [double]$ClickY = -1,
    [int]$ClickCount = 1,
    [double]$WheelX = -1,
    [double]$WheelY = -1,
    [int]$WheelNotches = 0,
    [int]$Key = 0,
    [int]$SettleMs = 1200
)

Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32M {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr PostMessage(IntPtr hWnd, uint msg, IntPtr wp, IntPtr lp);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
}
"@

[Win32M]::SetProcessDPIAware() | Out-Null
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

$p = Get-Process ThartEditor -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $p) { Write-Output "NO_PROCESS"; exit 1 }
$hwnd = $p.MainWindowHandle
if ($hwnd -eq [IntPtr]::Zero) { Write-Output "NO_WINDOW"; exit 1 }

[Win32M]::ShowWindow($hwnd, 9) | Out-Null
Start-Sleep -Milliseconds 400
[Win32M]::SetWindowPos($hwnd, [IntPtr]::new(-1), 0, 0, 0, 0, 0x0053) | Out-Null
[Win32M]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 400

$cr = New-Object Win32M+RECT
[Win32M]::GetClientRect($hwnd, [ref]$cr) | Out-Null
$cw = $cr.Right - $cr.Left
$ch = $cr.Bottom - $cr.Top
Write-Output "CLIENT ${cw}x${ch}"

$WM_MOUSEMOVE = 0x0200
$WM_LBUTTONDOWN = 0x0201
$WM_LBUTTONUP = 0x0202
$WM_MOUSEWHEEL = 0x020A
$WM_KEYDOWN = 0x0100
$WM_KEYUP = 0x0101

function GetLP([int]$x, [int]$y) {
    return [IntPtr]((($y -band 0xFFFF) -shl 16) -bor ($x -band 0xFFFF))
}

function DoClick([double]$fx, [double]$fy, [int]$n) {
    $x = [int]($cw * $fx); $y = [int]($ch * $fy)
    $pt = New-Object Win32M+POINT
    $pt.X = $x; $pt.Y = $y
    [Win32M]::ClientToScreen($hwnd, [ref]$pt) | Out-Null
    # move the real cursor so the engine's tracked mouse position matches the message
    [Win32M]::SetCursorPos($pt.X - 24, $pt.Y - 24) | Out-Null
    Start-Sleep -Milliseconds 150
    [Win32M]::SetCursorPos($pt.X, $pt.Y) | Out-Null
    Start-Sleep -Milliseconds 350
    $now = New-Object Win32M+POINT
    [Win32M]::GetCursorPos([ref]$now) | Out-Null
    $lp = GetLP $x $y
    [Win32M]::PostMessage($hwnd, $WM_MOUSEMOVE, [IntPtr]::Zero, $lp) | Out-Null
    Start-Sleep -Milliseconds 200
    for ($c = 0; $c -lt $n; $c++) {
        [Win32M]::PostMessage($hwnd, $WM_LBUTTONDOWN, [IntPtr]::new(1), $lp) | Out-Null
        Start-Sleep -Milliseconds 70
        [Win32M]::PostMessage($hwnd, $WM_LBUTTONUP, [IntPtr]::Zero, $lp) | Out-Null
        if ($c -lt $n - 1) { Start-Sleep -Milliseconds 90 }
    }
    Start-Sleep -Milliseconds 600
    Write-Output "PMCLICK x$n client=$x,$y cursor=$($now.X),$($now.Y) want=$($pt.X),$($pt.Y)"
}

function DoWheel([double]$fx, [double]$fy, [int]$notches) {
    $x = [int]($cw * $fx); $y = [int]($ch * $fy)
    $pt = New-Object Win32M+POINT
    $pt.X = $x; $pt.Y = $y
    [Win32M]::ClientToScreen($hwnd, [ref]$pt) | Out-Null
    $lpScreen = GetLP $pt.X $pt.Y
    [Win32M]::PostMessage($hwnd, $WM_MOUSEMOVE, [IntPtr]::Zero, (GetLP $x $y)) | Out-Null
    Start-Sleep -Milliseconds 150
    [Win32M]::PostMessage($hwnd, $WM_KEYDOWN, [IntPtr]::new(0x11), [IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 150
    $delta = if ($notches -gt 0) { 120 } else { -120 }
    $wp = [IntPtr]([int64]($delta -band 0xFFFF) -shl 16)
    for ($i = 0; $i -lt [Math]::Abs($notches); $i++) {
        [Win32M]::PostMessage($hwnd, $WM_MOUSEWHEEL, $wp, $lpScreen) | Out-Null
        Start-Sleep -Milliseconds 250
    }
    [Win32M]::PostMessage($hwnd, $WM_KEYUP, [IntPtr]::new(0x11), [IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 500
    Write-Output "PMWHEEL $notches client=$x,$y screen=$($pt.X),$($pt.Y)"
}

function DoKey([int]$vk) {
    [Win32M]::PostMessage($hwnd, $WM_KEYDOWN, [IntPtr]::new($vk), [IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 130
    [Win32M]::PostMessage($hwnd, $WM_KEYUP, [IntPtr]::new($vk), [IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 600
    Write-Output "PMKEY $vk"
}

function DoShot([string]$name) {
    $bmp = New-Object System.Drawing.Bitmap $cw, $ch
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    $ok = [Win32M]::PrintWindow($hwnd, $hdc, 2)
    $g.ReleaseHdc($hdc)
    $g.Dispose()
    $path = Join-Path $OutDir ($name + ".png")
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "SAVED $path ok=$ok"
}

if ($Key -gt 0) { DoKey $Key }
if ($ClickX -ge 0 -and $ClickY -ge 0) { DoClick $ClickX $ClickY $ClickCount }
if ($WheelNotches -ne 0 -and $WheelX -ge 0 -and $WheelY -ge 0) { DoWheel $WheelX $WheelY $WheelNotches }

Start-Sleep -Milliseconds $SettleMs
if ($Shot -ne "") { DoShot $Shot }
Write-Output "OK"