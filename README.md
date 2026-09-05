# Phone Racing Wheel — Windows Bridge

Native C# / .NET 8 companion application for Windows that receives real-time UDP controller telemetry from the Android phone racing wheel over local Wi-Fi or Phone Hotspot.

---

## 🏎️ Overview & Architecture

```
📱 Android Phone (Controller)
      │
      │  Local Wi-Fi or Phone Hotspot (UDP on Port 5000 @ ~60 Hz)
      ▼
💻 Windows Gaming Laptop (This Application)
      │
      ├── UdpServer (Reused socket, sequence validation, stale packet filter)
      ├── ConnectionWatchdog (200ms safety timeout -> neutral safeguard)
      ├── Statistics (PPS, packet drops, latency, phone IP)
      ├── DeviceDiscovery (1 Hz UDP announcement on port 5152)
      └── Dashboard (Low-CPU live telemetry HUD)
      │
      ▼ (Next Phase)
🎮 Virtual Controller (ViGEmBus / XInput)
      │
      ▼ (Next Phase)
🏁 Forza Motorsport / Horizon
```

> [!NOTE]
> **No Mac dependency at runtime**: Your Mac is strictly your code development machine. Once the phone app is installed and this `WindowsBridge` folder is on your Windows laptop, the complete controller system runs entirely between your Android phone and Windows laptop.

---

## 📁 Project Structure

```
WindowsBridge/
├── WindowsBridge.sln               # Visual Studio solution
├── WindowsBridge.csproj            # .NET 8 project (zero external dependencies)
├── Program.cs                      # Application entrypoint & service coordinator
│
├── Networking/
│   ├── UdpServer.cs                # Asynchronous UDP socket listener & packet validator
│   └── DeviceDiscovery.cs          # Local network beacon broadcaster on port 5152
│
├── Protocol/
│   └── ControllerPacket.cs         # JSON and 24-byte compact binary decoder & validator
│
├── Controller/
│   └── ControllerState.cs          # Thread-safe atomic controller state with safe neutral reset
│
├── Safety/
│   └── ConnectionWatchdog.cs       # 200 ms timeout safety monitor (snaps inputs to neutral)
│
├── Diagnostics/
│   └── Statistics.cs               # Throughput (PPS), latency, phone IP, and sequence gap tracking
│
├── UI/
│   └── Dashboard.cs                # Lightweight, flicker-free terminal dashboard
│
├── scripts/
│   ├── run.bat                     # One-click execution script (dotnet run)
│   ├── build_self_contained.bat    # Builds single-file standalone Windows x64 executable
│   └── allow_firewall.bat          # Configures Windows Firewall for UDP ports 5000 & 5152
│
└── README.md                       # This comprehensive documentation
```

---

## 📋 Prerequisites on the Windows Laptop

- **Operating System**: Windows 10 or Windows 11 (64-bit).
- **Network**: Connected to the **same Wi-Fi network** as the Android phone, OR connected to the **Phone Hotspot**.
- **.NET 8.0 SDK (x64)**:
  - **Direct Installer**: Download from [Microsoft .NET 8.0 SDK (x64)](https://dotnet.microsoft.com/download/dotnet/8.0)
  - **Or via Windows Terminal / PowerShell (Winget)**:
    ```powershell
    winget install Microsoft.DotNet.SDK.8
    ```
  - Verify installation in Command Prompt:
    ```cmd
    dotnet --version
    ```
    *(Should output `8.0.xxx`)*

---

## 📡 Network Protocol & Packet Format

### Protocol Specifications
- **Transport**: UDP datagrams over local network.
- **Default Port**: `5000` (configurable via CLI argument: `WindowsBridge.exe [port] [timeoutMs]`).
- **Update Rate**: ~60 packets/second.
- **Payload Format (JSON)**:
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
- **Field Definitions**:
  - `v`: Protocol version (`1`).
  - `seq`: Monotonically increasing sequence number (64-bit integer).
  - `t`: Client timestamp in milliseconds since epoch.
  - `steer`: Steering angle `[-1.0, 1.0]` (-1.0 = full left, +1.0 = full right).
  - `thrtl`: Throttle input `[0.0, 1.0]` (0.0 = off, 1.0 = full gas).
  - `brake`: Brake input `[0.0, 1.0]` (0.0 = off, 1.0 = full brake).
  - `hb`: Handbrake (`true` / `false`).
  - `gu`: Gear Up (`true` / `false`).
  - `gd`: Gear Down (`true` / `false`).
- **Alternative Binary Layout**: Supported out-of-the-box via `ControllerPacket.TryParse` (24 bytes).

---

## 🛡️ Packet Validation & Safety Watchdog

1. **Validation & Filtering**:
   - Rejects packets with invalid protocol versions.
   - Rejects NaN, infinite, or out-of-range values.
   - Enforces monotonic sequence numbers (`seq > lastSeq`). Any delayed or duplicate UDP packets are ignored.
2. **200 ms Safety Watchdog**:
   - Monitored on a 25 ms timer.
   - If no valid packet arrives within **200 ms** (configurable):
     - Snaps all controls immediately to safe neutral:
       `Steering = 0.0`, `Throttle = 0.0`, `Brake = 0.0`, `Handbrake = false`, `GearUp = false`, `GearDown = false`.
     - Dashboard displays `PHONE DISCONNECTED (WATCHDOG NEUTRAL ENGAGED)`.
   - As soon as the phone resumes sending packets, control recovers automatically.

---

## 🚀 Exact Windows Setup & Build Workflow

Follow these 5 simple steps on your Windows laptop:

### Step 1: Install .NET 8.0 SDK on Windows
Download and run the installer from:
👉 **https://dotnet.microsoft.com/download/dotnet/8.0** *(select ".NET SDK x64")*
Or run in PowerShell / Command Prompt:
```powershell
winget install Microsoft.DotNet.SDK.8
```
*(After installing, open a new Command Prompt window and confirm `dotnet --version` outputs `8.0.x`)*

### Step 2: Copy the `WindowsBridge` Folder to Windows
Copy the entire `WindowsBridge\` folder from your Mac to your Windows laptop (e.g. `C:\Games\PhoneRacingWheel\WindowsBridge` or your Desktop).

### Step 3: Configure Windows Firewall (One-Time)
Right-click `scripts\allow_firewall.bat` and select **"Run as administrator"**.

*Or run this in PowerShell as Administrator:*
```powershell
New-NetFirewallRule -DisplayName "Phone Racing Wheel UDP Bridge" -Direction Inbound -Protocol UDP -LocalPort 5000,5152 -Profile Private -Action Allow
```

### Step 4: Build Self-Contained Standalone Executable
Double-click:
`scripts\build_self_contained.bat`

This will execute:
```cmd
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o .\publish
```
When finished, it produces:
📁 `WindowsBridge\publish\WindowsBridge.exe`

### Step 5: Run the Windows Bridge
You can now run either:
- Direct executable: Double-click `publish\WindowsBridge.exe`
- Or runner script: Double-click `scripts\run.bat`
- Or from Command Prompt:
  ```cmd
  .\publish\WindowsBridge.exe 5000 200
  ```

---

## 📶 Test Procedures

### TEST A: Normal Wi-Fi Router
1. Ensure both your Android phone and Windows laptop are connected to the same home/office Wi-Fi router.
2. Start the Windows Bridge on your laptop (`scripts\run.bat`).
3. Launch the **Phone Racing Wheel** app on your phone.
4. Tap the **NET** status badge in the top bar.
5. Your Windows laptop will appear under **"DISCOVERED WINDOWS LAPTOPS"**. Tap **"CONNECT"**.
6. (If broadcast discovery is blocked by your router, check your laptop's IP via `ipconfig` e.g. `192.168.1.50`, type it into the IP field and tap **CONNECT**).
7. Tilt your phone left/right and forward/backward: verify that **Steering**, **Throttle**, **Brake**, and **Buttons** update live at **~60 packets/sec** on the Windows console dashboard.

### TEST B: Phone Hotspot Setup (No Router Needed!)
1. On your Android phone, turn **Personal Hotspot (Wi-Fi Hotspot)** ON.
2. On your Windows laptop, connect to your phone's Wi-Fi Hotspot network.
3. Open Command Prompt on Windows and type `ipconfig`. Look at your Wi-Fi adapter IPv4 address (it will usually be `192.168.43.x` or similar).
4. Run the Windows Bridge: `scripts\run.bat`.
5. Launch the **Phone Racing Wheel** app on the phone.
6. Tap **NET**, enter the laptop's IP address (from step 3), port `5000`, and tap **CONNECT**.
7. Observe live telemetry updating smoothly at ~60 pps directly between the phone and laptop with zero internet or router required!

---

## 📦 Building a Self-Contained Standalone Executable

To compile a single `.exe` file that can be transferred and executed on any Windows 10/11 x64 PC without installing the .NET SDK:

Run on any computer with .NET 8 SDK:
```cmd
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o .\publish
```

The output executable will be created at:
`publish\WindowsBridge.exe`

Copy the `publish\` folder directly to your Windows laptop and double-click `WindowsBridge.exe`.

---

## 🔍 Troubleshooting

| Issue | Cause | Solution |
| :--- | :--- | :--- |
| **Status remains WAITING FOR PHONE** | Windows Firewall is blocking inbound UDP | Run `scripts\allow_firewall.bat` as Administrator. Ensure the network profile is set to "Private" in Windows Settings. |
| **Auto-discovery doesn't find PC** | Router/Hotspot blocks UDP broadcasts (client isolation) | Find your Windows laptop IP via `ipconfig` (e.g. `192.168.43.100` or `192.168.1.50`) and enter it manually into the phone app's NET dialog. |
| **Packet rate is lower than 60 pps** | High 2.4GHz Wi-Fi interference | Switch your Wi-Fi router / phone hotspot to 5GHz band. |
| **Port 5000 in use** | Another service is using port 5000 | Launch on a custom port: `dotnet run -c Release -- 5050 200`, and set port `5050` in the phone app. |
