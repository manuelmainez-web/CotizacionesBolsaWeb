# Arranca la app publicada (carpeta publish/) y el tunel Cloudflare juntos,
# mostrando la URL publica nueva del tunel en cuanto esta disponible.
# Uso: .\start-secure-tunnel.ps1
# Para detener ambos procesos, pulsa Ctrl+C en esta misma ventana.

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$logFile = Join-Path $PSScriptRoot 'cloudflared-log.txt'
if (Test-Path $logFile) { Remove-Item $logFile -Force }

$appProcess = $null
$tunnelProcess = $null

try {
    Write-Host "Iniciando app publicada (puerto 5072)..." -ForegroundColor Green
    $appProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList ".\publish\CotizacionesBolsaWeb.dll --urls http://0.0.0.0:5072" `
        -WorkingDirectory $PSScriptRoot -PassThru -NoNewWindow

    Write-Host "Iniciando tunel Cloudflare..." -ForegroundColor Green
    $tunnelProcess = Start-Process -FilePath ".\tools\cloudflared.exe" `
        -ArgumentList "tunnel --url http://localhost:5072" `
        -RedirectStandardError $logFile `
        -WorkingDirectory $PSScriptRoot -PassThru -NoNewWindow

    Write-Host "Esperando la URL publica del tunel..." -ForegroundColor Yellow
    $publicUrl = $null
    $timeoutAt = (Get-Date).AddSeconds(30)
    while (-not $publicUrl -and (Get-Date) -lt $timeoutAt) {
        Start-Sleep -Milliseconds 500
        if (Test-Path $logFile) {
            $match = Select-String -Path $logFile -Pattern 'https://[a-z0-9-]+\.trycloudflare\.com' -ErrorAction SilentlyContinue |
                Select-Object -First 1
            if ($match) {
                $publicUrl = $match.Matches[0].Value
            }
        }
    }

    if ($publicUrl) {
        Write-Host ""
        Write-Host "==========================================================" -ForegroundColor Cyan
        Write-Host "  URL PUBLICA (nueva en cada arranque): $publicUrl" -ForegroundColor Cyan
        Write-Host "==========================================================" -ForegroundColor Cyan
        Write-Host ""

        # Actualiza appsettings.json con la URL nueva del tunel para que el
        # codigo QR de la pagina (Portfolio:PublicUrl) siempre apunte al
        # tunel vigente. Gracias al reload automatico de configuracion de
        # ASP.NET Core, la app ya en marcha recoge el cambio sin reiniciar.
        try {
            $settingsPath = Join-Path $PSScriptRoot 'appsettings.json'
            $settings = Get-Content -Path $settingsPath -Raw | ConvertFrom-Json -AsHashtable
            if (-not $settings.ContainsKey('Portfolio')) { $settings['Portfolio'] = @{} }
            $settings['Portfolio']['PublicUrl'] = $publicUrl
            ($settings | ConvertTo-Json -Depth 10) | Set-Content -Path $settingsPath -Encoding UTF8
            Write-Host "appsettings.json actualizado: el codigo QR ya apunta a $publicUrl" -ForegroundColor Green
        }
        catch {
            Write-Host "Aviso: no se pudo actualizar appsettings.json con la URL nueva ($_). El codigo QR puede mostrar una URL antigua." -ForegroundColor Red
        }
    }
    else {
        Write-Host "No se detecto la URL a tiempo. Revisa $logFile manualmente." -ForegroundColor Red
    }

    Write-Host "App y tunel en marcha. Pulsa Ctrl+C en esta ventana para detener ambos." -ForegroundColor Green
    while ($true) {
        Start-Sleep -Seconds 2
        if ($appProcess.HasExited) {
            Write-Host "La app se ha detenido inesperadamente." -ForegroundColor Red
            break
        }
        if ($tunnelProcess.HasExited) {
            Write-Host "El tunel se ha detenido inesperadamente." -ForegroundColor Red
            break
        }
    }
}
finally {
    Write-Host "Deteniendo procesos..." -ForegroundColor Yellow
    if ($appProcess -and -not $appProcess.HasExited) {
        Stop-Process -Id $appProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if ($tunnelProcess -and -not $tunnelProcess.HasExited) {
        Stop-Process -Id $tunnelProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
