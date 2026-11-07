$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
& (Join-Path $projectRoot 'build.ps1')
$appDirectory = Join-Path $projectRoot 'release\ZZ游戏管理器'
$appZip = Join-Path $projectRoot 'release\ZZ游戏管理器-v1.2.zip'
$sourceZip = Join-Path $projectRoot 'release\ZZ游戏管理器-v1.2-源码.zip'

# Explicit allowlist: never include an existing user's data directory or test fixture.
$appFiles = @((Join-Path $appDirectory 'ZZGameManager.exe'))
$appFiles += @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'docs') -File | ForEach-Object { Join-Path $appDirectory $_.Name })
Compress-Archive -LiteralPath $appFiles -DestinationPath $appZip -Force

# Keep source folder layout by staging into an isolated new directory.
$stage = Join-Path $projectRoot ('release\source-stage-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fffffff'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
foreach ($item in @('src', 'docs', 'tools', 'build.ps1', 'test.ps1', 'package.ps1', 'README.md', '.gitignore'))
{
    Copy-Item -LiteralPath (Join-Path $projectRoot $item) -Destination $stage -Recurse -Force
}
New-Item -ItemType Directory -Path (Join-Path $stage 'tests') -Force | Out-Null
foreach ($item in @('ProbeGame.cs', 'Tests.cs', 'PaletteTests.cs'))
{
    Copy-Item -LiteralPath (Join-Path $projectRoot "tests\$item") -Destination (Join-Path $stage 'tests') -Force
}
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $sourceZip -Force

# Delete only our verified staging directory within release.
$resolvedStage = [IO.Path]::GetFullPath($stage)
$releaseRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'release')) + [IO.Path]::DirectorySeparatorChar
if (-not $resolvedStage.StartsWith($releaseRoot, [StringComparison]::OrdinalIgnoreCase))
{
    throw '源码暂存目录超出 release，拒绝清理。'
}
Remove-Item -LiteralPath $resolvedStage -Recurse -Force
Write-Output "成品包：$appZip"
Write-Output "源码包：$sourceZip"
