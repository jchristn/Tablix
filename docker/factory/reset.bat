@echo off
setlocal

set "FACTORY_DIR=%~dp0"
set "DOCKER_DIR=%FACTORY_DIR%.."

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
call docker compose -f "%DOCKER_DIR%\compose.yaml" down

echo.
echo Restoring factory database...
copy /Y "%FACTORY_DIR%database.db" "%DOCKER_DIR%\database.db" >nul
if errorlevel 1 (
    echo Failed to restore factory database.
    exit /b 1
)
copy /Y "%FACTORY_DIR%tablix.db" "%DOCKER_DIR%\tablix.db" >nul
if errorlevel 1 (
    echo Failed to restore factory Tablix persistence database.
    exit /b 1
)
if exist "%DOCKER_DIR%\tablix.db-wal" del /F /Q "%DOCKER_DIR%\tablix.db-wal"
if exist "%DOCKER_DIR%\tablix.db-shm" del /F /Q "%DOCKER_DIR%\tablix.db-shm"

echo.
echo Restoring factory configuration...
copy /Y "%FACTORY_DIR%tablix.json" "%DOCKER_DIR%\tablix.json" >nul
if errorlevel 1 (
    echo Failed to restore factory configuration.
    exit /b 1
)

echo.
echo Clearing logs...
if exist "%DOCKER_DIR%\logs" rd /S /Q "%DOCKER_DIR%\logs"
mkdir "%DOCKER_DIR%\logs"

echo.
echo ============================================
echo   Reset complete.
echo ============================================
echo.
