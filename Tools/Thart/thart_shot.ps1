param(
    [string]$Exe = "d:\MyDataInD\Unity\RhythmGame\Builds\ThartEditor\ThartEditor.exe",
    [string]$OutDir = "d:\MyDataInD\Unity\RhythmGame\Builds\verify",
    [int]$WaitMs = 20000,
    [int]$SettleMs = 8000
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32S {
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

[Win32S]::SetProcessDPIAware() | Out-Null
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

Get-Process ThartEditor -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 1000

$p = Start-Process -FilePath $Exe -PassThru
$hwnd = [IntPtr]::Zero
$deadline = (Get-Date).AddMilliseconds($WaitMs)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 400
    $p.Refresh()
    if ($p.HasExited) { Write-Output "EXITED"; exit 1 }
    if ($p.MainWindowHandle -ne [IntPtr]::Zero) { $hwnd = $p.MainWindowHandle; break }
}
if ($hwnd -eq [IntPtr]::Zero) { Write-Output "NO_WINDOW"; exit 1 }

# 强制还原并放到 (40,40)，尺寸 1600x900
[Win32S]::ShowWindow($hwnd, 9) | Out-Null
Start-Sleep -Milliseconds 600
[Win32S]::SetWindowPos($hwnd, [IntPtr]::Zero, 40, 40, 1600, 900, 0x0040) | Out-Null
Start-Sleep -Milliseconds 600
[Win32S]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds $SettleMs

$wr = New-Object Win32S+RECT
[Win32S]::GetWindowRect($hwnd, [ref]$wr) | Out-Null
Write-Output "WINDOW $($wr.Right - $wr.Left)x$($wr.Bottom - $wr.Top) at $($wr.Left),$($wr.Top)"

$origin = New-Object Win32S+POINT
[Win32S]::ClientToScreen($hwnd, [ref]$origin) | Out-Null
$cr = New-Object Win32S+RECT
[Win32S]::GetClientRect($hwnd, [ref]$cr) | Out-Null
$cw = $cr.Right - $cr.Left
$ch = $cr.Bottom - $cr.Top
Write-Output "CLIENT ${cw}x${ch} ORIGIN $($origin.X),$($origin.Y)"

$bmp = New-Object System.Drawing.Bitmap $cw, $ch
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($origin.X, $origin.Y, 0, 0, $bmp.Size)
$path = Join-Path $OutDir "00-layout.png"
$bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output "SAVED $path"