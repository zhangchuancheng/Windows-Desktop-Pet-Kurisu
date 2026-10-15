$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'Windows .NET Framework C# compiler was not found.'
}

$spriteDir = Join-Path $projectRoot 'assets\sprites'
foreach ($state in @('idle', 'blink', 'poke-block', 'poke-fold', 'poke-cheek', 'rapid', 'drag', 'sleep', 'wake')) {
    if (-not (Test-Path -LiteralPath (Join-Path $spriteDir ($state + '.png')))) {
        throw "Missing sprite: $state.png"
    }
}

$dist = Join-Path $projectRoot 'dist'
$layerDir = Join-Path $projectRoot 'assets\layers\idle-v1'
$layerFiles = @('layers.json', 'body.png', 'head-base.png', 'hair-back-left.png', 'hair-back-right.png', 'eyes-open.png', 'eyes-half.png', 'eyes-closed.png')
foreach ($file in $layerFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $layerDir $file))) { throw "Missing idle layer: $file" }
}
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$sources = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName
$output = Join-Path $dist 'MakiseDesktopPet.exe'
$manifest = Join-Path $projectRoot 'app.manifest'
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /out:$output `
    "/win32manifest:$manifest" `
    /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll @sources
if ($LASTEXITCODE -ne 0) { throw 'Desktop pet compilation failed.' }

Copy-Item -LiteralPath (Join-Path $projectRoot 'dialogues.json') -Destination $dist -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'App.config') -Destination (Join-Path $dist 'MakiseDesktopPet.exe.config') -Force
$distSprites = Join-Path $dist 'assets\sprites'
New-Item -ItemType Directory -Force -Path $distSprites | Out-Null
Copy-Item -Path (Join-Path $spriteDir '*.png') -Destination $distSprites -Force
Copy-Item -LiteralPath (Join-Path $spriteDir 'click-colors.json') -Destination $distSprites -Force
$distLayers = Join-Path $dist 'assets\layers\idle-v1'
New-Item -ItemType Directory -Force -Path $distLayers | Out-Null
foreach ($file in $layerFiles) {
    Copy-Item -LiteralPath (Join-Path $layerDir $file) -Destination $distLayers -Force
}
Write-Host "Built: $output"
