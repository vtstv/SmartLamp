# SmartLamp Studio

SmartLamp Studio is an ultra-fast, lightweight Windows desktop application (.NET 9 WPF) and CLI tool for instant control of Tuya-compatible smart lamps, bulbs, and home automation devices.

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D4.svg)]()
[![Framework](https://img.shields.io/badge/Framework-.NET%209%20WPF-512BD4.svg)]()
[![Engine](https://img.shields.io/badge/TuyaNet.Native-Pure%20C%23-239120.svg)]()
[![Protocol](https://img.shields.io/badge/Protocols-v3.1%20%7C%20v3.3%20%7C%20v3.4%20%7C%20v3.5-4285F4.svg)]()
[![Zero Dependencies](https://img.shields.io/badge/Python-Zero%20Dependencies-brightgreen.svg)]()
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

![SmartLamp Studio Interface](src/Assets/Screenshot.png?v=2026)

---

## Highlights

- **Pure C# Native Engine**: 100% native .NET implementation without Python bridges, external wrappers, or heavy background daemons.
- **Instant Control**: Near-instantaneous response times (~25–35 ms cold execution via CLI shortcut flags like `--toggle`, `--on`, `--off`).
- **Tiny Footprint**: Portable standalone executable under **1 MB** (down from ~35 MB in legacy versions).
- **Hybrid Local / Cloud Dispatching**:
  - **Local LAN Protocol**: Direct TCP socket communication with AES-128-GCM / AES-128-ECB encryption and dynamic session key negotiation.
  - **Cloud OpenAPI Fallback**: Automatic Tuya Cloud API dispatching if the device is unreachable locally or on a different subnet.
- **Standalone Library (`TuyaNet.Native`)**: Reusable .NET class library (`net9.0;net8.0`) implementing Tuya local protocols (3.1, 3.3, 3.4, 3.5), suitable for any .NET application.
- **Google Home & Assistant Integration**: Built-in HTTP webhook endpoint supporting Google Smart Home intents (`SYNC`, `QUERY`, `EXECUTE`) and local voice simulation.
- **Home Assistant & MQTT Bridge**: Embedded MQTT bridge (port 1883) for automated Home Assistant discovery and bidirectional control.
- **Subnet Auto-Discovery**: Fast UDP broadcast scanner for local Tuya device detection and multi-device management.
- **Custom User Presets**: One-click quick-save chips for brightness, color temperature, and RGB colors.
- **System Integration**: One-click installation to `%LocalAppData%`, Start Menu integration, Windows Autostart (system tray launch), and DPAPI credential encryption.

---

## Command Line Interface (CLI)

The application supports direct command-line arguments for ultra-fast scripts, desktop shortcuts, and automation workflows without launching the GUI:

| Command | Description | Latency |
| :--- | :--- | :--- |
| `SmartLampApp.exe --toggle` | Toggles lamp power state based on real-time device status | ~25–35 ms |
| `SmartLampApp.exe --on` | Powers the lamp ON | ~25–35 ms |
| `SmartLampApp.exe --off` | Powers the lamp OFF | ~25–35 ms |
| `SmartLampApp.exe --brightness <1-100>` | Adjusts brightness percentage | ~30 ms |
| `SmartLampApp.exe --temp <2700-6500>` | Adjusts color temperature in Kelvin | ~30 ms |
| `SmartLampApp.exe --color <#HEX>` | Sets RGB color by hex string (e.g. `#FF0080`) | ~30 ms |
| `SmartLampApp.exe --status` | Queries and outputs current JSON device state | ~30 ms |
| `SmartLampApp.exe --install` | Installs application to system and registers Start Menu shortcuts | — |
| `SmartLampApp.exe --uninstall` | Removes system shortcuts and registration entries | — |
| `SmartLampApp.exe --autostart` | Background launch mode for Windows startup (system tray) | — |

---

## Standalone Library: `TuyaNet.Native`

The core protocol implementation is isolated in `libs/TuyaNet.Native/` and multi-targets `net9.0;net8.0`. It contains zero UI dependencies and can be used in your own .NET projects:

```csharp
using TuyaNet.Native;
using TuyaNet.Native.Models;

// Initialize device client
var device = new TuyaDevice(
    ipAddress: "192.168.0.68",
    deviceId: "bf5844a34bb0422e67tmbi",
    localKey: "your_16_byte_local_key",
    version: TuyaProtocolVersion.V35
);

// Toggle power
bool newState = await device.TogglePowerAsync();

// Set specific DP (e.g. DP 20 for power)
await device.SetPowerAsync(true);

// Set brightness (e.g. DP 22)
await device.SetDpsAsync(new Dictionary<string, object> { ["22"] = 500 });
```

### UDP Discovery:
```csharp
using TuyaNet.Native.Discovery;

// Scan local network for Tuya devices (ports 6666, 6667, 7000)
var devices = await TuyaDiscovery.ScanAsync(timeout: TimeSpan.FromSeconds(3));
foreach (var dev in devices)
{
    Console.WriteLine($"Found {dev.DeviceId} at {dev.IpAddress} (v{dev.Version})");
}
```

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                 SmartLamp Studio (UI / CLI)                 │
└──────┬──────────────────────┬───────────────────────┬───────┘
       │                      │                       │
       ▼                      ▼                       ▼
┌──────────────┐      ┌──────────────┐      ┌─────────────────┐
│ MQTT Bridge  │      │ Google Home  │      │  TuyaProtocol   │
│  (Port 1883) │      │  (Port 8088) │      │   Dispatcher    │
└──────────────┘      └──────────────┘      └────────┬────────┘
                                                     │
                             ┌───────────────────────┴───────────────────────┐
                             │                                               │
                             ▼ (LAN direct socket)                           ▼ (Remote fallback)
                   ┌───────────────────┐                           ┌───────────────────┐
                   │  TuyaNet.Native   │                           │  Tuya Cloud API   │
                   │ (v3.1, 3.3, 3.5)  │                           │   (OpenAPI HMAC)  │
                   └─────────┬─────────┘                           └─────────┬─────────┘
                             │ Local TCP (port 6668)                         │ HTTPS REST
                             ▼                                               ▼
                   ┌───────────────────┐                           ┌───────────────────┐
                   │  Tuya Smart Lamp  │                           │    Tuya Cloud     │
                   └───────────────────┘                           └───────────────────┘
```

---

## Building from Source

### Prerequisites
- Windows 10/11 x64
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

*(Python is **not required**. The legacy Python bridge and PyInstaller dependencies have been completely removed).*

### Build Commands

Build the desktop application:
```powershell
dotnet build src/SmartLampApp.csproj
```

Build the standalone class library:
```powershell
dotnet build libs/TuyaNet.Native/TuyaNet.Native.csproj
```

Publish single-file portable release executable:
```powershell
dotnet publish src/SmartLampApp.csproj -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
```

---

## Security

Sensitively stored access keys and device credentials are encrypted locally using the Windows Data Protection API (DPAPI). Local network keys never leave your machine.

---

## License

This project is licensed under the [MIT License](LICENSE).

Copyright (c) 2026 [Murr](https://github.com/vtstv).
