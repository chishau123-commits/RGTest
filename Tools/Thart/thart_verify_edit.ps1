param(
    [string]$Exe = "d:\MyDataInD\Unity\RhythmGame\Builds\ThartEditor\ThartEditor.exe",
    [string]$OutDir = "d:\MyDataInD\Unity\RhythmGame\Builds\edit-verify",
    [int]$WaitMs = 15000,
    [int]$SettleMs = 6000,
    [string]$ClickPoints = "0.279,0.286,0.293,0.300,0.307",
    [double]$ClickY = 0.806
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32E {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
}
"@

$LDOWN = 0x0002
$LUP = 0x0004
$RDOWN = 0x0008
$RUP = 0x0010
$VK_SHIFT = 0x10
$KEYUP = 0x0002

[Win32E]::SetProcessDPIAware() | Out-Null

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

[Win32E]::ShowWindow($hwnd, 9) | Out-Null
[Win32E]::SetWindowPos($hwnd, [IntPtr]::new(-1), 0, 0, 0, 0, 0x13) | Out-Null
[Win32E]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds $SettleMs

$origin = [Win32E+POINT]::new()
[Win32E]::ClientToScreen($hwnd, [ref]$origin) | Out-Null
$cr = [Win32E+RECT]::new()
[Win32E]::GetClientRect($hwnd, [ref]$cr) | Out-Null
$cw = $cr.Right - $cr.Left
$ch = $cr.Bottom - $cr.Top
Write-Output "CLIENT ${cw}x${ch} ORIGIN $($origin.X),$($origin.Y)"

function Get-ScreenPoint([double]$fx, [double]$fy) {
    $pt = [Win32E+POINT]::new()
    $pt.X = $origin.X + [int]($cw * $fx)
    $pt.Y = $origin.Y + [int]($ch * $fy)
    return $pt
}

function Invoke-Click([double]$fx, [double]$fy, [bool]$right) {
    $pt = Get-ScreenPoint $fx $fy
    [Win32E]::SetCursorPos($pt.X, $pt.Y) | Out-Null
    Start-Sleep -Milliseconds 160
    if ($right) {
        [Win32E]::mouse_event($RDOWN, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 60
        [Win32E]::mouse_event($RUP, 0, 0, 0, [UIntPtr]::Zero)
    } else {
        [Win32E]::mouse_event($LDOWN, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 60
        [Win32E]::mouse_event($LUP, 0, 0, 0, [UIntPtr]::Zero)
    }
    Start-Sleep -Milliseconds 320
    Write-Output ("CLICK {0} {1},{2}" -f $(if ($right) { "R" } else { "L" }), $pt.X, $pt.Y)
}

function Save-Shot([string]$name) {
    $r = [Win32E+RECT]::new()
    [Win32E]::GetWindowRect($hwnd, [ref]$r) | Out-Null
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

# warm up: the first clicks after launch are sometimes swallowed while the
# window finishes taking focus, so click a harmless spot (bottom status bar) first
Invoke-Click 0.5 0.975 $false

# 1) add notes with left clicks in the P1 row
foreach ($fxStr in $ClickPoints.Split(",")) {
    Invoke-Click ([double]$fxStr) $ClickY $false
}
Save-Shot "01-added"

# 2) shift+click a second note adds it to the selection
[Win32E]::keybd_event($VK_SHIFT, 0, 0, [UIntPtr]::Zero)
Invoke-Click ([double]$ClickPoints.Split(",")[3]) $ClickY $false
[Win32E]::keybd_event($VK_SHIFT, 0, $KEYUP, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 600
Save-Shot "02-shift-multi"

# 3) Ctrl+A select all
[System.Windows.Forms.SendKeys]::SendWait("^a")
Start-Sleep -Milliseconds 900
Save-Shot "03-select-all"

# 4) right click on the middle note deletes it
Invoke-Click ([double]$ClickPoints.Split(",")[2]) $ClickY $true
Save-Shot "04-after-right-delete"

# 5) Esc clears selection
[System.Windows.Forms.SendKeys]::SendWait("{ESC}")
Start-Sleep -Milliseconds 900
Save-Shot "05-esc-cleared"