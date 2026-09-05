@echo off
title Configure Windows Firewall for Phone Racing Wheel
echo ===================================================================
echo   Phone Racing Wheel - Windows Firewall Configuration
echo   Opens UDP port 5000 (telemetry) and 5152 (discovery)
echo   Profile: Private Network (minimal security footprint)
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

echo Removing any previous Phone Racing Wheel firewall rules...
netsh advfirewall firewall delete rule name="Phone Racing Wheel UDP Bridge" >nul 2>&1
netsh advfirewall firewall delete rule name="Phone Racing Wheel Discovery" >nul 2>&1

echo Adding inbound UDP firewall rule for controller port 5000 (Private & Public/Hotspot profiles)...
netsh advfirewall firewall add rule name="Phone Racing Wheel UDP Bridge" dir=in action=allow protocol=UDP localport=5000 profile=any

echo Adding inbound UDP firewall rule for discovery port 5152 (Private & Public/Hotspot profiles)...
netsh advfirewall firewall add rule name="Phone Racing Wheel Discovery" dir=in action=allow protocol=UDP localport=5152 profile=any

echo.
echo ===================================================================
echo   Firewall rules configured successfully!
echo.
echo   HOTSPOT NOTE:
echo   When connecting to your phone's Wi-Fi hotspot for the first time,
echo   Windows may set the connection to "Public network".
echo   Please make sure your hotspot Wi-Fi network profile is set to "Private":
echo     1. Open Windows Settings -> Network & internet -> Wi-Fi
echo     2. Click on your connected Phone Hotspot network
echo     3. Under "Network profile type", select "Private network"
echo ===================================================================
echo.
pause
