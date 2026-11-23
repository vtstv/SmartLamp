## SmartLamp Studio v3.5.2

### 🚀 What's New & Highlights

- **⚡ Event-Driven Reactive Push Architecture**:
  - Implemented continuous background push frame listener (`ReceiveLoopAsync`) in `TuyaNet.Native`: spontaneously receives incoming `0x08 Status` frames from the lamp (< 15 ms latency) without active DpQuery polling!
  - Added lightweight `0x09 Heartbeat` keep-alive (16 bytes, 0 dynamic memory allocations, 0% CPU overhead on Tuya microcontrollers).
  - Eliminated `DrainAvailableBytes()` to ensure incoming status notifications are never dropped.

- **🪟 Window-Focused Session Lifecycle**:
  - **Active Session (Window Visible)**: Holds local TCP connection, listens for live status pushes, and sends 15-second heartbeats. Active DpQuery polling is reduced to 0 Hz!
  - **Passive Idle (Minimized / In Tray)**: Cleanly closes TCP socket (`TCP FIN`), freeing the lamp's single hardware socket for the official Smart Life mobile app and Home Assistant.
  - **Tray Hotkey Grace Period**: Global hotkey executes instant toggle in 25–35 ms, with a 15-second grace period before releasing the socket if the window stays closed.

- **🛡️ Elimination of Status Flickering & SSD Wear**:
  - Background status updates are now silent (`showConnecting: false`), keeping the status pill rock-solid green `ONLINE` without flashing blue `CONNECTING...`.
  - Removed synchronous `ConfigManager.Save` calls from all status-reading code paths, saving SSDs from up to 34,500+ unnecessary writes per day.
  - Set default device priority to `local_first`, preventing Tuya Cloud developer API quotas from burning out.

- **🧪 Expanded Unit Test Suite**:
  - Added comprehensive loopback tests for `SendHeartbeatAsync` and `OnStatusPushed` across both v3.3 and v3.5 framing, bringing automated tests to 125 passing tests.
