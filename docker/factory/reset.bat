@echo off
echo.
echo ============================================
echo   Tablix Factory Reset
echo ============================================
echo.
echo WARNING: This will reset Tablix to factory defaults.
echo All data, configuration changes, and logs will be lost.
echo.
set /p confirm="Are you sure? (y/N): "
if /i not "%confirm%"=="y" (
    echo Cancelled.
    exit /b 0
)

echo.
echo Stopping containers...
docker compose -f ..\compose.yaml down

echo.
echo Restoring factory database...
copy /Y factory\database.db ..\database.db

echo.
echo Restoring factory configuration...
copy /Y factory\tablix.json ..\tablix.json

echo.
echo Clearing logs...
if exist "..\logs" rd /S /Q ..\logs
mkdir ..\logs

echo.
echo ============================================
echo   Reset complete.
echo ============================================
echo.
