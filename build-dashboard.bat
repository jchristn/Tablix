@echo off
if "%~1"=="" (
    echo Usage: build-dashboard.bat [version-tag]
    echo Example: build-dashboard.bat v0.3.0
    exit /b 1
)

set "VERSION_TAG=%~1"
set "DOCKER_BUILD_CLOUD_REF=jchristn77/jchristn77"
set "DOCKER_BUILD_CLOUD_BUILDER=cloud-jchristn77-jchristn77"

echo Building Tablix Dashboard %VERSION_TAG% with Docker Build Cloud builder %DOCKER_BUILD_CLOUD_BUILDER%...
docker buildx inspect "%DOCKER_BUILD_CLOUD_BUILDER%" >nul 2>&1
if errorlevel 1 (
    echo Connecting Docker Build Cloud builder %DOCKER_BUILD_CLOUD_REF%...
    docker buildx create --driver cloud "%DOCKER_BUILD_CLOUD_REF%" >nul
    if errorlevel 1 (
        echo Failed to connect Docker Build Cloud builder %DOCKER_BUILD_CLOUD_REF%.
        exit /b %errorlevel%
    )
)

docker buildx build ^
    --builder "%DOCKER_BUILD_CLOUD_BUILDER%" ^
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
