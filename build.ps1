# Compila OBSFS-AutoMount.exe usando el compilador de C# que ya viene con Windows
# (.NET Framework 4.x). No requiere instalar Visual Studio, el SDK de .NET ni NuGet.
#
#   powershell -ExecutionPolicy Bypass -File build.ps1
#
param([switch]$Run)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$src  = Join-Path $root 'src'
$dist = Join-Path $root 'dist'
$exe  = Join-Path $dist 'OBSFS-AutoMount.exe'
$ico  = Join-Path $root 'assets\app.ico'
$man  = Join-Path $root 'assets\app.manifest'

# --- compilador ------------------------------------------------------------
$csc = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $csc) {
    throw "No se encontro csc.exe (.NET Framework 4.x). Instalar .NET Framework 4.8 y reintentar."
}
Write-Host "Compilador : $csc"

# --- icono -----------------------------------------------------------------
if (-not (Test-Path $ico)) { & (Join-Path $root 'tools\make-icon.ps1') }

# --- salida ----------------------------------------------------------------
New-Item -ItemType Directory -Force -Path $dist | Out-Null
if (Test-Path $exe) { Remove-Item $exe -Force }

$refs = @(
    'System.dll',
    'System.Core.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll',
    'System.Security.dll',
    'System.IO.Compression.dll',
    'System.IO.Compression.FileSystem.dll'
)

$sources = Get-ChildItem -Path $src -Filter *.cs | ForEach-Object { $_.FullName }
Write-Host ("Fuentes    : {0} archivos" -f $sources.Count)

$cscArgs = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/langversion:5',
    '/warn:3',
    "/out:$exe",
    "/win32icon:$ico",
    "/win32manifest:$man"
)
foreach ($r in $refs) { $cscArgs += "/reference:$r" }
$cscArgs += $sources

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw "La compilacion fallo (codigo $LASTEXITCODE)." }

$size = [Math]::Round((Get-Item $exe).Length / 1KB, 1)
Write-Host ""
Write-Host ("OK -> {0}  ({1} KB)" -f $exe, $size) -ForegroundColor Green
Write-Host "Es un unico archivo portable: se puede copiar a cualquier Windows 10/11 y hacer doble clic."

if ($Run) { Start-Process $exe }
