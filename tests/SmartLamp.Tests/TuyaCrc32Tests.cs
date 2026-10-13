// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System.Text;
using TuyaNet.Native.Crypto;
using Xunit;

namespace SmartLamp.Tests
{
    public class TuyaCrc32Tests
    {
        [Fact]
        public void Compute_StandardAsciiVector_ReturnsExpectedCrc()
        {
            // Standard IEEE 802.3 CRC32 check value for "123456789" is 0xCBF43926
            byte[] input = Encoding.ASCII.GetBytes("123456789");
            uint crc = TuyaCrc32.Compute(input, 0, input.Length);

            Assert.Equal(0xCBF43926u, crc);
        }

        [Fact]
        public void Compute_EmptyArray_ReturnsZero()
        {
            byte[] empty = System.Array.Empty<byte>();
            uint crc = TuyaCrc32.Compute(empty, 0, 0);

            Assert.Equal(0u, crc);
        }

        [Fact]
        public void Compute_SliceOffset_ComputesOnlyTargetSlice()
        {
            byte[] full = Encoding.ASCII.GetBytes("PREFIX_123456789_SUFFIX");
            int offset = "PREFIX_".Length;
            int length = "123456789".Length;

            uint crcSlice = TuyaCrc32.Compute(full, offset, length);
            Assert.Equal(0xCBF43926u, crcSlice);
        }
    }
}
