@echo off
net session >nul 2>&1
if errorlevel 1 (
  echo Haz clic derecho en este archivo y selecciona "Ejecutar como administrador".
  pause
  exit /b 1
)
netsh advfirewall firewall delete rule name="Atlas de Aethria P2P TCP 3080"
pause
