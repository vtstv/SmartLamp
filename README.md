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
- **Rooms & Groups Management**: Group multiple lamps by room/zone with custom sync rules (1:1 synchronized or proportional dimming, master power).
- **Cross-Platform**:
  - **Windows 10/11**: Ultra-compact native WPF single-file executable (~900 KB).
  - **Linux (Ubuntu, CachyOS, Arch, Debian)**: Modern Avalonia UI desktop application & native standalone CLI binary.
- **Pure C# Architecture**: 100% native .NET implementation with zero Python or runtime dependencies.
- **Hybrid Connectivity**: Direct LAN protocol (v3.1–v3.5) with automatic Tuya Cloud OpenAPI fallback.
- **Smart Home Bridges**: Built-in endpoints for Google Home (port 8088) and MQTT (port 1883).
- **Subnet Auto-Discovery**: Automatic UDP scanning for local Tuya devices.
- **Custom Presets & Hotkeys**: User-defined lighting scenes, global hotkeys, and system tray integration.

---

## Installation & Downloads

Pre-built binaries, installers, and native packages for all major operating systems and architectures are available on the **[GitHub Releases](https://github.com/vtstv/SmartLamp/releases)** page:

### Windows 10 / 11 (x64)
- **Installer (Recommended)**: Download and run `SmartLampStudio_Setup_v3.3.1_win-x64.exe` (automatic wizard, Start Menu, Desktop shortcuts, autostart toggle, and uninstallation).
- **Portable (Standalone)**: Download `SmartLampApp_v3.3.1.exe` or `SmartLampStudio_v3.3.1_win-x64.zip` (single executable, no installation needed).

### Debian / Ubuntu (x86_64 & ARM64)
```bash
# Download and install the .deb package (amd64 or arm64)
sudo apt install ./smartlamp-studio_3.3.1_amd64.deb
# or on Raspberry Pi / ARM64:
sudo apt install ./smartlamp-studio_3.3.1_arm64.deb
```

### CachyOS / Arch Linux (x86_64 & ARM64)
```bash
# Install native pacman package directly
sudo pacman -U ./smartlamp-studio-3.3.1-1-x86_64.pkg.tar.zst
# or on aarch64:
sudo pacman -U ./smartlamp-studio-3.3.1-1-aarch64.pkg.tar.zst
```

### Universal Linux Tarball (Ubuntu, Fedora, CachyOS, openSUSE)
```bash
tar -xvf SmartLampStudio_v3.3.1_linux-x64.tar.gz
cd SmartLampStudio
./install.sh
```

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

### Quick Build (Solution)
```bash
# Build all projects in the solution
dotnet build SmartLamp.slnx
```

### Unified Build & Packaging Scripts

#### Windows (PowerShell):
```powershell
# Build and package all targets for release
.\scripts\build.ps1 -Target All -Pack

# Build only Windows WPF application
.\scripts\build.ps1 -Target Windows -Pack

# Clean build artifacts
.\scripts\clean.ps1
```

#### Linux & macOS (Bash):
```bash
# Build and package all targets (deb, pacman, tarball)
./scripts/build.sh --target all --pack

# Deep clean workspace
./scripts/clean.sh
```

---

## License

Released under the [MIT License](LICENSE). Copyright (c) 2026 [Murr](https://github.com/vtstv).
