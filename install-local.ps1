param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Noita'
)
$ErrorActionPreference = 'Stop'
$resolvedGame = [IO.Path]::GetFullPath($GamePath)
$gameExe = Join-Path $resolvedGame 'noita.exe'
if (-not (Test-Path -LiteralPath $gameExe -PathType Leaf)) { throw 'Noita.exe was not found.' }
$source = Join-Path $PSScriptRoot 'mod\noita_f5f8'
$target = Join-Path $resolvedGame 'mods\3835902155'
if (-not (Test-Path -LiteralPath (Join-Path $source 'companion\NoitaF5F8.bin'))) { throw 'Build the release first.' }
New-Item -ItemType Directory -Path $target -Force | Out-Null
Get-ChildItem -LiteralPath $source | Copy-Item -Destination $target -Recurse -Force
Write-Output "Installed mod: $target"
Write-Output 'Allow unsafe mods and enable the local copy in Noita. It starts the companion automatically.'
