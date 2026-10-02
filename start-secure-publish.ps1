# Arranca la versión PUBLICADA (carpeta publish/) sin autenticación y la recompila antes.
# Uso: .\start-secure-publish.ps1

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$publishDir = Join-Path $PSScriptRoot 'bin\Release\net9.0\publish'

Write-Host "Publicando cambios..." -ForegroundColor Yellow
dotnet publish .\CotizacionesBolsaWeb.csproj -c Release --nologo -o $publishDir

Set-Location $publishDir

Write-Host "Iniciando app publicada..." -ForegroundColor Green
dotnet .\CotizacionesBolsaWeb.dll --urls http://0.0.0.0:5072
