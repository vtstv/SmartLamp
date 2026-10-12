// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

namespace TuyaNet.Native.Models
{
    /// <summary>
    /// Descriptor for a device discovered via local UDP broadcast.
    /// </summary>
    public class TuyaDiscoveredDevice
    {
        /// <summary>IP address of the device on the local network.</summary>
        public string Ip { get; init; } = "";

        /// <summary>Unique Tuya Device ID (gwId).</summary>
        public string DeviceId { get; init; } = "";

        /// <summary>Protocol version reported by the device (e.g. "3.5", "3.4", "3.3").</summary>
        public string Version { get; init; } = "3.5";

        /// <summary>Tuya Product Key identifier.</summary>
        public string ProductKey { get; init; } = "";

        /// <summary>Raw JSON payload decoded from the discovery broadcast.</summary>
        public string RawJson { get; init; } = "";

        /// <inheritdoc />
        public override string ToString() => $"[TuyaDevice {DeviceId} @ {Ip} (v{Version}) - {ProductKey}]";
    }
}
