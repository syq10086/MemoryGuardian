# Rebuild the original texture and icon from public-domain Natural Earth land geometry.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assets = Join-Path (Split-Path -Parent $PSScriptRoot) 'orb\Assets'
$map = Get-Content -LiteralPath (Join-Path $assets 'natural-earth-land.geojson') -Raw -Encoding UTF8 | ConvertFrom-Json
$bitmap = New-Object Drawing.Bitmap 1024,512
$g = [Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode = 'AntiAlias'
$g.Clear([Drawing.Color]::FromArgb(15,79,147))
$land = New-Object Drawing.Drawing2D.LinearGradientBrush((New-Object Drawing.Rectangle 0,0,1024,512),[Drawing.Color]::FromArgb(138,192,140),[Drawing.Color]::FromArgb(49,127,92),90)
$coast = New-Object Drawing.Pen([Drawing.Color]::FromArgb(175,209,158),0.7)
foreach ($feature in $map.features) {
    $polygons = New-Object 'System.Collections.Generic.List[object]'
    if ($feature.geometry.type -eq 'MultiPolygon') {
        foreach ($polygonCoordinates in $feature.geometry.coordinates) { $polygons.Add($polygonCoordinates) }
    } else { $polygons.Add($feature.geometry.coordinates) }
    foreach ($polygon in $polygons) {
        $path = New-Object Drawing.Drawing2D.GraphicsPath
        foreach ($ring in $polygon) {
            $points = New-Object 'System.Collections.Generic.List[Drawing.PointF]'
            foreach ($coord in $ring) { $points.Add([Drawing.PointF]::new([single](($coord[0]+180)/360*1024),[single]((90-$coord[1])/180*512))) }
            if ($points.Count -gt 2) { $path.AddPolygon($points.ToArray()) }
        }
        $g.FillPath($land,$path); $g.DrawPath($coast,$path); $path.Dispose()
    }
}
$bitmap.Save((Join-Path $assets 'earth-map.png'),[Drawing.Imaging.ImageFormat]::Png)
# Orthographically project the same map to create a matching spherical application icon.
$iconBitmap = New-Object Drawing.Bitmap 128,128
$centerLongitude = 100 * [Math]::PI / 180
for ($y=0; $y -lt 128; $y++) {
    for ($x=0; $x -lt 128; $x++) {
        $nx = ($x-63.5)/59; $ny = (63.5-$y)/59; $r2 = $nx*$nx+$ny*$ny
        if ($r2 -ge 1) { continue }
        $nz = [Math]::Sqrt(1-$r2)
        $lon = [Math]::Atan2($nx,$nz)+$centerLongitude
        $lat = [Math]::Asin($ny)
        $tx = (([int](($lon+[Math]::PI)/(2*[Math]::PI)*1024))%1024+1024)%1024
        $ty = [Math]::Min(511,[Math]::Max(0,[int]((0.5-$lat/[Math]::PI)*512)))
        $color = $bitmap.GetPixel($tx,$ty)
        $light = [Math]::Min(1.25,0.28+[Math]::Max(0,-0.45*$nx+0.5*$ny+0.74*$nz))
        $rim = [Math]::Pow(1-$nz,4)*0.4
        $red = [int][Math]::Min(255,$color.R*$light+40*$rim)
        $green = [int][Math]::Min(255,$color.G*$light+155*$rim)
        $blue = [int][Math]::Min(255,$color.B*$light+255*$rim)
        $iconBitmap.SetPixel($x,$y,[Drawing.Color]::FromArgb(255,$red,$green,$blue))
    }
}
$buffer = New-Object IO.MemoryStream
$iconBitmap.Save($buffer,[Drawing.Imaging.ImageFormat]::Png)
$data = $buffer.ToArray()
$writer = New-Object IO.BinaryWriter([IO.File]::Create((Join-Path $assets 'EarthGuardian.ico')))
$writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]1)
$writer.Write([byte]128); $writer.Write([byte]128); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([UInt16]1); $writer.Write([UInt16]32); $writer.Write([UInt32]$data.Length); $writer.Write([UInt32]22); $writer.Write($data)
$writer.Dispose(); $buffer.Dispose(); $iconBitmap.Dispose(); $g.Dispose(); $bitmap.Dispose(); $land.Dispose(); $coast.Dispose()
