@echo off
title Atlas de Aethria - anfitrion P2P
cd /d "%~dp0"
where node >nul 2>nul
if errorlevel 1 (
  echo No se encuentra Node.js. Instala Node.js 18 o posterior y vuelve a abrir este archivo.
  pause
  exit /b 1
)
echo =============================================================
echo  ATLAS DE AETHRIA - SERVIDOR DIRECTO DEL ANFITRION
echo =============================================================
echo Mantén esta ventana abierta durante la partida.
echo Puerto TCP: 3080
echo Direcciones IPv4 locales detectadas:
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-NetIPAddress -AddressFamily IPv4 -AddressState Preferred -ErrorAction SilentlyContinue ^| Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } ^| ForEach-Object { Write-Host ('  http://' + $_.IPAddress + ':3080') }"
echo En el juego, crea la sala y comparte direccion + codigo.
echo Para jugar por Internet, permite TCP 3080 en firewall y router.
echo =============================================================
node index.js
pause
