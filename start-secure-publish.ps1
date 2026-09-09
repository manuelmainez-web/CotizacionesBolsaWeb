# Arranca la versión PUBLICADA (carpeta publish/) sin autenticación.
# Uso: .\start-secure-publish.ps1

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot\publish

Write-Host "Iniciando app publicada..." -ForegroundColor Green
dotnet .\CotizacionesBolsaWeb.dll --urls http://0.0.0.0:5072
