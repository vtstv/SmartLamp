// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TuyaNet.Native.Framing;
using TuyaNet.Native.Models;
using Xunit;

namespace SmartLamp.Tests
{
    public class TuyaPacketAndFramingTests
    {
        [Fact]
        public void TuyaHeader_Constants_MatchWireSpecifications()
        {
            Assert.Equal(0x000055AAu, TuyaHeader.Prefix55AA);
            Assert.Equal(0x0000AA55u, TuyaHeader.SuffixAA55);
            Assert.Equal(0x00006699u, TuyaHeader.Prefix6699);
            Assert.Equal(0x00009966u, TuyaHeader.Suffix9966);
        }

        [Fact]
        public void TuyaHeader_ExtractJsonString_StripsPaddingAndPrefix()
        {
            string json = "{\"dps\":{\"20\":true}}";
            byte[] rawWithReturnCode = new byte[] { 0, 0, 0, 0 }
                .Concat(Encoding.UTF8.GetBytes(json))
                .Concat(new byte[] { 0, 0 })
                .ToArray();

            string extracted = TuyaHeader.ExtractJsonString(rawWithReturnCode);
            Assert.Equal(json, extracted);

            // Null and empty checks
            Assert.Equal("{}", TuyaHeader.ExtractJsonString(null));
            Assert.Equal("{}", TuyaHeader.ExtractJsonString(System.Array.Empty<byte>()));
        }

        [Fact]
        public async Task TuyaPacket_55AA_PackAndRead_RoundTrip_MatchesPayload()
        {
            var cmd = TuyaCommandType.Control;
            uint seqNo = 42;
            byte[] payload = Encoding.UTF8.GetBytes("{\"dps\":{\"20\":true}}");

            byte[] packet = TuyaPacket.Pack55AA(cmd, seqNo, payload);
            Assert.NotNull(packet);

            // Verify prefix and suffix directly
            uint prefix = BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(0, 4));
            uint suffix = BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(packet.Length - 4, 4));
            Assert.Equal(TuyaHeader.Prefix55AA, prefix);
            Assert.Equal(TuyaHeader.SuffixAA55, suffix);

            // Verify via Stream reader
            using var ms = new MemoryStream(packet);
            var (readCmd, readSeqNo, readPayload) = await TuyaPacket.Read55AAAsync(ms, CancellationToken.None);

            Assert.Equal(cmd, readCmd);
            Assert.Equal(seqNo, readSeqNo);
            Assert.Equal(payload, readPayload);
        }

        [Fact]
        public async Task TuyaPacket_6699_PackAndRead_RoundTrip_MatchesPayload()
        {
            var cmd = TuyaCommandType.ControlNew;
            uint seqNo = 101;
            byte[] key = Encoding.UTF8.GetBytes("0123456789abcdef");
            byte[] payload = Encoding.UTF8.GetBytes("{\"dps\":{\"20\":false,\"22\":500}}");

            byte[] packet = TuyaPacket.Pack6699(cmd, seqNo, payload, key);
            Assert.NotNull(packet);

            uint prefix = BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(0, 4));
            uint suffix = BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(packet.Length - 4, 4));
            Assert.Equal(TuyaHeader.Prefix6699, prefix);
            Assert.Equal(TuyaHeader.Suffix9966, suffix);

            using var ms = new MemoryStream(packet);
            var (readCmd, readSeqNo, readPlaintext) = await TuyaPacket.Read6699Async(ms, key, CancellationToken.None);

            Assert.Equal(cmd, readCmd);
            Assert.Equal(seqNo, readSeqNo);
            Assert.Equal(payload, readPlaintext);
        }

        [Fact]
        public async Task TuyaPacket_55AA_InvalidPrefix_ThrowsInvalidDataException()
        {
            byte[] corrupted = new byte[32];
            BinaryPrimitives.WriteUInt32BigEndian(corrupted.AsSpan(0, 4), 0xDEADBEEF);

            using var ms = new MemoryStream(corrupted);
            await Assert.ThrowsAsync<InvalidDataException>(async () =>
            {
                await TuyaPacket.Read55AAAsync(ms, CancellationToken.None);
            });
        }

        [Fact]
        public async Task TuyaPacket_6699_InvalidSuffix_ThrowsInvalidDataException()
        {
            var cmd = TuyaCommandType.DpQueryNew;
            uint seqNo = 1;
            byte[] key = Encoding.UTF8.GetBytes("0123456789abcdef");
            byte[] payload = Encoding.UTF8.GetBytes("{}");

            byte[] packet = TuyaPacket.Pack6699(cmd, seqNo, payload, key);
            // Corrupt suffix
            packet[^1] ^= 0xFF;

            using var ms = new MemoryStream(packet);
            await Assert.ThrowsAsync<InvalidDataException>(async () =>
            {
                await TuyaPacket.Read6699Async(ms, key, CancellationToken.None);
            });
        }
    }
}
