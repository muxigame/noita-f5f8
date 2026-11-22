param([switch]$Test, [string]$OutputName = 'NoitaF5F8.exe')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$source = Join-Path $PSScriptRoot 'NoitaF5F8.cs'
if ([System.IO.Path]::GetFileName($OutputName) -ne $OutputName -or [System.IO.Path]::GetExtension($OutputName) -ne '.exe') { throw 'OutputName must be a plain .exe filename.' }
$output = Join-Path $PSScriptRoot $OutputName
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll /reference:System.Core.dll /out:$output $source
if ($LASTEXITCODE -ne 0) { throw 'Companion compilation failed.' }
Write-Output "Built $output"
if ($Test) {
    $testOutput = Join-Path $PSScriptRoot 'NoitaF5F8.Tests.exe'
    & $compiler /nologo /target:exe /optimize+ /platform:anycpu /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll /reference:System.Core.dll /out:$testOutput $source
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & $testOutput --self-test
    if ($LASTEXITCODE -ne 0) { throw 'Companion self-tests failed.' }
}
