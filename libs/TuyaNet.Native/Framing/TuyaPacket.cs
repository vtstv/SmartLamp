// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using TuyaNet.Native.Crypto;
using TuyaNet.Native.Models;

namespace TuyaNet.Native.Framing
{
    /// <summary>
    /// Binary packet packing and framing for Tuya protocol v3.3 and v3.5.
    /// </summary>
    public static class TuyaPacket
    {
        #region 55AA Framing

        /// <summary>
        /// Packs a payload into a legacy 0x55AA framed message.
        /// </summary>
        public static byte[] Pack55AA(TuyaCommandType cmd, uint seqNo, byte[] payload)
        {
            uint length = (uint)(payload.Length + 8);
            byte[] packet = new byte[16 + payload.Length + 8];

            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(0, 4), TuyaHeader.Prefix55AA);
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(4, 4), seqNo);
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(8, 4), (uint)cmd);
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(12, 4), length);

            Buffer.BlockCopy(payload, 0, packet, 16, payload.Length);

            uint crc = TuyaCrc32.Compute(packet, 0, 16 + payload.Length);
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(16 + payload.Length, 4), crc);
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(20 + payload.Length, 4), TuyaHeader.SuffixAA55);

            return packet;
        }

        /// <summary>
        /// Reads and validates a 0x55AA framed packet from a stream.
        /// </summary>
        public static async Task<(TuyaCommandType Cmd, uint SeqNo, byte[] Payload)> Read55AAAsync(Stream stream, CancellationToken cancellationToken)
        {
            byte[] header = new byte[16];
            await ReadExactAsync(stream, header, 0, 16, cancellationToken).ConfigureAwait(false);

            uint prefix = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0, 4));
            if (prefix != TuyaHeader.Prefix55AA)
            {
                throw new InvalidDataException($"Invalid Tuya 55AA prefix: 0x{prefix:X8}");
            }

            uint seqNo = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4, 4));
            var cmd = (TuyaCommandType)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(8, 4));
            uint length = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(12, 4));

            if (length < 8 || length > 65536)
            {
                throw new InvalidDataException($"Invalid Tuya 55AA length: {length}");
            }

            byte[] body = new byte[length];
            await ReadExactAsync(stream, body, 0, (int)length, cancellationToken).ConfigureAwait(false);

            uint suffix = BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan((int)length - 4, 4));
            if (suffix != TuyaHeader.SuffixAA55)
            {
                throw new InvalidDataException($"Invalid Tuya 55AA suffix: 0x{suffix:X8}");
            }

            byte[] payload = body[..((int)length - 8)];
            return (cmd, seqNo, payload);
        }

        #endregion

        #region 6699 Framing (AES-GCM)

        /// <summary>
        /// Packs and encrypts a payload into a 0x6699 framed AES-GCM message.
        /// </summary>
        public static byte[] Pack6699(TuyaCommandType cmd, uint seqNo, byte[] payload, byte[] key)
        {
            uint length = (uint)(payload.Length + 28);
            byte[] header = new byte[18];

            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), TuyaHeader.Prefix6699);
            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4, 2), 0);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(6, 4), seqNo);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(10, 4), (uint)cmd);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(14, 4), length);

            byte[] iv = RandomNumberGenerator.GetBytes(12);
            byte[] aad = header[4..18];

            byte[] ciphertext = new byte[payload.Length];
            byte[] tag = new byte[16];

            TuyaCrypto.AesGcmEncrypt(key, iv, payload, ciphertext, tag, aad);

            byte[] packet = new byte[18 + 12 + payload.Length + 16 + 4];
            Buffer.BlockCopy(header, 0, packet, 0, 18);
            Buffer.BlockCopy(iv, 0, packet, 18, 12);
            Buffer.BlockCopy(ciphertext, 0, packet, 30, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, packet, 30 + ciphertext.Length, 16);
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(packet.Length - 4, 4), TuyaHeader.Suffix9966);

            return packet;
        }

        /// <summary>
        /// Reads and decrypts a 0x6699 framed AES-GCM packet from a stream.
        /// </summary>
        public static async Task<(TuyaCommandType Cmd, uint SeqNo, byte[] Plaintext)> Read6699Async(Stream stream, byte[] key, CancellationToken cancellationToken)
        {
            byte[] header = new byte[18];
            await ReadExactAsync(stream, header, 0, 18, cancellationToken).ConfigureAwait(false);

            uint prefix = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0, 4));
            if (prefix != TuyaHeader.Prefix6699)
            {
                throw new InvalidDataException($"Invalid Tuya 6699 prefix: 0x{prefix:X8}");
            }

            uint seqNo = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(6, 4));
            var cmd = (TuyaCommandType)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(10, 4));
            uint length = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(14, 4));

            if (length < 28 || length > 65536)
            {
                throw new InvalidDataException($"Invalid Tuya 6699 length: {length}");
            }

            int totalBodyLen = (int)length + 4;
            byte[] body = new byte[totalBodyLen];
            await ReadExactAsync(stream, body, 0, totalBodyLen, cancellationToken).ConfigureAwait(false);

            uint suffix = BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(totalBodyLen - 4, 4));
            if (suffix != TuyaHeader.Suffix9966)
            {
                throw new InvalidDataException($"Invalid Tuya 6699 suffix: 0x{suffix:X8}");
            }

            byte[] iv = body[..12];
            int cipherLen = totalBodyLen - 4 - 16 - 12;
            byte[] ciphertext = body[12..(12 + cipherLen)];
            byte[] tag = body[(12 + cipherLen)..(12 + cipherLen + 16)];
            byte[] aad = header[4..18];

            byte[] plaintext = new byte[cipherLen];
            TuyaCrypto.AesGcmDecrypt(key, iv, ciphertext, tag, plaintext, aad);

            return (cmd, seqNo, plaintext);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Reads exact number of bytes from stream or throws EndOfStreamException if disconnected.
        /// </summary>
        public static async Task ReadExactAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(offset + totalRead, count - totalRead), cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                {
                    throw new EndOfStreamException("Tuya TCP socket closed unexpectedly by remote device.");
                }
                totalRead += read;
            }
        }

        #endregion
    }
}
