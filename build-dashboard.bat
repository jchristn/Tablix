@echo off
if "%~1"=="" (
    echo Usage: build-dashboard.bat [version-tag]
    echo Example: build-dashboard.bat v0.3.0
    exit /b 1
)

set "VERSION_TAG=%~1"

echo Building Tablix Dashboard %VERSION_TAG%...
docker buildx build ^
    --platform linux/amd64,linux/arm64/v8 ^
    -t jchristn77/tablix-ui:%VERSION_TAG% ^
    -t jchristn77/tablix-ui:latest ^
    -f dashboard/Dockerfile ^
    --push ^
    dashboard/
if errorlevel 1 (
    echo Tablix Dashboard build failed.
    exit /b %errorlevel%
)

echo Done.
exit /b 0
