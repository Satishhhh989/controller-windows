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

echo Downloading official ViGEmBus Setup (v1.22.0) from GitHub...
set "INSTALLER=%TEMP%\ViGEmBus_Setup_1.22.0.exe"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; " ^
    "$url = 'https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_Setup_1.22.0.exe'; " ^
    "Write-Host 'Downloading installer...'; " ^
    "Invoke-WebRequest -Uri $url -OutFile '%INSTALLER%'"

if not exist "%INSTALLER%" (
    echo [ERROR] Download failed. Please download and install ViGEmBus manually from:
    echo https://github.com/nefarius/ViGEmBus/releases/latest
    echo.
    pause
    exit /b 1
)

echo.
echo Launching ViGEmBus installer...
echo Please follow the setup wizard prompts to complete installation.
echo.
"%INSTALLER%"

echo.
echo ===================================================================
echo   ViGEmBus installation completed!
echo   Run publish\WindowsBridge.exe to start the virtual controller.
echo ===================================================================
echo.
pause
