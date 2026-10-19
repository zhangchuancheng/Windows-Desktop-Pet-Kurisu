$Tests = @('SizeSliderSmoke', 'OutfitSwitchSmoke', 'OutfitDragSmoke', 'OutfitIdleSmoke', 'OutfitGazeSmoke', 'IdleMotionSmoke', 'GazeSmoke', 'ClickArtSmoke', 'DialogueSmoke',
    'RapidClickSmoke', 'DragAnimationSmoke', 'SleepMotionSmoke', 'Step4Acceptance')
if ($args.Count -gt 0) { $Tests = $args }
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $projectRoot 'test-artifacts'
New-Item -ItemType Directory -Force -Path (Join-Path $outputRoot 'screenshots') | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName
foreach ($testName in $Tests) {
    $target = Join-Path $outputRoot ($testName + '.exe')
    $testSource = Join-Path $PSScriptRoot ($testName + '.cs')
    & $compiler /nologo /target:exe /optimize+ "/main:MakiseDesktopPet.$testName" "/out:$target" `
        /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll `
        /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll `
        /reference:System.Net.Http.dll @sources $testSource
    if ($LASTEXITCODE -ne 0) { throw "Failed to compile $testName" }
    & $target $projectRoot | Tee-Object -FilePath (Join-Path $outputRoot ("step4-$testName.log"))
    if ($LASTEXITCODE -ne 0) { throw "$testName failed; inspect the log." }
}
Write-Host 'Background checks completed. PNG samples and timestamps are in test-artifacts/step4.'
Write-Host 'Optional GIF encoding: python tests/encode-step4.py (requires Pillow).'
