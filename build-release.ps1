param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
if (-not $SkipBuild) { & (Join-Path $PSScriptRoot 'companion\build.ps1') -Test }
$modPath = Join-Path $PSScriptRoot 'mod\noita_f5f8'
$payload = Join-Path $modPath 'companion'
New-Item -ItemType Directory -Path $payload -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'companion\NoitaF5F8.exe') -Destination (Join-Path $payload 'NoitaF5F8.bin') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'companion\NoitaF5F8.cs') -Destination (Join-Path $payload 'CompanionSource.txt') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'companion\README.txt') -Destination (Join-Path $payload 'README.txt') -Force
$hash = (Get-FileHash -LiteralPath (Join-Path $payload 'NoitaF5F8.bin') -Algorithm SHA256).Hash
"$hash  NoitaF5F8.bin" | Set-Content -LiteralPath (Join-Path $payload 'SHA256.txt') -Encoding ascii
$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$zipStage = Join-Path $dist ('local-zip-' + [guid]::NewGuid().ToString('N'))
$numericFolder = Join-Path $zipStage '3835902155'
New-Item -ItemType Directory -Path $numericFolder -Force | Out-Null
Get-ChildItem -LiteralPath $modPath -Force | Copy-Item -Destination $numericFolder -Recurse -Force
Compress-Archive -Path $numericFolder -DestinationPath (Join-Path $dist 'Noita-F5F8-Local.zip') -Force
$allowed = @('.txt','.csv','.bmp','.xml','.png','.lua','.frag','.vert','.bank','.bin','.plz')
$unexpected = Get-ChildItem -LiteralPath $modPath -File -Recurse | Where-Object { $_.Extension -notin $allowed }
if ($unexpected) { throw "Unsupported Workshop files: $($unexpected.FullName -join ', ')" }
[xml]$null = Get-Content -LiteralPath (Join-Path $modPath 'mod.xml') -Raw
[xml]$null = Get-Content -LiteralPath (Join-Path $modPath 'workshop.xml') -Raw
Write-Output "Workshop payload: $modPath"
Write-Output "Companion SHA256: $hash"
