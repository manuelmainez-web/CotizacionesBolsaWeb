# Arranca la app en local (sin autenticación) y recompila si hay cambios.
# Uso: .\start-secure.ps1

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Write-Host "Compilando cambios..." -ForegroundColor Yellow
dotnet build .\CotizacionesBolsaWeb.csproj --configuration Debug --nologo

Write-Host "Iniciando app..." -ForegroundColor Green
dotnet .\bin\Debug\net9.0\CotizacionesBolsaWeb.dll --urls http://0.0.0.0:5072
