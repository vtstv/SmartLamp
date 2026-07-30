# 💡 SmartLamp Studio (Windows 11 C# WPF Edition)

Professional Windows 11 Desktop Application for controlling Tuya smart lamps with **Google Smart Home / Google Assistant Integration**, **100% Self-Contained Binary**, and **Wi-Fi Auto-Discovery**.

Created by **[Murr](https://github.com/vtstv)**.

[![Platform](https://img.shields.io/badge/Platform-Windows%2011-0078D4.svg)]()
[![C# WPF](https://img.shields.io/badge/Framework-.NET%209%20WPF-512BD4.svg)]()
[![Google Home](https://img.shields.io/badge/Integration-Google%20Smart%20Home-4285F4.svg)]()
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

## ✨ Features & Architecture

- **📁 Clean Project Structure in Root:**
  - All C# WPF source files (`SmartLampApp.csproj`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `GoogleHomeService.cs`, `TuyaProtocol.cs`, `ConfigManager.cs`, `App.xaml`) are located directly in the project root!
- **🌐 Google Smart Home & Assistant Integration:**
  - Includes a built-in **Google Smart Home Local Bridge HTTP/Webhook Server** (`http://localhost:8088/google-smart-home/`) supporting Google Smart Home intents (`SYNC`, `QUERY`, `EXECUTE`).
  - Includes a **Google Assistant Voice Command Simulator** directly inside the app!
- **🛡️ 100% Self-Contained Binary (Fixed Windows Launching):**
  - Compiled with `<SelfContained>true</SelfContained>`. Runs out-of-the-box on ANY Windows 10/11 system without requiring pre-installed .NET runtimes!
  - Includes global crash logging (`AppDomain.UnhandledException` & `DispatcherUnhandledException` log to `crash.log`).
- **🔍 Wi-Fi Auto-Discovery & Multi-Device Selector:**
  - Subnet UDP Broadcast Scanner to auto-discover Tuya devices on Wi-Fi.
  - Multi-device switcher & Group Control.
- **🎨 Modern Glassmorphic UI & Interactive Color Picker:**
  - Ambient Light Indicator, zero scrollbars, custom title bar, and full RGB Color Picker.

---

## 🚀 Executable Location

Run the standalone executable:
**[dist/SmartLampApp.exe](file:///d:/Dev/SmartLampTest/dist/SmartLampApp.exe)** *(Self-Contained Portable Binary)*

---

## 🛠️ Building from Source

```cmd
build.bat
```
*(Requires .NET 9 SDK)*.

---

## 📄 License

Licensed under the [MIT License](LICENSE).

Created with ❤️ by **[Murr](https://github.com/vtstv)**.
