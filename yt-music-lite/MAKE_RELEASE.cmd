@echo off
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0make-release.ps1" -BuildFirst
if errorlevel 1 pause
