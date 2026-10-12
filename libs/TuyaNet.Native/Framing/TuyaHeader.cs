// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System.Text;

namespace TuyaNet.Native.Framing
{
    /// <summary>
    /// Constants and framing helpers for Tuya binary wire protocols.
    /// </summary>
    public static class TuyaHeader
    {
        /// <summary>Prefix for legacy frames (v3.1, v3.2, v3.3, v3.4).</summary>
        public const uint Prefix55AA = 0x000055AA;

        /// <summary>Suffix for legacy frames (v3.1, v3.2, v3.3, v3.4).</summary>
        public const uint SuffixAA55 = 0x0000AA55;

        /// <summary>Prefix for GCM frames (v3.4 session, v3.5 all messages).</summary>
        public const uint Prefix6699 = 0x00006699;

        /// <summary>Suffix for GCM frames (v3.4 session, v3.5 all messages).</summary>
        public const uint Suffix9966 = 0x00009966;

        /// <summary>Protocol v3.3 payload header (16 bytes).</summary>
        public static readonly byte[] Version33Header = Encoding.Latin1.GetBytes("3.3\0\0\0\0\0\0\0\0\0\0\0\0");

        /// <summary>Protocol v3.5 payload header (16 bytes).</summary>
        public static readonly byte[] Version35Header = Encoding.Latin1.GetBytes("3.5\0\0\0\0\0\0\0\0\0\0\0\0");

        /// <summary>
        /// Extracts a clean JSON string from raw decrypted Tuya bytes, stripping leading 4-byte return codes.
        /// </summary>
        public static string ExtractJsonString(byte[]? data)
        {
            if (data == null || data.Length == 0) return "{}";

            if (data[0] != (byte)'{' && data.Length >= 4)
            {
                int firstBrace = Array.IndexOf(data, (byte)'{');
                if (firstBrace >= 0)
                {
                    return Encoding.UTF8.GetString(data, firstBrace, data.Length - firstBrace).TrimEnd('\0');
                }
            }

            return Encoding.UTF8.GetString(data).TrimEnd('\0');
        }
    }
}
