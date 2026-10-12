// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SmartLampApp.Services
{
    /// <summary>
    /// Pure C# native implementation of Tuya Local Protocol v3.3 & v3.5.
    /// Provides zero-dependency, ultra-fast LAN control directly over TCP port 6668.
    /// </summary>
    public class TuyaLocalClient : IDisposable
    {
        public const int DefaultPort = 6668;

        private const uint Prefix55AA = 0x000055AA;
        private const uint SuffixAA55 = 0x0000AA55;
        private const uint Prefix6699 = 0x00006699;
        private const uint Suffix9966 = 0x00009966;

        private const int CmdSessKeyNegStart = 0x03;
        private const int CmdSessKeyNegResp = 0x04;
        private const int CmdSessKeyNegFinish = 0x05;
        private const int CmdControl = 0x07;
        private const int CmdStatus = 0x08;
        private const int CmdDpQuery = 0x0A;
        private const int CmdControlNew = 0x0D;
        private const int CmdDpQueryNew = 0x10;

        private static readonly byte[] Protocol33Header = Encoding.Latin1.GetBytes("3.3\0\0\0\0\0\0\0\0\0\0\0\0");
        private static readonly byte[] Protocol35Header = Encoding.Latin1.GetBytes("3.5\0\0\0\0\0\0\0\0\0\0\0\0");

        private readonly string _ip;
        private readonly int _port;
        private readonly string _devId;
        private readonly byte[] _localKeyBytes;
        private readonly string _version;

        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private byte[]? _sessionKey;
        private uint _seqNo = 1;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public TuyaLocalClient(string ip, string devId, string localKey, string version = "3.5", int port = DefaultPort)
        {
            _ip = ip;
            _port = port > 0 ? port : DefaultPort;
            _devId = devId;
            _localKeyBytes = Encoding.UTF8.GetBytes(localKey);
            _version = string.IsNullOrWhiteSpace(version) ? "3.5" : version;
        }

        private bool IsVersion35 => _version.StartsWith("3.4") || _version.StartsWith("3.5");

        private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
        {
            if (_tcpClient != null && _tcpClient.Connected && _stream != null)
            {
                if (!IsVersion35 || _sessionKey != null)
                {
                    return;
                }
            }

            Disconnect();

            Console.WriteLine($"[TuyaLocalClient] Connecting to {_ip}:{_port}...");
            var client = new TcpClient();
            client.NoDelay = true;
            client.ReceiveTimeout = 4000;
            client.SendTimeout = 4000;

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(4000);

            await client.ConnectAsync(_ip, _port, connectCts.Token).ConfigureAwait(false);
            Console.WriteLine($"[TuyaLocalClient] Connected to {_ip}:{_port}!");

            _tcpClient = client;
            _stream = client.GetStream();
            _seqNo = 1;

            if (IsVersion35)
            {
                Console.WriteLine("[TuyaLocalClient] Negotiating v3.5 session key...");
                await NegotiateSessionKeyAsync(cancellationToken).ConfigureAwait(false);
                Console.WriteLine("[TuyaLocalClient] v3.5 session key negotiated successfully!");
            }
        }

        private void Disconnect()
        {
            try { _stream?.Dispose(); } catch { }
            try { _tcpClient?.Close(); } catch { }
            try { _tcpClient?.Dispose(); } catch { }
            _stream = null;
            _tcpClient = null;
            _sessionKey = null;
        }

        #region Session Negotiation (v3.5)

        private async Task NegotiateSessionKeyAsync(CancellationToken cancellationToken)
        {
            // Step 1: Send client nonce (Cmd 0x03) in 6699 format using localKey
            byte[] localNonce = RandomNumberGenerator.GetBytes(16);
            byte[] step1Packet = Pack6699(CmdSessKeyNegStart, _seqNo++, localNonce, _localKeyBytes);
            await _stream!.WriteAsync(step1Packet, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            // Step 2: Receive device nonce and verify HMAC
            var resp = await Read6699RawAsync(_localKeyBytes, cancellationToken).ConfigureAwait(false);
            if (resp.Cmd != CmdSessKeyNegResp || resp.Plaintext.Length < 48)
            {
                throw new InvalidOperationException($"Tuya v3.5 session negotiation step 2 failed. Cmd={resp.Cmd}, len={resp.Plaintext.Length}");
            }

            byte[] nonceAndHmac = resp.Plaintext.Length >= 52 ? resp.Plaintext[4..] : resp.Plaintext;
            byte[] remoteNonce = nonceAndHmac[..16];
            byte[] deviceHmac = nonceAndHmac[16..48];

            using var hmac = new HMACSHA256(_localKeyBytes);
            byte[] expectedHmac = hmac.ComputeHash(localNonce);
            if (!CryptographicOperations.FixedTimeEquals(expectedHmac, deviceHmac))
            {
                throw new InvalidOperationException("Tuya v3.5 session negotiation HMAC verification failed.");
            }

            // Step 3: Send client HMAC of device nonce (Cmd 0x05) in 6699 format using localKey
            byte[] clientHmac = hmac.ComputeHash(remoteNonce);
            byte[] step3Packet = Pack6699(CmdSessKeyNegFinish, _seqNo++, clientHmac, _localKeyBytes);
            await _stream.WriteAsync(step3Packet, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            // Step 4: Finalize session key
            byte[] xorNonce = new byte[16];
            for (int i = 0; i < 16; i++)
            {
                xorNonce[i] = (byte)(localNonce[i] ^ remoteNonce[i]);
            }

            byte[] iv = localNonce[..12];
            byte[] sessionKey = new byte[16];
            byte[] tag = new byte[16];

            using var aesGcm = new AesGcm(_localKeyBytes, 16);
            aesGcm.Encrypt(iv, xorNonce, sessionKey, tag);

            _sessionKey = sessionKey;
        }

        #endregion

        #region Public Device Commands

        public async Task<string> GetStatusJsonAsync(int timeoutMs = 6000)
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            await _lock.WaitAsync(cts.Token).ConfigureAwait(false);
            try
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        await EnsureConnectedAsync(cts.Token).ConfigureAwait(false);

                        if (IsVersion35)
                        {
                            Console.WriteLine("[TuyaLocalClient] Sending DP_QUERY_NEW packet (Cmd=0x10)...");
                            byte[] payload = Encoding.UTF8.GetBytes("{}");
                            byte[] packet = Pack6699(CmdDpQueryNew, _seqNo++, payload);
                            await _stream!.WriteAsync(packet, cts.Token).ConfigureAwait(false);
                            await _stream.FlushAsync(cts.Token).ConfigureAwait(false);

                            Console.WriteLine("[TuyaLocalClient] Waiting for 6699 response from device...");
                            var resp = await Read6699Async(cts.Token).ConfigureAwait(false);
                            Console.WriteLine($"[TuyaLocalClient] Received 6699 response! Json: {resp.Json}");
                            return resp.Json;
                        }
                        else
                        {
                            Console.WriteLine("[TuyaLocalClient] Sending v3.3 DP_QUERY packet (Cmd=0x0A)...");
                            long t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            string queryJson = $"{{\"gwId\":\"{_devId}\",\"devId\":\"{_devId}\",\"uid\":\"{_devId}\",\"t\":\"{t}\"}}";
                            byte[] encPayload = AesEcbEncrypt(Encoding.UTF8.GetBytes(queryJson), _localKeyBytes);
                            byte[] packet = Pack55AA(CmdDpQuery, _seqNo++, encPayload);
                            await _stream!.WriteAsync(packet, cts.Token).ConfigureAwait(false);
                            await _stream.FlushAsync(cts.Token).ConfigureAwait(false);

                            Console.WriteLine("[TuyaLocalClient] Waiting for 55AA response from device...");
                            var resp = await Read55AAAsync(cts.Token).ConfigureAwait(false);
                            byte[] decrypted = AesEcbDecrypt(resp.Payload, _localKeyBytes);
                            string json = ExtractJsonString(decrypted);
                            Console.WriteLine($"[TuyaLocalClient] Received 55AA response! Json: {json}");
                            return json;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[TuyaLocalClient] Error on attempt {attempt}: {ex.Message}");
                        Disconnect();
                        if (attempt == 1) throw;
                    }
                }

                return "{}";
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<bool> SetDpsAsync(Dictionary<string, object> dps, int timeoutMs = 3000)
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            await _lock.WaitAsync(cts.Token).ConfigureAwait(false);
            try
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        await EnsureConnectedAsync(cts.Token).ConfigureAwait(false);

                        if (IsVersion35)
                        {
                            long t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            var dataObj = new
                            {
                                protocol = 5,
                                t = t,
                                data = new { dps = dps }
                            };
                            string jsonStr = JsonSerializer.Serialize(dataObj);
                            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonStr);

                            byte[] fullPayload = new byte[Protocol35Header.Length + jsonBytes.Length];
                            Buffer.BlockCopy(Protocol35Header, 0, fullPayload, 0, Protocol35Header.Length);
                            Buffer.BlockCopy(jsonBytes, 0, fullPayload, Protocol35Header.Length, jsonBytes.Length);

                            byte[] packet = Pack6699(CmdControlNew, _seqNo++, fullPayload);
                            await _stream!.WriteAsync(packet, cts.Token).ConfigureAwait(false);
                            await _stream.FlushAsync(cts.Token).ConfigureAwait(false);

                            var resp = await Read6699Async(cts.Token).ConfigureAwait(false);
                            return !string.IsNullOrWhiteSpace(resp.Json) && !resp.Json.Contains("\"error\"");
                        }
                        else
                        {
                            long t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            var dataObj = new
                            {
                                devId = _devId,
                                uid = _devId,
                                t = t.ToString(),
                                dps = dps
                            };
                            string jsonStr = JsonSerializer.Serialize(dataObj);
                            byte[] encPayload = AesEcbEncrypt(Encoding.UTF8.GetBytes(jsonStr), _localKeyBytes);

                            byte[] fullPayload = new byte[Protocol33Header.Length + encPayload.Length];
                            Buffer.BlockCopy(Protocol33Header, 0, fullPayload, 0, Protocol33Header.Length);
                            Buffer.BlockCopy(encPayload, 0, fullPayload, Protocol33Header.Length, encPayload.Length);

                            byte[] packet = Pack55AA(CmdControl, _seqNo++, fullPayload);
                            await _stream!.WriteAsync(packet, cts.Token).ConfigureAwait(false);
                            await _stream.FlushAsync(cts.Token).ConfigureAwait(false);

                            var resp = await Read55AAAsync(cts.Token).ConfigureAwait(false);
                            return resp.Cmd == CmdStatus || resp.Cmd == CmdControl;
                        }
                    }
                    catch
                    {
                        Disconnect();
                        if (attempt == 1) return false;
                    }
                }

                return false;
            }
            finally
            {
                _lock.Release();
            }
        }

        #endregion

        #region Protocol 55AA Framing

        private static byte[] Pack55AA(int cmd, uint seqNo, byte[] payload)
        {
            // Header: 16 bytes. Tail: 8 bytes (CRC32 + Suffix).
            uint length = (uint)(payload.Length + 8);
            byte[] packet = new byte[16 + payload.Length + 8];

            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(0, 4), Prefix55AA);
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(4, 4), seqNo);
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(8, 4), (uint)cmd);
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(12, 4), length);

            Buffer.BlockCopy(payload, 0, packet, 16, payload.Length);

            uint crc = TuyaCrc32.Compute(packet, 0, 16 + payload.Length);
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(16 + payload.Length, 4), crc);
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(20 + payload.Length, 4), SuffixAA55);

            return packet;
        }

        private async Task<(int Cmd, uint SeqNo, byte[] Payload)> Read55AAAsync(CancellationToken cancellationToken)
        {
            byte[] header = new byte[16];
            await ReadExactAsync(header, 0, 16, cancellationToken).ConfigureAwait(false);

            uint prefix = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0, 4));
            if (prefix != Prefix55AA)
            {
                throw new InvalidDataException($"Invalid Tuya 55AA prefix: 0x{prefix:X8}");
            }

            uint seqNo = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4, 4));
            int cmd = (int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(8, 4));
            uint length = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(12, 4));

            if (length < 8 || length > 65536)
            {
                throw new InvalidDataException($"Invalid Tuya 55AA length: {length}");
            }

            byte[] body = new byte[length];
            await ReadExactAsync(body, 0, (int)length, cancellationToken).ConfigureAwait(false);

            uint suffix = BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan((int)length - 4, 4));
            if (suffix != SuffixAA55)
            {
                throw new InvalidDataException($"Invalid Tuya 55AA suffix: 0x{suffix:X8}");
            }

            byte[] payload = body[..((int)length - 8)];
            return (cmd, seqNo, payload);
        }

        #endregion

        #region Protocol 6699 Framing (v3.5)

        private byte[] Pack6699(int cmd, uint seqNo, byte[] payload, byte[]? keyOverride = null)
        {
            byte[] key = keyOverride ?? _sessionKey ?? throw new InvalidOperationException("Session key must be negotiated before sending 6699 packets.");

            // Length field = payload.Length + 12 (IV) + 16 (Tag) = payload.Length + 28
            uint length = (uint)(payload.Length + 28);
            byte[] header = new byte[18];

            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), Prefix6699);
            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4, 2), 0);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(6, 4), seqNo);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(10, 4), (uint)cmd);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(14, 4), length);

            byte[] iv = RandomNumberGenerator.GetBytes(12);
            byte[] aad = header[4..18]; // 14 bytes: unknown (2) + seq (4) + cmd (4) + len (4)

            byte[] ciphertext = new byte[payload.Length];
            byte[] tag = new byte[16];

            using var gcm = new AesGcm(key, 16);
            gcm.Encrypt(iv, payload, ciphertext, tag, aad);

            // Total packet = Header (18) + IV (12) + Ciphertext (N) + Tag (16) + Suffix (4)
            byte[] packet = new byte[18 + 12 + payload.Length + 16 + 4];
            Buffer.BlockCopy(header, 0, packet, 0, 18);
            Buffer.BlockCopy(iv, 0, packet, 18, 12);
            Buffer.BlockCopy(ciphertext, 0, packet, 30, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, packet, 30 + ciphertext.Length, 16);
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(packet.Length - 4, 4), Suffix9966);

            return packet;
        }

        private async Task<(int Cmd, uint SeqNo, byte[] Plaintext)> Read6699RawAsync(byte[] key, CancellationToken cancellationToken)
        {
            byte[] header = new byte[18];
            await ReadExactAsync(header, 0, 18, cancellationToken).ConfigureAwait(false);

            uint prefix = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0, 4));
            if (prefix != Prefix6699)
            {
                throw new InvalidDataException($"Invalid Tuya 6699 prefix: 0x{prefix:X8}");
            }

            uint seqNo = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(6, 4));
            int cmd = (int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(10, 4));
            uint length = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(14, 4));

            if (length < 28 || length > 65536)
            {
                throw new InvalidDataException($"Invalid Tuya 6699 length: {length}");
            }

            // Read body + 4 bytes suffix
            int totalBodyLen = (int)length + 4;
            byte[] body = new byte[totalBodyLen];
            await ReadExactAsync(body, 0, totalBodyLen, cancellationToken).ConfigureAwait(false);

            uint suffix = BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(totalBodyLen - 4, 4));
            if (suffix != Suffix9966)
            {
                throw new InvalidDataException($"Invalid Tuya 6699 suffix: 0x{suffix:X8}");
            }

            byte[] iv = body[..12];
            int cipherLen = totalBodyLen - 4 - 16 - 12; // total - suffix(4) - tag(16) - iv(12)
            byte[] ciphertext = body[12..(12 + cipherLen)];
            byte[] tag = body[(12 + cipherLen)..(12 + cipherLen + 16)];
            byte[] aad = header[4..18];

            byte[] plaintext = new byte[cipherLen];
            using var gcm = new AesGcm(key, 16);
            gcm.Decrypt(iv, ciphertext, tag, plaintext, aad);

            return (cmd, seqNo, plaintext);
        }

        private async Task<(int Cmd, uint SeqNo, string Json)> Read6699Async(CancellationToken cancellationToken)
        {
            if (_sessionKey == null)
            {
                throw new InvalidOperationException("Session key must be negotiated before reading 6699 packets.");
            }

            var (cmd, seqNo, plaintext) = await Read6699RawAsync(_sessionKey, cancellationToken).ConfigureAwait(false);
            string json = ExtractJsonString(plaintext);
            return (cmd, seqNo, json);
        }

        #endregion

        #region Helpers & Crypto

        private async Task ReadExactAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = await _stream!.ReadAsync(buffer.AsMemory(offset + totalRead, count - totalRead), cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                {
                    throw new EndOfStreamException("Tuya TCP socket closed unexpectedly by remote device.");
                }
                totalRead += read;
            }
        }

        private static string ExtractJsonString(byte[] data)
        {
            if (data == null || data.Length == 0) return "{}";

            // If prepended with a 4-byte uint32 return code (e.g. 0x00000000)
            if (data[0] != (byte)'{' && data.Length >= 4)
            {
                int firstBrace = Array.IndexOf(data, (byte)'{');
                if (firstBrace >= 0)
                {
                    return Encoding.UTF8.GetString(data, firstBrace, data.Length - firstBrace);
                }
            }

            return Encoding.UTF8.GetString(data);
        }

        private static byte[] AesEcbEncrypt(byte[] plainBytes, byte[] keyBytes)
        {
            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;
            using var enc = aes.CreateEncryptor();
            return enc.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        }

        private static byte[] AesEcbDecrypt(byte[] cipherBytes, byte[] keyBytes)
        {
            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;
            using var dec = aes.CreateDecryptor();
            return dec.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        }

        public static readonly byte[] UdpKey = MD5.HashData(Encoding.UTF8.GetBytes("yGAdlopoPVldABfn"));

        public static byte[] Pack6699Direct(int cmd, uint seqNo, byte[] payload, byte[] key)
        {
            uint length = (uint)(payload.Length + 28);
            byte[] header = new byte[18];

            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), Prefix6699);
            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4, 2), 0);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(6, 4), seqNo);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(10, 4), (uint)cmd);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(14, 4), length);

            byte[] iv = RandomNumberGenerator.GetBytes(12);
            byte[] aad = header[4..18];

            byte[] ciphertext = new byte[payload.Length];
            byte[] tag = new byte[16];

            using var gcm = new AesGcm(key, 16);
            gcm.Encrypt(iv, payload, ciphertext, tag, aad);

            byte[] packet = new byte[18 + 12 + payload.Length + 16 + 4];
            Buffer.BlockCopy(header, 0, packet, 0, 18);
            Buffer.BlockCopy(iv, 0, packet, 18, 12);
            Buffer.BlockCopy(ciphertext, 0, packet, 30, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, packet, 30 + ciphertext.Length, 16);
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(packet.Length - 4, 4), Suffix9966);

            return packet;
        }

        public static string DecryptUdpPacket(byte[] data, byte[] udpKey)
        {
            if (data == null || data.Length < 16) return "";

            uint prefix = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(0, 4));
            if (prefix == Prefix6699 && data.Length >= 38)
            {
                uint length = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(14, 4));
                if (data.Length >= 18 + length + 4)
                {
                    byte[] iv = data[18..30];
                    int cipherLen = (int)length - 28;
                    if (cipherLen > 0)
                    {
                        byte[] ciphertext = data[30..(30 + cipherLen)];
                        byte[] tag = data[(30 + cipherLen)..(30 + cipherLen + 16)];
                        byte[] aad = data[4..18];

                        byte[] plaintext = new byte[cipherLen];
                        using var gcm = new AesGcm(udpKey, 16);
                        gcm.Decrypt(iv, ciphertext, tag, plaintext, aad);
                        return ExtractJsonString(plaintext).TrimEnd('\0');
                    }
                }
            }
            else if (prefix == Prefix55AA && data.Length >= 24)
            {
                uint length = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(12, 4));
                int payloadLen = (int)length - 8;
                if (payloadLen > 0 && data.Length >= 16 + payloadLen)
                {
                    byte[] payload = data[16..(16 + payloadLen)];
                    if (payload[0] == '{')
                    {
                        return Encoding.UTF8.GetString(payload);
                    }
                    try
                    {
                        byte[] decrypted = AesEcbDecrypt(payload, udpKey);
                        return ExtractJsonString(decrypted);
                    }
                    catch { }
                }
            }
            else if (data[0] == '{')
            {
                return Encoding.UTF8.GetString(data).TrimEnd('\0');
            }

            return "";
        }

        public void Dispose()
        {
            Disconnect();
            _lock.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// Standard IEEE 802.3 CRC32 implementation.
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
