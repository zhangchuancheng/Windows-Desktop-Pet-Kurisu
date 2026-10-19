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
$spriteFiles = @('idle.png', 'blink-half.png', 'blink.png', 'poke-block.png', 'poke-fold.png',
    'poke-cheek.png', 'rapid.png', 'drag.png', 'sleep.png', 'wake.png', 'drag-base.png')
foreach ($spriteFile in $spriteFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $spriteDir $spriteFile))) {
        throw "Missing sprite: $spriteFile"
    }
}
$outfitSource = Join-Path $projectRoot 'assets\concepts\outfit-actions-2026-10-19'
$outfitIds = @('lab-coat', 'maid', 'yukata', 'christmas-v2', 'red-dress-handfix',
    'beachwear-v2', 'battle-suit')
foreach ($outfitId in $outfitIds) {
    $outfitDirectory = Join-Path $outfitSource $outfitId
    foreach ($spriteFile in $spriteFiles) {
        $outfitSprite = Join-Path $outfitDirectory $spriteFile
        if (-not (Test-Path -LiteralPath $outfitSprite -PathType Leaf)) {
            throw "Missing outfit sprite: $outfitId/$spriteFile"
        }
    }
}
$dist = Join-Path $projectRoot 'dist'
foreach ($file in @('drag-warp.json', 'idle-motion.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $spriteDir $file))) { throw "Missing motion asset: $file" }
}
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$sources = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName
$output = Join-Path $dist 'MakiseDesktopPet.exe'
$manifest = Join-Path $projectRoot 'app.manifest'
$appIcon = Join-Path $projectRoot 'assets\icons\app.ico'
$persona = Join-Path $projectRoot 'chat-persona.json'
if (-not (Test-Path -LiteralPath $appIcon)) { throw 'Missing application icon: assets/icons/app.ico' }
if (-not (Test-Path -LiteralPath $persona)) { throw 'Missing chat persona: chat-persona.json' }
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /out:$output `
    "/win32manifest:$manifest" `
    "/win32icon:$appIcon" `
    /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll `
    /reference:System.Net.Http.dll @sources
if ($LASTEXITCODE -ne 0) { throw 'Desktop pet compilation failed.' }

Copy-Item -LiteralPath (Join-Path $projectRoot 'dialogues.json') -Destination $dist -Force
Copy-Item -LiteralPath $persona -Destination $dist -Force
foreach ($chatSetupFile in @('Install-Chat-Model.cmd', 'setup-chat.ps1')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $chatSetupFile) -Destination $dist -Force
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'App.config') -Destination (Join-Path $dist 'MakiseDesktopPet.exe.config') -Force
$distIcons = Join-Path $dist 'assets\icons'
New-Item -ItemType Directory -Force -Path $distIcons | Out-Null
Copy-Item -LiteralPath $appIcon -Destination $distIcons -Force
$distSprites = Join-Path $dist 'assets\sprites'
New-Item -ItemType Directory -Force -Path $distSprites | Out-Null
foreach ($spriteFile in $spriteFiles) {
    Copy-Item -LiteralPath (Join-Path $spriteDir $spriteFile) -Destination $distSprites -Force
}
$distOutfits = Join-Path $dist 'assets\outfits'
foreach ($outfitId in $outfitIds) {
    $source = Join-Path $outfitSource $outfitId
    $destination = Join-Path $distOutfits $outfitId
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    foreach ($spriteFile in $spriteFiles) {
        Copy-Item -LiteralPath (Join-Path $source $spriteFile) -Destination $destination -Force
    }
}
$distWakeSequence = Join-Path $distSprites 'wake-sequence'
if (Test-Path -LiteralPath $distWakeSequence) {
    Get-ChildItem -LiteralPath $distWakeSequence -Filter 'frame-*.png' -File |
        ForEach-Object { Remove-Item -LiteralPath $_.FullName }
    if (-not (Get-ChildItem -LiteralPath $distWakeSequence -Force)) {
        Remove-Item -LiteralPath $distWakeSequence
    }
}
foreach ($legacyFile in @('drag-sheet.png', 'drag-animation.json', 'click-colors.json', 'drag-colors.json',
    'wake-stir.png', 'wake-start.png', 'wake-raise.png', 'wake-fold.png')) {
    $legacyPath = Join-Path $distSprites $legacyFile
    if (Test-Path -LiteralPath $legacyPath) { Remove-Item -LiteralPath $legacyPath }
}
foreach ($file in @('drag-warp.json', 'idle-motion.json')) {
    Copy-Item -LiteralPath (Join-Path $spriteDir $file) -Destination $distSprites -Force
}
$staleLayers = Join-Path $dist 'assets\layers\idle-v1'
$resolvedDist = [IO.Path]::GetFullPath($dist).TrimEnd([IO.Path]::DirectorySeparatorChar)
$resolvedStale = [IO.Path]::GetFullPath($staleLayers)
if (-not $resolvedStale.StartsWith($resolvedDist + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to remove layers outside dist.'
}
if (Test-Path -LiteralPath $staleLayers) { Remove-Item -LiteralPath $staleLayers -Recurse -Force }
Write-Host "Built: $output"
