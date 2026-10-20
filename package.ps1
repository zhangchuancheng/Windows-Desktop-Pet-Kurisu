$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$releaseDir = Join-Path $PSScriptRoot 'releases'
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
$package = Join-Path $releaseDir 'MakiseDesktopPet-Windows.zip'
$dist = Join-Path $PSScriptRoot 'dist'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $dist -Force
$runtimeReadme = Join-Path $dist 'README.md'
$version = (Get-Item -LiteralPath (Join-Path $dist 'MakiseDesktopPet.exe')).VersionInfo.ProductVersion
$readmeText = [IO.File]::ReadAllText($runtimeReadme)
$readmeText = $readmeText.Replace('assets/concepts/outfit-actions-2026-10-19/', 'assets/outfits/')
$readmeText = $readmeText.Replace('(tests/README.md)',
    "(https://github.com/zhangchuancheng/Windows-Desktop-Pet-Kurisu/blob/v$version/tests/README.md)")
[IO.File]::WriteAllText($runtimeReadme, $readmeText, (New-Object Text.UTF8Encoding($false)))
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CHANGELOG.md') -Destination $dist -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs') -Destination $dist -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\QUICKSTART.zh-CN.txt') -Destination (Join-Path $dist '使用说明.txt') -Force
$contents = @(
    (Join-Path $dist 'MakiseDesktopPet.exe'),
    (Join-Path $dist 'MakiseDesktopPet.exe.config'),
    (Join-Path $dist 'dialogues.json'),
    (Join-Path $dist 'chat-persona.json'),
    (Join-Path $dist 'Install-Chat-Model.cmd'),
    (Join-Path $dist 'setup-chat.ps1'),
    (Join-Path $dist 'assets'),
    (Join-Path $dist 'README.md'),
    (Join-Path $dist 'CHANGELOG.md'),
    (Join-Path $dist '使用说明.txt'),
    (Join-Path $dist 'docs')
)
Compress-Archive -Path $contents -DestinationPath $package -Force
Write-Host "Packaged: $package"

$sourcePackage = Join-Path $releaseDir 'MakiseDesktopPet-Source.zip'
& git -C $PSScriptRoot archive --format=zip "--output=$sourcePackage" HEAD
if ($LASTEXITCODE -ne 0) { throw 'Failed to archive committed source files.' }
Write-Host "Packaged: $sourcePackage"

$checksums = Join-Path $releaseDir 'SHA256SUMS.txt'
@($package, $sourcePackage) | ForEach-Object {
    $hash = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $(Split-Path -Leaf $_)"
} | Set-Content -LiteralPath $checksums -Encoding Ascii
Write-Host "Checksums: $checksums"
