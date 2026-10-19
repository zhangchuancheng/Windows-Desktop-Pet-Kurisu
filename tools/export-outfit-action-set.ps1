param(
    [Parameter(Mandatory = $true)]
    [string]$OutfitDirectory
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$required = @(
    'idle.png', 'blink-half.png', 'blink.png',
    'poke-block.png', 'poke-fold.png', 'poke-cheek.png',
    'rapid.png', 'sleep.png', 'wake.png', 'drag-base.png', 'drag.png'
)

$folder = (Resolve-Path -LiteralPath $OutfitDirectory).Path
$sourceFolder = Join-Path $folder 'source'
New-Item -ItemType Directory -Force -Path $sourceFolder | Out-Null

foreach ($name in $required) {
    $outputPath = Join-Path $folder $name
    $sourcePath = Join-Path $sourceFolder $name

    if (-not (Test-Path -LiteralPath $sourcePath)) {
        if (-not (Test-Path -LiteralPath $outputPath)) {
            throw "Missing sprite: $outputPath"
        }
        Move-Item -LiteralPath $outputPath -Destination $sourcePath
    }

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
        $graphics.DrawImage($inputImage, [System.Drawing.Rectangle]::new(0, 0, 1024, 1402))

        $temporaryPath = "$outputPath.tmp.png"
        try {
            $outputImage.Save($temporaryPath, [System.Drawing.Imaging.ImageFormat]::Png)
            Move-Item -LiteralPath $temporaryPath -Destination $outputPath -Force
        }
        finally {
            if (Test-Path -LiteralPath $temporaryPath) {
                Remove-Item -LiteralPath $temporaryPath
            }
        }
    }
    finally {
        if ($graphics) { $graphics.Dispose() }
        if ($outputImage) { $outputImage.Dispose() }
        $inputImage.Dispose()
    }
}

Write-Output "Exported $($required.Count) transparent sprites at 1024x1402 to $folder"
