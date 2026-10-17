@echo off
chcp 65001 >nul
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup-chat.ps1"
set "result=%errorlevel%"
echo.
if not "%result%"=="0" echo Please follow the instructions above, then run this installer again.
pause
exit /b %result%
