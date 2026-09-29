param(
    [string]$OutDir = "d:\MyDataInD\Unity\RhythmGame\Builds\verify",
    [string]$Shot = "",
    [double]$ClientX = -1,
    [double]$ClientY = -1,
    [int]$ClickCount = 0,
    [int]$WheelNotches = 0,
    [int]$KeyVk = 0,
    [switch]$WithCtrl,
    [int]$SettleMs = 900,
    [switch]$ShotOnly
)

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class ThartUi {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out R r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref P p);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, int d, UIntPtr e);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte sc, uint f, UIntPtr e);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(P p);
    [StructLayout(LayoutKind.Sequential)] public struct R { public int L, T, Rt, B; }
    [StructLayout(LayoutKind.Sequential)] public struct P { public int X, Y; }
}
"@

[ThartUi]::SetProcessDPIAware() | Out-Null
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

$p = Get-Process ThartEditor -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $p) { Write-Output "NO_PROCESS"; exit 1 }
$hwnd = $p.MainWindowHandle
if ($hwnd -eq [IntPtr]::Zero) { Write-Output "NO_WINDOW"; exit 1 }

[ThartUi]::ShowWindow($hwnd, 9) | Out-Null
Start-Sleep -Milliseconds 400
[ThartUi]::SetWindowPos($hwnd, [IntPtr]::new(-1), 0, 0, 0, 0, 0x0053) | Out-Null
[ThartUi]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 500

$cr = New-Object ThartUi+R
[ThartUi]::GetClientRect($hwnd, [ref]$cr) | Out-Null
$cw = $cr.Rt - $cr.L
$ch = $cr.B - $cr.T
$origin = New-Object ThartUi+P
$origin.X = 0; $origin.Y = 0
[ThartUi]::ClientToScreen($hwnd, [ref]$origin) | Out-Null
Write-Output "HWND=$hwnd CLIENT=${cw}x${ch} ORIGIN=$($origin.X),$($origin.Y)"
Write-Output "FG=$([ThartUi]::GetForegroundWindow()) match=$([ThartUi]::GetForegroundWindow() -eq $hwnd)"

$LDOWN = 0x0002; $LUP = 0x0004; $WHEEL = 0x0800; $KEYUP = 0x0002

function ToScreen([double]$x, [double]$y) {
    $pt = New-Object ThartUi+P
    $pt.X = [int]$x; $pt.Y = [int]$y
    [ThartUi]::ClientToScreen($hwnd, [ref]$pt) | Out-Null
    return $pt
}

if ($KeyVk -gt 0) {
    [ThartUi]::keybd_event([byte]$KeyVk, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [ThartUi]::keybd_event([byte]$KeyVk, 0, $KEYUP, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 400
    Write-Output "KEY vk=$KeyVk"
}

if ($ClientX -ge 0 -and $ClientY -ge 0) {
    $pt = ToScreen $ClientX $ClientY
    [ThartUi]::SetCursorPos($pt.X, $pt.Y) | Out-Null
    Start-Sleep -Milliseconds 250
    $hit = [ThartUi]::WindowFromPoint($pt)
    Write-Output "TARGET client=$ClientX,$ClientY screen=$($pt.X),$($pt.Y) windowAtPoint=$hit"

    if ($WheelNotches -ne 0) {
        if ($WithCtrl) { [ThartUi]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 150 }
        $delta = if ($WheelNotches -gt 0) { 120 } else { -120 }
        for ($i = 0; $i -lt [Math]::Abs($WheelNotches); $i++) {
            [ThartUi]::mouse_event($WHEEL, 0, 0, $delta, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds 200
        }
        if ($WithCtrl) { [ThartUi]::keybd_event(0x11, 0, $KEYUP, [UIntPtr]::Zero); Start-Sleep -Milliseconds 120 }
        Write-Output "WHEEL $WheelNotches ctrl=$WithCtrl"
    }

    if ($ClickCount -gt 0) {
        for ($c = 0; $c -lt $ClickCount; $c++) {
            [ThartUi]::mouse_event($LDOWN, 0, 0, 0, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds 60
            [ThartUi]::mouse_event($LUP, 0, 0, 0, [UIntPtr]::Zero)
            if ($c -lt $ClickCount - 1) { Start-Sleep -Milliseconds 80 }
        }
        Write-Output "CLICK x$ClickCount"
    }
}

Start-Sleep -Milliseconds $SettleMs

if ($Shot -ne "") {
    $bmp = New-Object System.Drawing.Bitmap $cw, $ch
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($origin.X, $origin.Y, 0, 0, (New-Object System.Drawing.Size $cw, $ch))
    $g.Dispose()
    $path = Join-Path $OutDir ($Shot + ".png")
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "SAVED $path"
}

Write-Output "OK"