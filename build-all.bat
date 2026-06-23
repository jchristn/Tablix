@echo off
if "%~1"=="" (
    echo Usage: build-all.bat [version-tag]
    echo Example: build-all.bat v0.1.1
    exit /b 1
)

pushd "%~dp0" >nul
if errorlevel 1 exit /b 1

call build-dashboard.bat "%~1"
if errorlevel 1 (
    popd
    exit /b 1
)

call build-server.bat "%~1"
if errorlevel 1 (
    popd
    exit /b 1
)

popd
echo Done.
