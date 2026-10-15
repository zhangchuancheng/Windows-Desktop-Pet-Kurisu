$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source = Join-Path $PSScriptRoot 'ExtractIdleLayers.cs'
New-Item -ItemType Directory -Force -Path (Join-Path $projectRoot 'test-artifacts') | Out-Null
$output = Join-Path $projectRoot 'test-artifacts\ExtractIdleLayers.exe'
& $compiler /nologo /target:exe /optimize+ "/out:$output" /reference:System.dll /reference:System.Drawing.dll $source
if ($LASTEXITCODE -ne 0) { throw 'Layer extraction compilation failed.' }
& $output (Join-Path $projectRoot 'assets\layers\idle-v1')
if ($LASTEXITCODE -ne 0) { throw 'Layer extraction failed.' }
