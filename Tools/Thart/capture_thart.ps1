param(
    [string]$Exe = "d:\MyDataInD\Unity\RhythmGame\Builds\ThartEditor\ThartEditor.exe",
    [string]$Out = "d:\MyDataInD\Unity\RhythmGame\Builds\thart-preview.png",
    [int]$WaitMs = 15000,
    [int]$SettleMs = 6000,
    [string]$Keys = "3"
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
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
"@

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

if ($Keys -ne "" -and $Keys -ne "none") {
    foreach ($k in $Keys.ToCharArray()) {
        [System.Windows.Forms.SendKeys]::SendWait([string]$k)
        Start-Sleep -Milliseconds 700
    }
}

$r = [Win32+RECT]::new()
[Win32]::GetWindowRect($hwnd, [ref]$r) | Out-Null
$w = $r.Right - $r.Left
$h = $r.Bottom - $r.Top
if ($w -le 0 -or $h -le 0) { Write-Output "BAD_RECT"; exit 1 }

$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output "SAVED $Out ${w}x${h}"