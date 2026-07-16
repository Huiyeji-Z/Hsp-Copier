# Hsp Copier - Velopack 打包脚本
# 在 publish.ps1 完成后运行，产出 .vpk 增量/全量更新包

param(
    [string]$Version = "0.1.0",
    [string]$Channel = "stable"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot "publish\win-x64"
$exePath = Join-Path $publishDir "HspCopier.exe"
$releaseDir = Join-Path $repoRoot "releases"
$iconPath = Join-Path $repoRoot "src\HspCopier.App\Assets\hspcopier.ico"

if (-not (Test-Path $exePath)) {
    Write-Error "未找到发布产物 $exePath。请先运行 publish.ps1"
    exit 1
}

if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
}

Write-Host "==> Packing Velopack release v$Version..." -ForegroundColor Cyan

# vpk pack 使用长参数避免短参数歧义（生成 delta + full 包）
$vpkArgs = @(
    "pack",
    "--packId", "HspCopier",
    "--packVersion", $Version,
    "--packDir", $publishDir,
    "--mainExe", "HspCopier.exe",
    "--releaseDir", $releaseDir,
    "--channel", $Channel
)
if (Test-Path $iconPath) {
    $vpkArgs += @("--icon", $iconPath)
}

& vpk @vpkArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "vpk pack 失败 (exit $LASTEXITCODE)"
    exit $LASTEXITCODE
}

Write-Host "==> Velopack release packaged to $releaseDir" -ForegroundColor Green
Get-ChildItem $releaseDir -Filter "*.vpk" -ErrorAction SilentlyContinue | Format-Table Name, Length
