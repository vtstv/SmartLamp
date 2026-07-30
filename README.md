# 💡 SmartLamp Studio
Desktop Application for controlling Tuya smart lamps with **Google Smart Home / Google Assistant Integration**, **MQTT Bridge** and **Wi-Fi Auto-Discovery**.

[![Platform](https://img.shields.io/badge/Platform-Windows%2011-0078D4.svg)]()
[![C# WPF](https://img.shields.io/badge/Framework-.NET%209%20WPF-512BD4.svg)]()
[![Google Home](https://img.shields.io/badge/Integration-Google%20Smart%20Home-4285F4.svg)]()
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

## ✨ Features & Architecture

- **⚡ High Performance Tuya 3.5 Protocol Engine:**
  - Uses an internal daemon HTTP server for persistent TCP socket connections to Tuya devices, dropping latency to **~10ms** and eliminating dropped commands.
  - Bundled as a single embedded executable resource inside `SmartLampApp.exe`.
- **🌐 Google Smart Home & Assistant Integration:**
  - Includes a built-in **Google Smart Home Local Bridge HTTP/Webhook Server** (`http://localhost:8088/google-smart-home/`) supporting Google Smart Home intents (`SYNC`, `QUERY`, `EXECUTE`).
  - Includes a **Google Assistant Voice Command Simulator** directly inside the app!
- **📡 Home Assistant & MQTT Bridge:**
  - Built-in MQTT bridge (`port 1883`) for easy integration with Home Assistant.
- **🔍 Wi-Fi Auto-Discovery & Multi-Device Selector:**
  - Subnet UDP Broadcast Scanner to auto-discover Tuya devices on Wi-Fi.
  - Multi-device switcher & Group Control.
- **🎨 Modern Glassmorphic UI & Interactive Color Picker:**
  - Ambient Light Indicator, zero scrollbars, custom title bar, and full RGB Color Picker.
 **🔒 DPAPI Secret Encryption:**
  - Automatically encrypts sensitive credentials using Windows Data Protection API (DPAPI).

---

## 🛠️ Building from Source

```cmd
build.bat
```
*(Requires .NET 9 SDK)*.

---

## 📄 License

Licensed under the [MIT License](LICENSE).

Copyright (c) 2026 **[Murr](https://github.com/vtstv)**.
