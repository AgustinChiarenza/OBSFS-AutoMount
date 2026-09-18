# Captura la ventana principal de la app (solo esa ventana) para revisar la UI.
param([string]$Out = "$env:TEMP\obsfs-shot.png", [int]$WaitMs = 3500)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
"@

$exe = Join-Path (Split-Path -Parent $PSScriptRoot) 'dist\OBSFS-AutoMount.exe'
$p = Start-Process $exe -PassThru
Start-Sleep -Milliseconds $WaitMs
$p.Refresh()

if ($p.MainWindowHandle -eq 0) { $p.Kill(); throw "La ventana principal no se abrio." }
[void][Win]::SetForegroundWindow($p.MainWindowHandle)
Start-Sleep -Milliseconds 700

$r = New-Object Win+RECT
[void][Win]::GetWindowRect($p.MainWindowHandle, [ref]$r)
$w = $r.R - $r.L; $h = $r.B - $r.T

$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
[void][Win]::PrintWindow($p.MainWindowHandle, $hdc, 2)   # PW_RENDERFULLCONTENT
$g.ReleaseHdc($hdc)
$g.Dispose()
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

Write-Host ("Captura: {0}  ({1}x{2})" -f $Out, $w, $h)
Write-Host ("PID {0} sigue abierto" -f $p.Id)
