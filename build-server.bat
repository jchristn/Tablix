@echo off
if "%1"=="" (
    echo Usage: build-server.bat [version-tag]
    echo Example: build-server.bat v0.1.0
    exit /b 1
)

echo Building Tablix Server %1...
docker buildx build ^
    --builder cloud-jchristn77-jchristn77 ^
    --platform linux/amd64,linux/arm64/v8 ^
    -t jchristn77/tablix-server:%1 ^
    -t jchristn77/tablix-server:latest ^
    -f src/Tablix.Server/Dockerfile ^
    --push ^
    src/

echo Done.
