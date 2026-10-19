param(
    [Parameter(Mandatory = $true)] [string]$OutfitDirectory,
    [Parameter(Mandatory = $true)] [int]$X,
    [Parameter(Mandatory = $true)] [int]$Y,
    [Parameter(Mandatory = $true)] [int]$Width,
    [Parameter(Mandatory = $true)] [int]$Height
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$folder = (Resolve-Path -LiteralPath $OutfitDirectory).Path
$sourcePath = Join-Path $folder 'source/idle.png'
$outputPath = Join-Path $folder 'idle.png'
if (-not (Test-Path -LiteralPath $sourcePath)) { throw "Missing idle source: $sourcePath" }

$inputImage = [System.Drawing.Bitmap]::new($sourcePath)
$outputImage = $null
$graphics = $null
try {
    $outputImage = [System.Drawing.Bitmap]::new(
        1024, 1402, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
    )
    $graphics = [System.Drawing.Graphics]::FromImage($outputImage)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.DrawImage($inputImage, [System.Drawing.Rectangle]::new($X, $Y, $Width, $Height))
    $temporaryPath = "$outputPath.tmp.png"
    try {
        $outputImage.Save($temporaryPath, [System.Drawing.Imaging.ImageFormat]::Png)
        Move-Item -LiteralPath $temporaryPath -Destination $outputPath -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath }
    }
}
finally {
    if ($graphics) { $graphics.Dispose() }
    if ($outputImage) { $outputImage.Dispose() }
    $inputImage.Dispose()
}

Write-Output "Aligned idle sprite in $folder"
