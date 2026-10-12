// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System.Net.Sockets;
using System.Security.Cryptography;
using TuyaNet.Native.Crypto;
using TuyaNet.Native.Framing;
using TuyaNet.Native.Models;

namespace TuyaNet.Native.Session
{
    /// <summary>
    /// Manages the TCP socket connection and protocol session negotiation with a Tuya device.
    /// </summary>
    public class TuyaSession : IDisposable
    {
        private readonly string _ip;
        private readonly int _port;
        private readonly byte[] _localKeyBytes;
        private readonly TuyaProtocolVersion _version;

        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private byte[]? _sessionKey;
        private uint _seqNo = 1;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public TuyaSession(string ip, int port, byte[] localKeyBytes, TuyaProtocolVersion version)
        {
            _ip = ip;
            _port = port > 0 ? port : 6668;
            _localKeyBytes = localKeyBytes;
            _version = version;
        }

        public bool IsVersion35 => _version == TuyaProtocolVersion.V34 || _version == TuyaProtocolVersion.V35;

        public bool IsConnected => _tcpClient != null && _tcpClient.Connected && _stream != null && (!IsVersion35 || _sessionKey != null);

        /// <summary>
        /// Ensures an active TCP connection and completes the session key handshake if needed.
        /// </summary>
        public async Task EnsureConnectedAsync(CancellationToken cancellationToken)
        {
            if (IsConnected) return;

            Disconnect();

            var client = new TcpClient
            {
                NoDelay = true,
                ReceiveTimeout = 4000,
                SendTimeout = 4000
            };

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(4000);

            await client.ConnectAsync(_ip, _port, connectCts.Token).ConfigureAwait(false);

            _tcpClient = client;
            _stream = client.GetStream();
            _seqNo = 1;

            if (IsVersion35)
            {
                await NegotiateSessionKeyAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public void Disconnect()
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
            byte[] step1Packet = TuyaPacket.Pack6699(TuyaCommandType.SessKeyNegStart, _seqNo++, localNonce, _localKeyBytes);
            await _stream!.WriteAsync(step1Packet, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            // Step 2: Receive device nonce and verify HMAC
            var (cmd, _, plaintext) = await TuyaPacket.Read6699Async(_stream, _localKeyBytes, cancellationToken).ConfigureAwait(false);
            if (cmd != TuyaCommandType.SessKeyNegResp || plaintext.Length < 48)
            {
                throw new InvalidOperationException($"Tuya v3.5 session negotiation step 2 failed. Cmd={cmd}, len={plaintext.Length}");
            }

            byte[] nonceAndHmac = plaintext.Length >= 52 ? plaintext[4..] : plaintext;
            byte[] remoteNonce = nonceAndHmac[..16];
            byte[] deviceHmac = nonceAndHmac[16..48];

            byte[] expectedHmac = TuyaCrypto.ComputeHmacSha256(_localKeyBytes, localNonce);
            if (!CryptographicOperations.FixedTimeEquals(expectedHmac, deviceHmac))
            {
                throw new InvalidOperationException("Tuya v3.5 session negotiation HMAC verification failed.");
            }

            // Step 3: Send client HMAC of device nonce (Cmd 0x05) in 6699 format using localKey
            byte[] clientHmac = TuyaCrypto.ComputeHmacSha256(_localKeyBytes, remoteNonce);
            byte[] step3Packet = TuyaPacket.Pack6699(TuyaCommandType.SessKeyNegFinish, _seqNo++, clientHmac, _localKeyBytes);
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

            TuyaCrypto.AesGcmEncrypt(_localKeyBytes, iv, xorNonce, sessionKey, tag);
            _sessionKey = sessionKey;
        }

        #endregion

        #region Thread-Safe Request Dispatching

        /// <summary>
        /// Sends a 6699 message and returns the decrypted payload. Reconnects on connection loss.
        /// </summary>
        public async Task<(TuyaCommandType Cmd, byte[] Plaintext)> SendReceive6699Async(TuyaCommandType cmd, byte[] payload, CancellationToken cancellationToken)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

                        byte[] packet = TuyaPacket.Pack6699(cmd, _seqNo++, payload, _sessionKey!);
                        await _stream!.WriteAsync(packet, cancellationToken).ConfigureAwait(false);
                        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);

                        var resp = await TuyaPacket.Read6699Async(_stream, _sessionKey!, cancellationToken).ConfigureAwait(false);
                        return (resp.Cmd, resp.Plaintext);
                    }
                    catch
                    {
                        Disconnect();
                        if (attempt == 1) throw;
                    }
                }
                throw new InvalidOperationException("Failed to exchange Tuya 6699 packet after retry.");
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Sends a 55AA message and returns the raw response payload. Reconnects on connection loss.
        /// </summary>
        public async Task<(TuyaCommandType Cmd, byte[] Payload)> SendReceive55AAAsync(TuyaCommandType cmd, byte[] payload, CancellationToken cancellationToken)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

                        byte[] packet = TuyaPacket.Pack55AA(cmd, _seqNo++, payload);
                        await _stream!.WriteAsync(packet, cancellationToken).ConfigureAwait(false);
                        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);

                        var resp = await TuyaPacket.Read55AAAsync(_stream, cancellationToken).ConfigureAwait(false);
                        return (resp.Cmd, resp.Payload);
                    }
                    catch
                    {
                        Disconnect();
                        if (attempt == 1) throw;
                    }
                }
                throw new InvalidOperationException("Failed to exchange Tuya 55AA packet after retry.");
            }
            finally
            {
                _lock.Release();
            }
        }

        #endregion

        public void Dispose()
        {
            Disconnect();
            _lock.Dispose();
        }
    }
}
