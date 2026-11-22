## SmartLamp Studio v3.5.1

### 🚀 What's New & Highlights

- **🎯 Accurate Power & Online Status Detection**:
  - Fixed Tuya Cloud status fallback: now explicitly queries `/v1.0/iot-03/devices/{id}` and validates `result.online`. Unpowered/offline lamps are never falsely marked as `ONLINE` or `POWERED ON` from stale cloud data.
  - Added full support for Tuya v3.5 asynchronous status frames wrapping DPS in `root.data.dps`.
  - Implemented socket stream byte drainage in `TuyaSession` and secondary acknowledgement frame consumption in `TuyaDevice.SetDpsAsync`, preventing leftover TCP bytes from corrupting subsequent status queries.

- **🔄 Background Status Polling & Window Activation Refresh**:
  - Added background polling timer (5s when window is active, 15s when minimized/in tray) so external state changes (wall switches, Smart Life mobile app, Home Assistant) reflect automatically without manual refresh.
  - Instantly refreshes status upon window restoration, activation, or opening from the system tray (`Activated` / `StateChanged`).
  - Eliminated duplicate parallel startup status queries that previously caused single-socket contention on Tuya hardware.

- **🛡️ Optimistic UI Rollback**:
  - Safely rolls back the power button state if a network command fails, preventing the UI from showing `POWERED ON` when the physical lamp did not respond.

- **💻 CLI DPAPI Decryption Fix**:
  - SmartLamp CLI now automatically decrypts protected DPAPI local keys, allowing `--status` and direct control to execute smoothly from terminal/scripts.

- **🧪 Expanded Test Suite**:
  - Added comprehensive test coverage in `TuyaDeviceTests.cs`, bringing automated unit tests to 119 passing tests.
