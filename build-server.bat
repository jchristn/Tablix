@echo off
if "%~1"=="" (
    echo Usage: build-server.bat [version-tag]
    echo Example: build-server.bat v0.3.0
    exit /b 1
)

set "VERSION_TAG=%~1"

echo Building Tablix Server %VERSION_TAG%...
docker buildx build ^
    --platform linux/amd64,linux/arm64/v8 ^
    -t jchristn77/tablix-server:%VERSION_TAG% ^
    -t jchristn77/tablix-server:latest ^
    -f src/Tablix.Server/Dockerfile ^
    --push ^
    src/
if errorlevel 1 (
    echo Tablix Server build failed.
    exit /b %errorlevel%
)

echo Done.
exit /b 0
