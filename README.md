# SmartLamp Studio

A high-performance desktop application and CLI suite for instant local control of Tuya-compatible smart lighting devices, built entirely in C# / .NET 9.

[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux-0078D4.svg)]()
[![Framework](https://img.shields.io/badge/Framework-.NET%209%20%7C%20Avalonia%20%7C%20WPF-512BD4.svg)]()
[![Pure C#](https://img.shields.io/badge/Architecture-100%25%20C%23-239120.svg)]()
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

| Windows (WPF Native) | Linux (Avalonia UI / KDE Plasma) |
| :---: | :---: |
| ![Windows Interface](src/Assets/Screenshot.png?v=2026) | ![Linux Interface](src/Assets/Screenshot_Linux.png?v=2026) |

---

## Features

- **Instant Control**: Direct local TCP socket communication with sub-35ms latency via CLI shortcuts.
- **Cross-Platform**:
  - **Windows 10/11**: Ultra-compact native WPF single-file executable (~900 KB).
  - **Linux (Ubuntu, CachyOS, Arch, Debian)**: Modern Avalonia UI desktop application & native standalone CLI binary.
- **Pure C# Architecture**: 100% native .NET implementation with zero Python or runtime dependencies.
- **Hybrid Connectivity**: Direct LAN protocol (v3.1–v3.5) with automatic Tuya Cloud OpenAPI fallback.
- **Smart Home Bridges**: Built-in endpoints for Google Home (port 8088) and MQTT (port 1883).
- **Subnet Auto-Discovery**: Automatic UDP scanning for local Tuya devices.
- **Custom Presets & Hotkeys**: User-defined lighting scenes, global hotkeys, and system tray integration.

---

## CLI Usage

Control your lighting directly from the command line or automation scripts:

| Command (Windows) | Command (Linux) | Description |
| :--- | :--- | :--- |
| `SmartLampApp.exe --toggle` | `smartlamp --toggle` | Toggles lamp power state |
| `SmartLampApp.exe --on` | `smartlamp --on` | Turns the lamp ON |
| `SmartLampApp.exe --off` | `smartlamp --off` | Turns the lamp OFF |
| `SmartLampApp.exe --brightness <1-100>` | `smartlamp --brightness <1-100>` | Sets brightness percentage |
| `SmartLampApp.exe --temp <2700-6500>` | `smartlamp --temp <2700-6500>` | Sets color temperature in Kelvin |
| `SmartLampApp.exe --color <#HEX>` | `smartlamp --color <#HEX>` | Sets RGB color (e.g. `#FF0080`) |
| `SmartLampApp.exe --status` | `smartlamp --status` | Outputs current device state JSON |
| `SmartLampApp.exe --install` | — | Registers Start Menu & autostart (Windows) |

---

## Standalone Library: `TuyaNet.Native`

The Tuya protocol implementation is factored into a standalone .NET class library (`net9.0;net8.0`) available in `libs/TuyaNet.Native/`:

```csharp
using TuyaNet.Native;
using TuyaNet.Native.Models;

var device = new TuyaDevice("192.168.1.100", "device_id", "local_key", TuyaProtocolVersion.V35);
await device.TogglePowerAsync();
```

---

## Building from Source

**Prerequisites**: [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

```bash
# Windows Desktop App (WPF, standalone)
dotnet publish src/SmartLampApp.csproj -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=true -o dist

# Linux CLI (Single-file native binary)
dotnet publish src/SmartLamp.Cli/SmartLamp.Cli.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true -o dist/cli-linux-x64

# Linux Desktop App (Avalonia UI)
dotnet publish src/SmartLamp.Desktop/SmartLamp.Desktop.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o dist/desktop-linux-x64

# Standalone Protocol Library (NuGet)
dotnet pack libs/TuyaNet.Native/TuyaNet.Native.csproj -c Release -o releases
```

---

## License

Released under the [MIT License](LICENSE). Copyright (c) 2026 [Murr](https://github.com/vtstv).
