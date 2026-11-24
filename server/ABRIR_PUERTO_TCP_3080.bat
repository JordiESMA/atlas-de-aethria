@echo off
net session >nul 2>&1
if errorlevel 1 (
  echo Haz clic derecho en este archivo y selecciona "Ejecutar como administrador".
  pause
  exit /b 1
)
netsh advfirewall firewall add rule name="Atlas de Aethria P2P TCP 3080" dir=in action=allow protocol=TCP localport=3080 profile=private
if errorlevel 1 echo No se pudo crear la regla del firewall.
if not errorlevel 1 echo Regla creada para permitir TCP 3080 en redes privadas.
pause
