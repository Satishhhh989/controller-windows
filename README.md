# Phone Racing Wheel - Windows Bridge

High-performance native C# / .NET 8 companion application for Windows that receives real-time UDP controller telemetry from the mobile racing wheel over local Wi-Fi or phone hotspot, emulating a native virtual Xbox 360 controller via the ViGEmBus kernel driver.

---

## Architecture Overview

```
+-------------------------------------------------------+
|        Mobile Phone (Racing Wheel Controller)        |
|  - Gyroscope & Accelerometer sensor fusion            |
|  - Progressive brake pedal & analog throttle slider   |
|  - Paddle shifters & handbrake                        |
+-------------------------------------------------------+
                           |
                           | UDP Datagrams (Port 5000 @ ~60-100 Hz)
                           | Local Wi-Fi or Direct Phone Hotspot
                           v
+-------------------------------------------------------+
|         Windows PC (Windows Bridge Service)           |
|                                                       |
|  [UdpServer]                                          |
|   - Reused asynchronous socket listener               |
|   - Monotonic sequence validation & stale drop        |
|                                                       |
|  [Protocol Engine]                                    |
|   - High-throughput JSON parser                       |
|   - 24-byte zero-allocation binary decoder            |
|                                                       |
|  [Connection Watchdog]                                |
|   - 25 ms evaluation interval                         |
|   - 200 ms timeout fail-safe -> neutral controls      |
|                                                       |
|  [VirtualGamepad Adapter]                             |
|   - Nefarius ViGEm.Client Xbox 360 controller target  |
|   - Direct kernel driver interface (sub-millisecond)  |
|                                                       |
|  [Network Discovery & Telemetry]                      |
|   - 1 Hz UDP announcement beacon on port 5152         |
|   - Real-time rolling statistics (PPS, Jitter, Drops) |
|   - Clock offset compensated true network latency     |
|                                                       |
|  [Console Dashboard]                                  |
|   - Zero-flicker ANSI terminal HUD with visual gauges |
+-------------------------------------------------------+
                           |
                           | Native Windows Virtual Hardware Bus
                           v
+-------------------------------------------------------+
|       Virtual Xbox 360 Controller (XInput)            |
|  - Left Thumbstick X : Steering [-32767, 32767]       |
|  - Right Trigger     : Throttle [0, 255]              |
|  - Left Trigger      : Brake    [0, 255]              |
|  - Button A          : Handbrake                      |
|  - Right Bumper (RB) : Gear Up                        |
|  - Left Bumper  (LB) : Gear Down                      |
+-------------------------------------------------------+
                           |
                           | Standard DirectInput / XInput
                           v
+-------------------------------------------------------+
|             Racing Games & Simulators                 |
|  Forza Horizon 4 / 5, Forza Motorsport, F1 23 / 24,   |
|  Assetto Corsa, Project CARS, Need for Speed          |
+-------------------------------------------------------+
```

---

## Directory Structure

```
WindowsBridge/
|-- WindowsBridge.sln               # Visual Studio solution file
|-- WindowsBridge.csproj            # .NET 8 project file (Nefarius.ViGEm.Client)
|-- Program.cs                      # Entrypoint, argument parser, service orchestrator
|
|-- VirtualController/
|   `-- VirtualGamepad.cs           # ViGEmBus Xbox 360 controller emulation & axis mapping
|
|-- Networking/
|   |-- UdpServer.cs                # Asynchronous UDP socket listener & packet validator
|   `-- DeviceDiscovery.cs          # Local network beacon broadcaster on port 5152
|
|-- Protocol/
|   `-- ControllerPacket.cs         # JSON and 24-byte compact binary decoder & validator
|
|-- Controller/
|   `-- ControllerState.cs          # Thread-safe atomic controller state model
|
|-- Safety/
|   `-- ConnectionWatchdog.cs       # 200 ms timeout monitor (snaps inputs to neutral)
|
|-- Diagnostics/
|   `-- Statistics.cs               # Throughput (PPS), latency, packet drop, and jitter tracker
|
|-- UI/
|   `-- Dashboard.cs                # ANSI terminal dashboard with live ASCII gauges
|
|-- drivers/
|   `-- ViGEmBus_1.22.0_...exe      # Official offline installer for ViGEmBus kernel driver
|
|-- scripts/
|   |-- install_vigem_driver.bat    # Automated one-click ViGEmBus driver installation
|   |-- run.bat                     # Runner script (supports dotnet run or prebuilt binary)
|   |-- build_self_contained.bat    # Standalone single-file Windows x64 publisher
|   `-- allow_firewall.bat          # Configures Windows Firewall for UDP ports 5000 & 5152
|
|-- .gitignore                      # Git ignore file for build and temporary artifacts
`-- README.md                       # Technical documentation
```

---

## Prerequisites and System Requirements

### Hardware
- Windows PC or Gaming Laptop (Windows 10 or Windows 11, 64-bit).
- Local Wi-Fi network or mobile device capable of creating a Wi-Fi Hotspot.
- Smartphone running the Phone Racing Wheel mobile client.

### Software
- .NET 8.0 SDK or Desktop Runtime:
  Download: https://dotnet.microsoft.com/download/dotnet/8.0 (select .NET SDK x64 or .NET Desktop Runtime x64).
  CLI command via winget:
  ```powershell
  winget install Microsoft.DotNet.SDK.8
  ```
- ViGEmBus Driver (Version 1.22.0 or later):
  Included directly in the repository at `drivers/ViGEmBus_1.22.0_x64_x86_arm64.exe` or installed via `scripts/install_vigem_driver.bat`.
- Windows Firewall:
  Inbound UDP ports 5000 (telemetry) and 5152 (network discovery) must be open.

---

## Network Protocol and Wire Format

The Windows Bridge supports dual ingestion formats: JSON datagrams (for readability and debugging) and 24-byte compact binary datagrams (for ultra-low-bandwidth transmission). Both protocols are automatically parsed on UDP port 5000.

### 1. JSON Telemetry Format

```json
{
  "v": 1,
  "seq": 1042,
  "t": 1725555678123,
  "steer": -0.4200,
  "thrtl": 0.7300,
  "brake": 0.0000,
  "hb": false,
  "gu": false,
  "gd": false
}
```

#### JSON Field Specifications

| Key | Type | Valid Range | Description |
| :--- | :--- | :--- | :--- |
| `v` | Integer | `1` | Wire protocol version identifier. |
| `seq` | Long | `0` to `2^63-1` | Monotonically increasing sequence number. |
| `t` | Long | Positive integer | Client timestamp in milliseconds since Unix epoch. |
| `steer` | Double | `[-1.0, 1.0]` | Normalized steering input (-1.0 = full left, +1.0 = full right). |
| `thrtl` | Double | `[0.0, 1.0]` | Normalized throttle input (0.0 = idle, 1.0 = wide open throttle). |
| `brake` | Double | `[0.0, 1.0]` | Normalized brake input (0.0 = released, 1.0 = maximum braking). |
| `hb` | Boolean | `true` / `false` | Handbrake state (mapped to Xbox Button A). |
| `gu` | Boolean | `true` / `false` | Gear Up shift paddle (mapped to Xbox Right Bumper). |
| `gd` | Boolean | `true` / `false` | Gear Down shift paddle (mapped to Xbox Left Bumper). |

### 2. Compact Binary Telemetry Format (24 Bytes)

For environments with packet loss or low bandwidth, the application supports a zero-allocation 24-byte binary structure:

```
 0                   1                   2                   3
 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|    Version    |        Sequence Number (Bytes 0-3)            |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                  Sequence Number (Bytes 4-7)                  |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                    Timestamp (Bytes 0-3)                      |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                    Timestamp (Bytes 4-7)                      |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|        Steering (Int16)       |        Throttle (UInt16)      |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|         Brake (UInt16)        |    Buttons    |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
```

#### Memory Layout Breakdown

| Offset (Bytes) | Size (Bytes) | Data Type | Encoding | Field Description |
| :--- | :--- | :--- | :--- | :--- |
| `0` | 1 | `uint8` | Unsigned byte | Protocol version (`1`). |
| `1 - 8` | 8 | `int64` | Big-Endian | Monotonic sequence number. |
| `9 - 16` | 8 | `int64` | Big-Endian | Unix epoch timestamp in milliseconds. |
| `17 - 18` | 2 | `int16` | Big-Endian | Normalized steering: `[-32767, 32767]`. |
| `19 - 20` | 2 | `uint16` | Big-Endian | Normalized throttle: `[0, 65535]`. |
| `21 - 22` | 2 | `uint16` | Big-Endian | Normalized brake: `[0, 65535]`. |
| `23` | 1 | `uint8` | Bitmask | Buttons: Bit 0 = Handbrake, Bit 1 = Gear Up, Bit 2 = Gear Down. |

---

## Fail-Safe Safety Watchdog

To prevent runaway acceleration or steering lock if Wi-Fi disconnects during a race, the application implements a dedicated real-time watchdog:

1. **Monotonic Sequence Enforcement**:
   - Out-of-order, delayed, or duplicated UDP datagrams (`packet.SequenceNumber <= lastProcessedSequence`) are silently dropped.
2. **200 ms Timeout Interval**:
   - Monitored continuously on an asynchronous 25 ms evaluation loop.
   - If no valid packet arrives within the configured threshold (default: 200 ms), the bridge engages an automatic neutral state:
     - `Steering = 0.0`
     - `Throttle = 0.0`
     - `Brake = 0.0`
     - `Handbrake = false`
     - `GearUp = false`
     - `GearDown = false`
   - The virtual Xbox 360 controller instantly updates to neutral, halting the vehicle safely in-game.
   - The console HUD flags: `PHONE DISCONNECTED (WATCHDOG NEUTRAL ENGAGED)`.
3. **Instant Auto-Recovery**:
   - The moment the mobile device sends a fresh valid packet, normal control resumes immediately with zero manual intervention required.

---

## Installation and Execution Guide

### Step 1: Install the ViGEmBus Kernel Driver
The ViGEmBus driver creates the virtual Xbox 360 controller at the Windows kernel level.
1. Navigate to the `scripts/` directory.
2. Right-click `install_vigem_driver.bat` and select "Run as administrator".
3. Follow the installation wizard.
Alternatively, execute the standalone installer from `drivers/ViGEmBus_1.22.0_x64_x86_arm64.exe`.

### Step 2: Configure Windows Firewall
To permit inbound UDP datagrams on port 5000 (telemetry) and 5152 (discovery):
1. Right-click `scripts/allow_firewall.bat` and select "Run as administrator".
Alternatively, run this PowerShell command in an elevated terminal:
```powershell
New-NetFirewallRule -DisplayName "Phone Racing Wheel UDP Bridge" -Direction Inbound -Protocol UDP -LocalPort 5000,5152 -Profile Any -Action Allow
```

### Step 3: Run the Application

#### Option A: Running from Source (.NET 8 SDK)
```cmd
cd WindowsBridge
dotnet run -c Release
```

#### Option B: Using the Runner Script
Double-click `scripts/run.bat`. This automatically detects whether a precompiled binary or .NET SDK is available and starts the service.

#### Option C: Building a Standalone Single-File Executable
To create a self-contained executable that runs on any 64-bit Windows PC without requiring .NET SDK:
1. Double-click `scripts/build_self_contained.bat`.
2. The compiled binary will be placed at:
   `WindowsBridge/publish/WindowsBridge.exe`
3. You can transfer this single `.exe` file to any Windows 10/11 computer and run it directly.

---

## Verification via Windows Game Controllers (`joy.cpl`)

Before starting your racing simulator, verify that the virtual controller is recognized by the operating system:

1. Press `Win + R`, type `joy.cpl`, and press Enter.
2. The "Game Controllers" dialog opens.
3. Confirm that **"Controller (XBOX 360 For Windows)"** appears with status **"OK"**.
4. Select the controller and click **"Properties"**:
   - Tilt your mobile phone left and right: The crosshair in the "X Axis / Y Axis" box moves smoothly along the horizontal X axis.
   - Apply throttle on the phone: The "Z Axis" slider responds proportionally.
   - Apply brake on the phone: The "Z Rotation" slider responds proportionally.
   - Tap Handbrake on the phone: Button 1 (A) illuminates.
   - Tap Gear Up on the phone: Button 6 (RB) illuminates.
   - Tap Gear Down on the phone: Button 5 (LB) illuminates.

---

## In-Game Setup and Configuration

### Forza Horizon 4 / 5 & Forza Motorsport
1. Launch Forza Horizon.
2. Navigate to **Settings -> Controls -> Controller**.
3. The game automatically binds the virtual Xbox 360 controller.
4. Recommended in-game tuning under **Advanced Controls**:
   - **Steering Axis Deadzone Inside**: `0` (the mobile app handles deadzone math).
   - **Steering Axis Deadzone Outside**: `100`.
   - **Steering Linearity**: `50` (linear; exponential curve tuning is handled on the mobile device).
   - **Acceleration Axis Deadzone Inside**: `0`.
   - **Acceleration Axis Deadzone Outside**: `100`.
   - **Deceleration Axis Deadzone Inside**: `0`.
   - **Deceleration Axis Deadzone Outside**: `100`.

### Assetto Corsa, F1 23/24, Project CARS
- Select **Wheel / Gamepad -> Xbox 360 Controller**.
- Verify axis assignments: Steer = Left Thumb X, Throttle = RT, Brake = LT, Shift Up = RB, Shift Down = LB.

---

## Network Configuration and Latency Optimization

### Network Topology Comparison

| Mode | Setup Requirement | Expected Latency | Recommended Scenario |
| :--- | :--- | :--- | :--- |
| **Direct Phone Hotspot** | Turn on phone hotspot, connect Windows laptop to phone Wi-Fi | 1 ms to 4 ms | Maximum performance, zero router jitter, portable/LAN play |
| **5 GHz Local Wi-Fi Router** | Connect both devices to 5 GHz Wi-Fi SSID | 3 ms to 8 ms | Standard home setup with modern router |
| **2.4 GHz Local Wi-Fi Router** | Connect both devices to 2.4 GHz Wi-Fi | 10 ms to 35 ms | Functional, but prone to packet jitter and interference |

### Best Practices for Lowest Latency
1. **Use Phone Hotspot Mode**:
   - Turning on your phone's personal Wi-Fi hotspot and connecting the Windows PC directly bypasses router hops and airtime contention entirely, yielding sub-3ms telemetry.
2. **Set Windows Network Profile to Private**:
   - Windows often marks hotspot networks as "Public", which restricts inbound UDP packets. In Windows Settings, set the hotspot Wi-Fi network profile to "Private".
3. **5 GHz Over 2.4 GHz**:
   - If using a router, ensure both phone and laptop are on 5 GHz to avoid Bluetooth and microwave interference typical on 2.4 GHz.

---

## Command Line Arguments

`WindowsBridge.exe` accepts optional positional parameters for custom networking setups:

```cmd
WindowsBridge.exe [port] [watchdogTimeoutMs]
```

- `port`: Inbound UDP telemetry port (Default: `5000`).
- `watchdogTimeoutMs`: Watchdog fail-safe timeout in milliseconds (Default: `200`).

Example:
```cmd
WindowsBridge.exe 5050 150
```

---

## Troubleshooting Guide

| Issue | Root Cause | Resolution |
| :--- | :--- | :--- |
| **Console displays: "WAITING FOR PHONE"** | Windows Firewall is dropping inbound UDP datagrams | Run `scripts/allow_firewall.bat` as Administrator. Check that your network connection profile is set to Private. |
| **Auto-discovery does not list laptop in phone app** | UDP broadcast packets (port 5152) are blocked by router | Type `ipconfig` in Command Prompt on Windows. Note your IPv4 address (e.g. `192.168.1.45`), open the NET dialog in the mobile app, enter the IP manually, and tap Connect. |
| **"ViGEmBus driver not found" warning on launch** | The ViGEmBus kernel driver is not installed | Run `scripts/install_vigem_driver.bat` as Administrator, or install `drivers/ViGEmBus_1.22.0_x64_x86_arm64.exe`. Restart the bridge application. |
| **Controls not responding in Forza Horizon** | Another controller or steering wheel has active focus in-game | Disconnect physical steering wheels or secondary gamepads, or switch controller profile to Gamepad 1 in Forza settings. |
| **High latency or stuttering gauges** | 2.4 GHz Wi-Fi interference or background network load | Switch to a 5 GHz network or use the phone hotspot connection mode. |
| **Port 5000 already in use** | Another local process bound UDP port 5000 | Launch the bridge with an alternative port: `WindowsBridge.exe 5050 200`, and set port 5050 in the phone application. |
