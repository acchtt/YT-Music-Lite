@echo off
setlocal
cd /d "%~dp0"
set "BOOTSTRAPPER=%TEMP%\MicrosoftEdgeWebview2Setup.exe"
echo Downloading Microsoft WebView2 Evergreen Runtime bootstrapper...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Invoke-WebRequest -UseBasicParsing 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile '%BOOTSTRAPPER%'"
if errorlevel 1 goto :error

echo Installing WebView2 Runtime...
"%BOOTSTRAPPER%" /silent /install
if errorlevel 1 goto :error

echo WebView2 Runtime install command completed.
pause
exit /b 0

:error
echo WebView2 Runtime installation failed.
pause
exit /b 1
