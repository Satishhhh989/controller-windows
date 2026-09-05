@echo off
title Build Standalone Windows Bridge
cd /d "%~dp0.."
echo ===================================================
echo   Building Self-Contained Single-File Executable
echo   Target: Windows x64 (.NET 8 runtime included)
echo ===================================================
echo.

where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] .NET 8.0 SDK is required to compile the executable.
    echo Please download and install .NET 8.0 SDK:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)

echo Publishing self-contained win-x64 executable to .\publish ...
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o .\publish

if %errorlevel% equ 0 (
    echo.
    echo ===================================================
    echo   BUILD SUCCEEDED!
    echo   Executable is ready at: .\publish\WindowsBridge.exe
    echo.
    echo   To run the bridge:
    echo     .\publish\WindowsBridge.exe
    echo ===================================================
) else (
    echo.
    echo [ERROR] Build failed. Please check the compiler errors above.
)
echo.
pause

