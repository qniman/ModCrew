# Rebuilds the ModCrewFinal folder next to this repo:
#   ..\App\ModCrew.exe              portable app (run as is)
#   ..\Installer\ModCrew-Setup.exe  installer for friends (the same exe; the name switches it to setup mode)
# Usage:  powershell -ExecutionPolicy Bypass -File build-final.ps1
$ErrorActionPreference = 'Stop'
$src = $PSScriptRoot
$final = Split-Path $src -Parent
$app = Join-Path $final 'App'
$installer = Join-Path $final 'Installer'

Write-Host '== tests' -ForegroundColor Cyan
dotnet test (Join-Path $src 'tests\ModCrew.Tests') -nologo -v:q
if ($LASTEXITCODE) { throw 'Tests failed' }

Write-Host '== publish' -ForegroundColor Cyan
dotnet publish (Join-Path $src 'src\ModCrew') -p:PublishProfile=Friends -nologo -v:q
if ($LASTEXITCODE) { throw 'Publish failed' }
$exe = Join-Path $src 'publish\ModCrew.exe'

New-Item -ItemType Directory -Force $app, $installer | Out-Null
Copy-Item $exe (Join-Path $app 'ModCrew.exe') -Force
Copy-Item $exe (Join-Path $installer 'ModCrew-Setup.exe') -Force

$version = (Get-Item $exe).VersionInfo.ProductVersion -replace '\+.*$', ''
$mb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "== done: ModCrew $version ($mb MB)" -ForegroundColor Green
Write-Host "   $app\ModCrew.exe"
Write-Host "   $installer\ModCrew-Setup.exe"
