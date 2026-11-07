$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$outputDirectory = Join-Path $projectRoot 'release\ZZ游戏管理器'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath))
{
    throw '需要 Windows 自带的 .NET Framework 4.x 编译器。'
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compilerPath /nologo /target:winexe /platform:anycpu /optimize+ /utf8output "/out:$outputDirectory\ZZGameManager.exe" "/win32manifest:$projectRoot\src\app.manifest" "/win32icon:$projectRoot\src\app.ico" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll /reference:System.Xml.dll $sourceFiles
if ($LASTEXITCODE -ne 0)
{
    throw '软件编译失败。'
}

Get-ChildItem -LiteralPath (Join-Path $projectRoot 'docs') -File | Copy-Item -Destination $outputDirectory -Force
Write-Output "已生成：$outputDirectory\ZZGameManager.exe"
