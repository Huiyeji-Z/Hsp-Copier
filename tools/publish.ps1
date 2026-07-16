# Hsp Copier - 发布脚本
# 用法：.\tools\publish.ps1 [-Version 0.1.0]

param(
    [string]$Version = "0.1.0",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\HspCopier.App\HspCopier.App.csproj"
$outDir = Join-Path $repoRoot "publish\$Runtime"

Write-Host "==> Publishing Hsp Copier v$Version ($Configuration, $Runtime)..." -ForegroundColor Cyan

# 设置版本号
dotnet build $project -c $Configuration -p:Version=$Version

# 单文件打包
dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeAllContentForSelfExtract=true `
    --self-contained true `
    -o $outDir

Write-Host "==> Published to $outDir" -ForegroundColor Green
Get-ChildItem $outDir -Filter "HspCopier.exe" | Format-Table Name, Length
