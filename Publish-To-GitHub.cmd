@echo off
setlocal
chcp 65001 >nul
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Publish-To-GitHub.ps1"
if errorlevel 1 (
  echo.
  echo GitHub publish did not complete. Please review the message above.
  pause
  exit /b 1
)
echo.
echo Repository publish complete.
pause
