@echo off
if "%~1"=="" (
    echo Usage: build-all.bat [version-tag]
    echo Example: build-all.bat v0.3.0
    exit /b 1
)

set "VERSION_TAG=%~1"

pushd "%~dp0" >nul
if errorlevel 1 exit /b 1

call build-dashboard.bat "%VERSION_TAG%"
if errorlevel 1 (
    popd
    exit /b %errorlevel%
)

call build-server.bat "%VERSION_TAG%"
if errorlevel 1 (
    popd
    exit /b %errorlevel%
)

popd
echo Done.
exit /b 0
