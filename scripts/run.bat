@echo off
title Phone Racing Wheel Bridge
cd /d "%~dp0.."
echo ===================================================
echo   Phone Racing Wheel - Windows Bridge
echo ===================================================
echo.

:: 1. Check if self-contained standalone executable exists
if exist "publish\WindowsBridge.exe" (
    echo Found standalone executable: .\publish\WindowsBridge.exe
    echo Starting Windows Bridge on UDP port 5000 (watchdog timeout 200ms)...
    echo.
    publish\WindowsBridge.exe 5000 200
    goto :end
)

:: 2. Check if dotnet CLI is installed
where dotnet >nul 2>&1
if %errorlevel% equ 0 (
    echo Starting Windows Bridge via .NET SDK on UDP port 5000 (watchdog timeout 200ms)...
    echo.
    dotnet run -c Release -- 5000 200
    goto :end
)

:: 3. Neither found
echo [ERROR] Neither .NET 8.0 SDK nor a prebuilt executable was found!
echo.
echo To run this application, choose ONE of the following:
echo   Option A (Recommended):
echo     1. Download and install .NET 8.0 SDK from:
echo        https://dotnet.microsoft.com/download/dotnet/8.0
echo     2. Re-run this script or run "scripts\build_self_contained.bat".
echo.
echo   Option B:
echo     Build the executable on another PC with .NET 8 SDK using "scripts\build_self_contained.bat",
echo     then place "WindowsBridge.exe" inside the "publish\" folder.
echo.

:end
echo.
pause

