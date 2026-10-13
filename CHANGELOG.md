# Changelog

All notable changes to **SmartLamp Studio** will be documented in this file.

## [3.3.3] - 2026-10-13

### Improved
- **Responsive Layout & Adaptive Sizing**: Removed rigid fixed-width buttons across Dashboard, Rooms, and Settings tabs in favor of flexible grid columns and auto-sizing `MinWidth` with balanced padding.
- **Compact UI Microcopy**: Streamlined Russian and German button labels and dropdown options to completely eliminate text clipping and awkward word wraps.
- **Informative ToolTips**: Added localized tooltips (EN / DE / RU) to all action buttons for instant guidance on hover.

## [3.3.2] - 2026-10-13

### Fixed
- **Device Removal & Socket Isolation**: Completely resolved an issue where removing a device left residual fallback credentials and active TCP sockets in memory.
- **Integration Tabs Auto-Visibility**: Google Home and Home Assistant & MQTT navigation tabs now strictly respect settings on startup and remain hidden when disabled.

### Improved
- **Tuya Cloud Key Diagnostics**: Added friendly warnings for truncated Access Keys (< 32 characters), empty Device ID guidance, and clear explanations for expired IoT Core subscriptions (`28841002`).
- **Legacy Device Migration**: Automatic migration of legacy single-lamp configs into the multi-device database.

## [3.3.1] - 2026-10-13

### Added
- **Complete UI Localization**: Full multi-language support (English, German, Russian) across all tabs, buttons, dialogs, and notifications.

## [3.3.0] - 2026-10-13

### Added
- **Multi-Device & Room Groups**: Support for multi-lamp management, room zones, synchronized / proportional dimming, and mass power control.
