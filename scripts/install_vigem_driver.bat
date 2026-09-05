@echo off
title Install ViGEmBus Driver for Phone Racing Wheel
echo ===================================================================
echo   Phone Racing Wheel - ViGEmBus Driver Setup (Forza Horizon)
echo   Installs the virtual Xbox 360 controller driver for Windows
echo ===================================================================
echo.

:: Check for Administrative privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] This script must be run as Administrator!
    echo Right-click this file and select "Run as administrator".
    echo.
    pause
    exit /b 1
)

:: Check if already installed
if exist "%SystemRoot%\System32\drivers\ViGEmBus.sys" (
    echo [INFO] ViGEmBus driver is already installed on this machine!
    echo Virtual Xbox 360 controller is ready to use in Forza Horizon.
    echo.
    pause
    exit /b 0
)

:: Check if installer already exists locally in drivers folder
if exist "%~dp0..\drivers\ViGEmBus_1.22.0_x64_x86_arm64.exe" (
    echo [INFO] Found local ViGEmBus installer in drivers folder.
    echo Launching installer...
    "%~dp0..\drivers\ViGEmBus_1.22.0_x64_x86_arm64.exe"
    goto :done
)

echo Downloading official ViGEmBus Setup (v1.22.0) from GitHub...
set "INSTALLER=%TEMP%\ViGEmBus_1.22.0_x64_x86_arm64.exe"
set "URL=https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_1.22.0_x64_x86_arm64.exe"

:: Try curl first (standard in Windows 10/11)
where curl >nul 2>&1
if %errorlevel% equ 0 (
    echo Using curl to download installer...
    curl -L "%URL%" -o "%INSTALLER%"
) else (
    echo Using PowerShell to download installer...
    powershell -NoProfile -ExecutionPolicy Bypass -Command ^
        "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; " ^
        "(New-Object System.Net.WebClient).DownloadFile('%URL%', '%INSTALLER%')"
)

if not exist "%INSTALLER%" (
    echo [ERROR] Download failed. Please download ViGEmBus manually from:
    echo %URL%
    echo.
    pause
    exit /b 1
)

echo.
echo Launching ViGEmBus installer...
echo Please follow the setup wizard prompts to complete installation.
echo.
"%INSTALLER%"

:done
echo.
echo ===================================================================
echo   ViGEmBus installation completed!
echo   Run publish\WindowsBridge.exe to start the virtual controller.
echo ===================================================================
echo.
pause

