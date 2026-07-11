@echo off
setlocal

cd /d "%~dp0"

docker compose pull
if errorlevel 1 exit /b %errorlevel%

docker compose down
if errorlevel 1 exit /b %errorlevel%

docker compose up -d
if errorlevel 1 exit /b %errorlevel%

docker ps -a

endlocal
