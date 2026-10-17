param([switch]$CheckOnly)

$ErrorActionPreference = 'Stop'
$modelName = 'qwen3:4b-instruct'
$downloadPage = 'https://ollama.com/download/windows'

$ollamaCommand = Get-Command 'ollama.exe' -CommandType Application -ErrorAction SilentlyContinue
$ollamaPath = if ($ollamaCommand) { $ollamaCommand.Source } else { $null }
if (-not $ollamaPath) {
    $usualPath = Join-Path $env:LOCALAPPDATA 'Programs\Ollama\ollama.exe'
    if (Test-Path -LiteralPath $usualPath) { $ollamaPath = $usualPath }
}

if (-not $ollamaPath) {
    Write-Host '没有找到 Ollama。请先从官方页面安装 Windows 版：' -ForegroundColor Yellow
    Write-Host $downloadPage
    Write-Host '安装并启动 Ollama 后，再运行此脚本。'
    exit 2
}

try {
    $installedModels = (& $ollamaPath list 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) { throw '本机 Ollama 服务尚未启动。' }
}
catch {
    Write-Host '无法连接本机 Ollama。请先从开始菜单启动 Ollama，再运行此脚本。' -ForegroundColor Yellow
    exit 3
}

if ($installedModels -match '(?m)^qwen3:4b-instruct\s+') {
    Write-Host "模型 $modelName 已安装，可以直接打开桌宠聊天。" -ForegroundColor Green
    exit 0
}

if ($CheckOnly) {
    Write-Host "模型 $modelName 尚未安装。"
    exit 1
}

Write-Host "开始从 Ollama 官方库下载 $modelName（约 2.5 GB），请保持网络连接。"
& $ollamaPath pull $modelName
if ($LASTEXITCODE -ne 0) {
    Write-Host '模型下载失败。检查网络后，可重新运行此脚本继续下载。' -ForegroundColor Red
    exit 4
}

$installedModels = (& $ollamaPath list 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or $installedModels -notmatch '(?m)^qwen3:4b-instruct\s+') {
    Write-Host '下载命令已结束，但本机模型列表里还没有目标模型。' -ForegroundColor Red
    exit 5
}

Write-Host '模型安装完成。保持 Ollama 运行，打开桌宠的“聊两句”即可使用。' -ForegroundColor Green
exit 0
