$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testBuild = Join-Path $projectRoot 'tests\bin'
$testRun = Join-Path $projectRoot ('tests\runs\' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fffffff'))
New-Item -ItemType Directory -Path $testBuild,$testRun -Force | Out-Null

& $compilerPath /nologo /target:winexe /utf8output "/out:$testBuild\ProbeGame.exe" (Join-Path $projectRoot 'tests\ProbeGame.cs')
if ($LASTEXITCODE -ne 0)
{
    throw '测试启动程序编译失败。'
}
$testSources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | Where-Object { $_.Name -ne 'Program.cs' } | ForEach-Object { $_.FullName })
& $compilerPath /nologo /target:exe /utf8output "/out:$testBuild\Tests.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll /reference:System.Xml.dll $testSources (Join-Path $projectRoot 'tests\Tests.cs') (Join-Path $projectRoot 'tests\PaletteTests.cs')
if ($LASTEXITCODE -ne 0)
{
    throw '测试程序编译失败。'
}
& "$testBuild\Tests.exe" $testRun "$testBuild\ProbeGame.exe" 2>&1 | Tee-Object -FilePath (Join-Path $testRun 'results.txt')
if ($LASTEXITCODE -ne 0)
{
    throw "测试失败，详见 $testRun\results.txt"
}
Write-Output "测试证据：$testRun"
