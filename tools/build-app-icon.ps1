$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'assets\icons\app.png'
$output = Join-Path $root 'assets\icons\app.ico'
$sizes = @(16, 20, 24, 32, 48, 64, 128, 256)
$payloads = [System.Collections.Generic.List[byte[]]]::new()
$image = [System.Drawing.Bitmap]::FromFile($source)
try {
    if ($image.Width -ne $image.Height) { throw 'Icon master must use a square canvas.' }
    foreach ($size in $sizes) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $memory = [System.IO.MemoryStream]::new()
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($image, [System.Drawing.Rectangle]::new(0, 0, $size, $size), 0, 0, $image.Width, $image.Height, [System.Drawing.GraphicsUnit]::Pixel)
            $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
            $payloads.Add($memory.ToArray())
        }
        finally { $memory.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
}
finally { $image.Dispose() }
$stream = [System.IO.File]::Create($output)
$writer = [System.IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$payloads[$i].Length)
        $writer.Write([uint32]$offset)
        $offset += $payloads[$i].Length
    }
    foreach ($payload in $payloads) { $writer.Write([byte[]]$payload) }
}
finally { $writer.Dispose(); $stream.Dispose() }
Write-Host "Created: $output"
