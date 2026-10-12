// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

namespace TuyaNet.Native.Models
{
    /// <summary>
    /// Supported versions of the Tuya Local Protocol.
    /// </summary>
    public enum TuyaProtocolVersion
    {
        /// <summary>Tuya Protocol v3.1 (MD5 checksum, AES-ECB).</summary>
        V31,

        /// <summary>Tuya Protocol v3.3 (CRC32 tail, AES-ECB payload).</summary>
        V33,

        /// <summary>Tuya Protocol v3.4 (AES-ECB with session handshake).</summary>
        V34,

        /// <summary>Tuya Protocol v3.5 (AES-GCM-128 with AAD, HMAC-SHA256 handshake).</summary>
        V35
    }

    /// <summary>
    /// Helper extensions for Tuya protocol versions.
    /// </summary>
    public static class TuyaProtocolVersionExtensions
    {
        /// <summary>
        /// Parses a string representation (e.g. "3.5", "3.3") into a <see cref="TuyaProtocolVersion"/>.
        /// </summary>
        public static TuyaProtocolVersion ParseVersion(string? versionStr)
        {
            if (string.IsNullOrWhiteSpace(versionStr)) return TuyaProtocolVersion.V35;

            versionStr = versionStr.Trim();
            if (versionStr.StartsWith("3.1")) return TuyaProtocolVersion.V31;
            if (versionStr.StartsWith("3.3")) return TuyaProtocolVersion.V33;
            if (versionStr.StartsWith("3.4")) return TuyaProtocolVersion.V34;
            return TuyaProtocolVersion.V35;
        }

        /// <summary>
        /// Converts the version enum to its Tuya wire protocol string representation.
        /// </summary>
        public static string ToVersionString(this TuyaProtocolVersion version) => version switch
        {
            TuyaProtocolVersion.V31 => "3.1",
            TuyaProtocolVersion.V33 => "3.3",
            TuyaProtocolVersion.V34 => "3.4",
            _ => "3.5"
        };
    }
}
