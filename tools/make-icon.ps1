# Genera assets/app.ico (nube sobre disco, rojo Huawei) sin herramientas externas.
# Se ejecuta solo cuando el .ico no existe o se pasa -Force.
param([switch]$Force)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$icoPath = Join-Path $root 'assets\app.ico'
if ((Test-Path $icoPath) -and -not $Force) { Write-Host "app.ico ya existe (usar -Force para regenerar)"; return }

function New-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-Frame([int]$S) {
    $bmp = New-Object System.Drawing.Bitmap($S, $S, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    # Fondo: cuadrado redondeado con degradado rojo Huawei
    $pad = [float]($S * 0.04)
    $side = [float]($S - 2 * $pad)
    $bg = New-RoundedPath $pad $pad $side $side ([float]($S * 0.21))
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF($pad, $pad)),
        (New-Object System.Drawing.PointF(($pad + $side), ($pad + $side))),
        [System.Drawing.Color]::FromArgb(255, 226, 28, 62),
        [System.Drawing.Color]::FromArgb(255, 142, 6, 29))
    $g.FillPath($brush, $bg)

    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)

    # Nube (FillMode Winding: sin esto los circulos superpuestos se recortan entre si)
    $cloud = New-Object System.Drawing.Drawing2D.GraphicsPath
    $cloud.FillMode = [System.Drawing.Drawing2D.FillMode]::Winding
    $cloud.AddEllipse([float]($S * 0.30), [float]($S * 0.235), [float]($S * 0.28), [float]($S * 0.28))
    $cloud.AddEllipse([float]($S * 0.18), [float]($S * 0.325), [float]($S * 0.20), [float]($S * 0.20))
    $cloud.AddEllipse([float]($S * 0.52), [float]($S * 0.305), [float]($S * 0.23), [float]($S * 0.23))
    $cloud.AddRectangle((New-Object System.Drawing.RectangleF([float]($S * 0.28), [float]($S * 0.40), [float]($S * 0.47), [float]($S * 0.135))))
    $g.FillPath($white, $cloud)

    # Disco / unidad
    $disk = New-RoundedPath ([float]($S * 0.20)) ([float]($S * 0.645)) ([float]($S * 0.60)) ([float]($S * 0.165)) ([float]($S * 0.045))
    $g.FillPath($white, $disk)

    # Luz del disco
    $dot = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 200, 20, 48))
    $g.FillEllipse($dot, [float]($S * 0.68), [float]($S * 0.70), [float]($S * 0.055), [float]($S * 0.055))
    if ($S -ge 48) {
        $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 200, 20, 48), [float]([Math]::Max(1, $S * 0.018)))
        $g.DrawLine($pen, [float]($S * 0.26), [float]($S * 0.728), [float]($S * 0.52), [float]($S * 0.728))
        $pen.Dispose()
    }

    $dot.Dispose(); $white.Dispose(); $brush.Dispose(); $bg.Dispose(); $cloud.Dispose(); $disk.Dispose()
    $g.Dispose()
    return $bmp
}

# DIB (BITMAPINFOHEADER + BGRA bottom-up + mascara AND) para los tamanos chicos:
# System.Drawing.Icon.ToBitmap() no sabe leer entradas PNG, y la app usa esa API.
function Get-DibBytes([System.Drawing.Bitmap]$bmp, [int]$S) {
    $rect = New-Object System.Drawing.Rectangle(0, 0, $S, $S)
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                          [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $src = New-Object byte[] ($data.Stride * $S)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $src, 0, $src.Length)
    $stride = $data.Stride
    $bmp.UnlockBits($data)

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    $bw.Write([UInt32]40)            # biSize
    $bw.Write([Int32]$S)             # biWidth
    $bw.Write([Int32]($S * 2))       # biHeight = XOR + AND
    $bw.Write([UInt16]1)             # biPlanes
    $bw.Write([UInt16]32)            # biBitCount
    $bw.Write([UInt32]0)             # biCompression = BI_RGB
    $bw.Write([UInt32]($S * $S * 4)) # biSizeImage
    $bw.Write([Int32]0); $bw.Write([Int32]0)
    $bw.Write([UInt32]0); $bw.Write([UInt32]0)

    for ($y = $S - 1; $y -ge 0; $y--) { $bw.Write($src, $y * $stride, $S * 4) }   # filas invertidas

    $maskRow = [Math]::Floor(($S + 31) / 32) * 4                                   # mascara AND vacia
    $bw.Write((New-Object byte[] ($maskRow * $S)))
    $bw.Flush()
    $result = $ms.ToArray()
    $bw.Dispose(); $ms.Dispose()
    return , $result
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = @()
foreach ($s in $sizes) {
    $bmp = New-Frame $s
    if ($s -ge 256) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $frames += , @{ Size = $s; Bytes = $ms.ToArray() }
        $ms.Dispose()
    }
    else {
        $frames += , @{ Size = $s; Bytes = (Get-DibBytes $bmp $s) }
    }
    $bmp.Dispose()
}

# Ensamblado del contenedor .ico
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([UInt16]0)                 # reservado
$bw.Write([UInt16]1)                 # tipo: icono
$bw.Write([UInt16]$frames.Count)

$offset = 6 + (16 * $frames.Count)
foreach ($p in $frames) {
    $dim = if ($p.Size -ge 256) { 0 } else { $p.Size }
    $bw.Write([Byte]$dim)            # ancho
    $bw.Write([Byte]$dim)            # alto
    $bw.Write([Byte]0)               # colores de paleta
    $bw.Write([Byte]0)               # reservado
    $bw.Write([UInt16]1)             # planos
    $bw.Write([UInt16]32)            # bits por pixel
    $bw.Write([UInt32]$p.Bytes.Length)
    $bw.Write([UInt32]$offset)
    $offset += $p.Bytes.Length
}
foreach ($p in $frames) { $bw.Write($p.Bytes) }
$bw.Flush()
$pngs = $frames

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $icoPath) | Out-Null
[System.IO.File]::WriteAllBytes($icoPath, $out.ToArray())
$bw.Dispose(); $out.Dispose()

Write-Host ("app.ico generado: {0} ({1} bytes, {2} tamanos)" -f $icoPath, (Get-Item $icoPath).Length, $pngs.Count)
