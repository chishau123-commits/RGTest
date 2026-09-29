param(
    [string]$Exe = "d:\MyDataInD\Unity\RhythmGame\Builds\ThartEditor\ThartEditor.exe",
    [string]$AppArgs = "-thartRecording d:\MyDataInD\Unity\RhythmGame\Builds\verify\thart-corners.json",
    [string]$Out = "d:\MyDataInD\Unity\RhythmGame\Builds\verify\thart-corners.png",
    [int]$WaitMs = 20000,
    [int]$SettleMs = 6000,
    [int]$WindowW = 1600,
    [int]$WindowH = 900,
    [string]$Keys = "",
    [int]$Vk = 0
)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class ThartShot {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
"@

[ThartShot]::SetProcessDPIAware() | Out-Null
Get-Process ThartEditor -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 900

$p = Start-Process -FilePath $Exe -ArgumentList $AppArgs -PassThru
$hwnd = [IntPtr]::Zero
$deadline = (Get-Date).AddMilliseconds($WaitMs)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 300
    $p.Refresh()
    if ($p.HasExited) { Write-Output "EXITED"; exit 1 }
    if ($p.MainWindowHandle -ne [IntPtr]::Zero) { $hwnd = $p.MainWindowHandle; break }
}
if ($hwnd -eq [IntPtr]::Zero) { Write-Output "NO_WINDOW"; exit 1 }

[ThartShot]::ShowWindow($hwnd, 9) | Out-Null
Start-Sleep -Milliseconds 500
[ThartShot]::SetWindowPos($hwnd, [IntPtr]::Zero, 30, 30, $WindowW, $WindowH, 0x0040) | Out-Null
Start-Sleep -Milliseconds 500
[ThartShot]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 1200

# Optional keystrokes (e.g. "3" switches to the 3D preview mode).
if ($Keys -ne "") {
    foreach ($k in $Keys.ToCharArray()) {
        [System.Windows.Forms.SendKeys]::SendWait([string]$k)
        Start-Sleep -Milliseconds 800
    }
}
# A raw virtual key avoids the IME/emoji panel that SendKeys can open.
if ($Vk -gt 0) {
    [ThartShot]::SetForegroundWindow($hwnd) | Out-Null
    Start-Sleep -Milliseconds 400
    [ThartShot]::keybd_event([byte]$Vk, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    [ThartShot]::keybd_event([byte]$Vk, 0, 2, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 1200
}

Start-Sleep -Milliseconds $SettleMs

$r = New-Object ThartShot+RECT
[ThartShot]::GetWindowRect($hwnd, [ref]$r) | Out-Null
$w = $r.Right - $r.Left
$h = $r.Bottom - $r.Top

$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output "SAVED $Out ${w}x${h}"
