$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$releaseDir = Join-Path $PSScriptRoot 'releases'
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
$package = Join-Path $releaseDir 'MakiseDesktopPet-Windows.zip'
$dist = Join-Path $PSScriptRoot 'dist'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $dist -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CHANGELOG.md') -Destination $dist -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs') -Destination $dist -Recurse -Force
$contents = @(
    (Join-Path $dist 'MakiseDesktopPet.exe'),
    (Join-Path $dist 'MakiseDesktopPet.exe.config'),
    (Join-Path $dist 'dialogues.json'),
    (Join-Path $dist 'assets'),
    (Join-Path $dist 'README.md'),
    (Join-Path $dist 'CHANGELOG.md'),
    (Join-Path $dist 'docs')
)
Compress-Archive -Path $contents -DestinationPath $package -Force
Write-Host "Packaged: $package"

$sourcePackage = Join-Path $releaseDir 'MakiseDesktopPet-Source.zip'
$sourceContents = @(
    (Join-Path $PSScriptRoot 'src'),
    (Join-Path $PSScriptRoot 'assets'),
    (Join-Path $PSScriptRoot 'tests'),
    (Join-Path $PSScriptRoot 'tools'),
    (Join-Path $PSScriptRoot 'docs'),
    (Join-Path $PSScriptRoot '.gitignore'),
    (Join-Path $PSScriptRoot '.gitattributes'),
    (Join-Path $PSScriptRoot 'build.ps1'),
    (Join-Path $PSScriptRoot 'package.ps1'),
    (Join-Path $PSScriptRoot 'App.config'),
    (Join-Path $PSScriptRoot 'app.manifest'),
    (Join-Path $PSScriptRoot 'dialogues.json'),
    (Join-Path $PSScriptRoot 'README.md'),
    (Join-Path $PSScriptRoot 'CHANGELOG.md')
)
Compress-Archive -Path $sourceContents -DestinationPath $sourcePackage -Force
Write-Host "Packaged: $sourcePackage"
