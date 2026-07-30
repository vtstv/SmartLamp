# SmartLamp Studio

SmartLamp Studio is a high-performance Windows desktop application for managing Tuya smart lighting devices with native **Google Home**, **MQTT**, and **Command-Line Interface (CLI)** integration.

[![Platform](https://img.shields.io/badge/Platform-Windows%2011-0078D4.svg)]()
[![Framework](https://img.shields.io/badge/Framework-.NET%209%20WPF-512BD4.svg)]()
[![Protocol](https://img.shields.io/badge/Protocol-Tuya%20v3.5-4285F4.svg)]()
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

![SmartLamp Studio Interface](src/Assets/Screenshot.png)

---

## Features

- **Persistent Protocol Engine**: Uses an embedded daemon for persistent TCP connections to Tuya 3.5 devices, achieving low-latency control (~10ms) and reliable command dispatching.
- **Google Home & Assistant Integration**: Built-in HTTP webhook endpoint supporting Google Smart Home intents (`SYNC`, `QUERY`, `EXECUTE`) and local voice command simulation.
- **Home Assistant & MQTT Bridge**: Embedded MQTT bridge (port 1883) for automated Home Assistant discovery and bidirectional control.
- **Headless CLI Execution**: Full command-line interface supporting silent execution (`--toggle`, `--on`, `--off`, `--brightness`, `--temp`, `--color`) without bringing up the GUI.
- **Wi-Fi Auto-Discovery**: Subnet UDP broadcast scanner for local Tuya device detection and multi-device management.
- **System Integration**: One-click installation to `%LocalAppData%`, Start Menu integration, Windows Autostart (system tray launch), and DPAPI credential encryption.

---

## Command Line Interface (CLI)

The application supports direct command-line arguments for scripts, shortcuts, and automation workflows:

| Command | Description |
| :--- | :--- |
| `SmartLampApp.exe --toggle` | Toggles lamp power state based on real-time device status |
| `SmartLampApp.exe --on` | Powers the lamp ON |
| `SmartLampApp.exe --off` | Powers the lamp OFF |
| `SmartLampApp.exe --brightness <1-100>` | Adjusts brightness percentage |
| `SmartLampApp.exe --temp <2700-6500>` | Adjusts color temperature in Kelvin |
| `SmartLampApp.exe --color <#HEX>` | Sets RGB color by hex string |
| `SmartLampApp.exe --install` | Installs application to system and registers Start Menu shortcuts |
| `SmartLampApp.exe --uninstall` | Removes system shortcuts and registration entries |
| `SmartLampApp.exe --autostart` | Background launch mode for Windows startup |

---

## Building from Source

### Prerequisites
- Windows 10/11 x64
- .NET 9.0 SDK

*(Note: The Tuya protocol daemon `tuya_bridge.exe` is pre-compiled and embedded directly into the application resources. Python is **not required** to build or run the application. Python 3.10+ with `tinytuya` and `PyInstaller` is only needed if you modify `src/TuyaBridge/tuya_bridge.py` source code).*

### Build Command
Execute the build script in the repository root:
```cmd
build.bat
```
The compiled single-file executable will be generated at `dist/SmartLampApp.exe`.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    SmartLamp Studio UI                      │
└──────┬──────────────────────┬───────────────────────┬───────┘
       │                      │                       │
       ▼                      ▼                       ▼
┌──────────────┐      ┌──────────────┐      ┌─────────────────┐
│ MQTT Bridge  │      │ Google Home  │      │  Tuya Protocol  │
│  (Port 1883) │      │  (Port 8088) │      │  Daemon Engine  │
└──────────────┘      └──────────────┘      └────────┬────────┘
                                                     │ Local TCP
                                                     ▼
                                            ┌─────────────────┐
                                            │ Tuya Smart Lamp │
                                            └─────────────────┘
```

---

## Security

Sensitively stored access keys and device credentials are encrypted locally using the Windows Data Protection API (DPAPI).

---

## License

This project is licensed under the [MIT License](LICENSE).

Copyright (c) 2026 [Murr](https://github.com/vtstv).
