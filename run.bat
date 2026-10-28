@echo off
cd /d "%~dp0"
echo Iniciando DiskTune com privilegios de administrador...
powershell -Command "Start-Process 'bin\Debug\net8.0-windows\DiskTune.exe' -Verb RunAs"
