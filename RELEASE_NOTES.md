## SmartLamp Studio v3.3.2

### 🚀 What's New & Fixed
- **Device Lifecycle & Socket Isolation**: Completely resolved an issue where deleted devices continued receiving LAN commands via legacy fallback credentials and open TCP socket connections.
- **Integration Tabs Auto-Visibility**: Google Home and Home Assistant & MQTT navigation tabs now strictly respect settings on application startup and remain hidden when disabled.
- **Intelligent Cloud Key Diagnostics**:
  - Added warning for truncated Tuya Access Keys (less than 32 characters).
  - Clear guidance when Device ID is empty before fetching cloud keys.
  - Added human-readable diagnostics for Tuya error `28841002` (IoT Core trial subscription expired) with renewal instructions.
- **Smooth Legacy Migration**: Automatic seamless migration of legacy single-lamp settings into the multi-device database.
