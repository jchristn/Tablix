@echo off
if "%1"=="" (
    echo Usage: build-dashboard.bat [version-tag]
    echo Example: build-dashboard.bat v0.1.0
    exit /b 1
)

echo Building Tablix Dashboard %1...
docker buildx build ^
    --builder cloud-jchristn77-jchristn77 ^
    --platform linux/amd64,linux/arm64/v8 ^
    -t jchristn77/tablix-ui:%1 ^
    -t jchristn77/tablix-ui:latest ^
    -f dashboard/Dockerfile ^
    --push ^
    dashboard/

echo Done.
