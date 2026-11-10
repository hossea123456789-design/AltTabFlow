@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Windows-Build.ps1"
set BUILD_RESULT=%ERRORLEVEL%
echo.
if not "%BUILD_RESULT%"=="0" echo Build failed with exit code %BUILD_RESULT%.
pause
exit /b %BUILD_RESULT%
