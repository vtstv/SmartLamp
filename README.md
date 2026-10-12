# SmartLamp Studio

A high-performance Windows desktop application and CLI tool for instant control of Tuya-compatible smart lighting devices, built entirely on .NET 9.

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D4.svg)]()
[![Framework](https://img.shields.io/badge/Framework-.NET%209%20WPF-512BD4.svg)]()
[![Pure C#](https://img.shields.io/badge/Architecture-100%25%20C%23-239120.svg)]()
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

![SmartLamp Studio Interface](src/Assets/Screenshot.png?v=2026)

---

## Features

- **Instant Control**: Direct local TCP connection with near-zero latency (~25–35 ms via CLI shortcuts).
- **Pure C# Architecture**: 100% native .NET 9 implementation with zero Python or runtime dependencies.
- **Lightweight & Portable**: Single-file standalone executable under 1 MB.
- **Hybrid Connectivity**: Direct LAN protocol (v3.1–v3.5) with automatic Tuya Cloud OpenAPI fallback.
- **Smart Home Bridges**: Built-in endpoints for Google Home (port 8088) and MQTT (port 1883).
- **Subnet Auto-Discovery**: Automatic UDP scanning for local Tuya devices.
- **Custom Presets & Hotkeys**: User-defined lighting scenes, global hotkeys, and system tray integration.

---

## CLI Usage

Control your lighting directly from the command line or desktop shortcuts:

| Command | Description |
| :--- | :--- |
| `SmartLampApp.exe --toggle` | Toggles lamp power state |
| `SmartLampApp.exe --on` | Turns the lamp ON |
| `SmartLampApp.exe --off` | Turns the lamp OFF |
| `SmartLampApp.exe --brightness <1-100>` | Sets brightness percentage |
| `SmartLampApp.exe --temp <2700-6500>` | Sets color temperature in Kelvin |
| `SmartLampApp.exe --color <#HEX>` | Sets RGB color (e.g. `#FF0080`) |
| `SmartLampApp.exe --status` | Outputs current device state JSON |
| `SmartLampApp.exe --install` | Registers Start Menu shortcuts and startup entries |
| `SmartLampApp.exe --uninstall` | Removes shortcuts and system registration |

---

## Standalone Library: `TuyaNet.Native`

The Tuya protocol implementation is factored into a standalone .NET class library (`net9.0;net8.0`) available in `libs/TuyaNet.Native/`:

```csharp
using TuyaNet.Native;
using TuyaNet.Native.Models;

var device = new TuyaDevice("192.168.0.68", "device_id", "local_key", TuyaProtocolVersion.V35);
await device.TogglePowerAsync();
```

---

## Building from Source

**Prerequisites**: Windows 10/11 x64, [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

```powershell
# Build application
dotnet publish src/SmartLampApp.csproj -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=true -o dist

# Build library package
dotnet pack libs/TuyaNet.Native/TuyaNet.Native.csproj -c Release -o releases
```

---

## License

Released under the [MIT License](LICENSE). Copyright (c) 2026 [Murr](https://github.com/vtstv).
