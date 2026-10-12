// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

namespace TuyaNet.Native.Crypto
{
    /// <summary>
    /// Fast IEEE 802.3 standard CRC32 checksum calculator for Tuya 0x55AA frames.
    /// </summary>
    public static class TuyaCrc32
    {
        private static readonly uint[] Table;

        static TuyaCrc32()
        {
            Table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((c & 1) != 0) c = 0xEDB88320 ^ (c >> 1);
                    else c >>= 1;
                }
                Table[i] = c;
            }
        }

        /// <summary>
        /// Computes CRC32 checksum for a slice of bytes.
        /// </summary>
        public static uint Compute(byte[] bytes, int offset, int length)
        {
            uint crc = 0xFFFFFFFF;
            for (int i = offset; i < offset + length; i++)
            {
                crc = Table[(crc ^ bytes[i]) & 0xFF] ^ (crc >> 8);
            }
            return crc ^ 0xFFFFFFFF;
        }
    }
}
