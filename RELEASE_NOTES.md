## SmartLamp Studio v3.5.0

### 🚀 What's New & Highlights

- **📲 Mobile QR Code Authorization (Smart Life / Tuya Smart)**:
  - Add lamps directly by scanning a QR code with the Smart Life or Tuya Smart mobile app — no complex Tuya Developer Cloud account or OpenAPI keys needed!
  - Implements Tuya's device-sharing auth flow with dynamic signature generation, encrypted payload exchange, and automatic device list retrieval.

- **🌐 Auto-LAN Discovery & Smart IP Switcher**:
  - Automatic LAN IP resolver (`⚡ Auto-LAN` / `Wi-Fi Scan`) that finds the exact local subnet address of lamps added via cloud.
  - Local/External IP toggle (`LAN (Local IP)` vs `Cloud/WAN`) on each device card with instant failover, delivering ultra-low LAN response times (~25–35 ms).

- **💾 Settings Backup & Restore**:
  - One-click export and import of full configuration and device profiles to a portable JSON backup file.

- **🛠️ Refined Device Management UI**:
  - Optimized layout for Russian, German, and English locales: separated Device ID and IP rows, eliminating button overlaps.
  - Dedicated "New Lamp" drafting mode with an explicit Cancel (`✖️ Отмена`) button, preventing accidental deletion of configured devices.

- **🐛 Startup Power Sync & Illumination Fix**:
  - Resolved an issue where launching the application could inadvertently send brightness/temperature packets and illuminate a powered-off lamp.
  - Reentrant UI update lock depth counter guarantees that background sliders never dispatch socket commands while the lamp is powered off.

- **⚡ Core & Architecture Improvements**:
  - Upgraded standalone `TuyaNet.Native` protocol library to v1.0.2 with enhanced status parsing and 111 passing automated unit tests.
  - Full compliance with strict modular architecture (< 500 lines per file).
