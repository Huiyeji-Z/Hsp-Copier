# Hsp Copier - Velopack 打包脚本
# 在 publish.ps1 完成后运行，产出 .vpk 增量/全量更新包

param(
    [string]$Version = "0.1.0",
    [string]$Channel = "stable",
    [string]$RepoOwner = "hspcopier",
    [string]$RepoName = "hsp-copier"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot "publish\win-x64"
$exePath = Join-Path $publishDir "HspCopier.exe"

if (-not (Test-Path $exePath)) {
    Write-Error "未找到发布产物 $exePath。请先运行 publish.ps1"
    exit 1
}

Write-Host "==> Packing Velopack release v$Version..." -ForegroundColor Cyan

# vpk pack（生成 delta + full 包）
vpk pack `
    -u "HspCopier" `
    -v $Version `
    -p $publishDir `
    -e "HspCopier.exe" `
    --packId "HspCopier" `
    --packVersion $Version `
    --channel $Channel `
    --outputDir (Join-Path $repoRoot "releases") `
    --releaseNotes (Join-Path $repoRoot "CHANGELOG.md")

Write-Host "==> Velopack release packaged to $repoRoot\releases" -ForegroundColor Green
Get-ChildItem (Join-Path $repoRoot "releases") -Filter "*.vpk" | Format-Table Name, Length
