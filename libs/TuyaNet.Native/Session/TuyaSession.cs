// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TuyaNet.Native.Crypto;
using TuyaNet.Native.Framing;
using TuyaNet.Native.Models;

namespace TuyaNet.Native.Session
{
    /// <summary>
    /// Manages the TCP socket connection, protocol session negotiation,
    /// heartbeat keep-alive, and asynchronous push frame dispatching with a Tuya device.
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
        private int _seqNo = 1;

        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly SemaphoreSlim _connectLock = new(1, 1);

        private CancellationTokenSource? _receiveCts;
        private Task? _receiveTask;
        private PendingRequest? _pendingRequest;

        /// <summary>
        /// Fires when an asynchronous status frame (Cmd 0x08) is received from the device.
        /// </summary>
        public event Action<string>? OnStatusPushed;

        /// <summary>
        /// Timestamp of the last received heartbeat pong frame (Cmd 0x09).
        /// </summary>
        public DateTime LastPongTime { get; private set; } = DateTime.MinValue;

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
        /// Ensures an active TCP connection, completes the session key handshake if needed,
        /// and starts the reactive push frame listener.
        /// </summary>
        public async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
        {
            if (IsConnected) return;

            await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (IsConnected) return;

                Disconnect();

                var client = new TcpClient
                {
                    NoDelay = true,
                    ReceiveTimeout = 0,
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

                StartReceiveLoop();
            }
            finally
            {
                _connectLock.Release();
            }
        }

        public void Disconnect()
        {
            try { _receiveCts?.Cancel(); } catch { }
            try { _stream?.Dispose(); } catch { }
            try { _tcpClient?.Close(); } catch { }
            try { _tcpClient?.Dispose(); } catch { }
            _stream = null;
            _tcpClient = null;
            _sessionKey = null;
            try { _receiveCts?.Dispose(); } catch { }
            _receiveCts = null;

            FailPendingRequests(new IOException("Tuya session disconnected."));
        }

        #region Session Negotiation (v3.5)

        private async Task NegotiateSessionKeyAsync(CancellationToken cancellationToken)
        {
            // Step 1: Send client nonce (Cmd 0x03) in 6699 format using localKey
            byte[] localNonce = RandomNumberGenerator.GetBytes(16);
            uint seq = (uint)Interlocked.Increment(ref _seqNo);
            byte[] step1Packet = TuyaPacket.Pack6699(TuyaCommandType.SessKeyNegStart, seq, localNonce, _localKeyBytes);
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
            seq = (uint)Interlocked.Increment(ref _seqNo);
            byte[] step3Packet = TuyaPacket.Pack6699(TuyaCommandType.SessKeyNegFinish, seq, clientHmac, _localKeyBytes);
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

        #region Asynchronous Push Frame Listener

        private void StartReceiveLoop()
        {
            _receiveCts?.Cancel();
            _receiveCts?.Dispose();
            _receiveCts = new CancellationTokenSource();
            var token = _receiveCts.Token;
            _receiveTask = Task.Run(() => ReceiveLoopAsync(token), token);
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var stream = _stream;
            if (stream == null) return;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    TuyaCommandType cmd;
                    uint seqNo;
                    byte[] payload;

                    if (IsVersion35)
                    {
                        var key = _sessionKey;
                        if (key == null) break;
                        var res = await TuyaPacket.Read6699Async(stream, key, cancellationToken).ConfigureAwait(false);
                        cmd = res.Cmd;
                        seqNo = res.SeqNo;
                        payload = res.Plaintext;
                    }
                    else
                    {
                        var res = await TuyaPacket.Read55AAAsync(stream, cancellationToken).ConfigureAwait(false);
                        cmd = res.Cmd;
                        seqNo = res.SeqNo;
                        payload = res.Payload;
                    }

                    DispatchReceivedFrame(cmd, seqNo, payload);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal exit on disconnect / cancellation
            }
            catch (Exception ex)
            {
                FailPendingRequests(ex);
            }
        }

        private void DispatchReceivedFrame(TuyaCommandType cmd, uint seqNo, byte[] payload)
        {
            if (cmd == TuyaCommandType.Heartbeat)
            {
                LastPongTime = DateTime.UtcNow;
            }
            else if (cmd == TuyaCommandType.Status)
            {
                string json = ExtractStatusJson(payload);
                try
                {
                    OnStatusPushed?.Invoke(json);
                }
                catch { }
            }

            var pending = _pendingRequest;
            if (pending != null)
            {
                bool matches = false;
                if (pending.SeqNo == seqNo)
                {
                    matches = true;
                }
                else if (pending.ExpectedCmd == cmd)
                {
                    matches = true;
                }
                else if (cmd == TuyaCommandType.Status &&
                         (pending.ExpectedCmd == TuyaCommandType.DpQuery ||
                          pending.ExpectedCmd == TuyaCommandType.DpQueryNew ||
                          pending.ExpectedCmd == TuyaCommandType.Control ||
                          pending.ExpectedCmd == TuyaCommandType.ControlNew))
                {
                    matches = true;
                }

                if (matches)
                {
                    pending.Tcs.TrySetResult((cmd, payload));
                }
            }
        }

        private string ExtractStatusJson(byte[] payload)
        {
            if (payload == null || payload.Length == 0) return "{}";

            if (IsVersion35)
            {
                return TuyaHeader.ExtractJsonString(payload);
            }

            string candidate = TuyaHeader.ExtractJsonString(payload);
            if (candidate.StartsWith('{') && candidate.EndsWith('}'))
            {
                return candidate;
            }

            try
            {
                byte[] cipher = payload;
                if (cipher.Length > 16 && Encoding.Latin1.GetString(cipher, 0, 3) == "3.3")
                {
                    cipher = cipher[16..];
                }
                byte[] decrypted = TuyaCrypto.AesEcbDecrypt(cipher, _localKeyBytes);
                return TuyaHeader.ExtractJsonString(decrypted);
            }
            catch
            {
                return candidate;
            }
        }

        private void FailPendingRequests(Exception ex)
        {
            var pending = _pendingRequest;
            if (pending != null)
            {
                pending.Tcs.TrySetException(ex);
            }
        }

        #endregion

        #region Heartbeat and Send

        /// <summary>
        /// Sends a keep-alive heartbeat frame (Cmd 0x09) with an empty payload.
        /// </summary>
        public async Task SendHeartbeatAsync(CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

            uint seq = (uint)Interlocked.Increment(ref _seqNo);
            byte[] packet;
            if (IsVersion35)
            {
                if (_sessionKey == null)
                    throw new InvalidOperationException("Session key not negotiated for v3.5 heartbeat.");
                packet = TuyaPacket.Pack6699(TuyaCommandType.Heartbeat, seq, Array.Empty<byte>(), _sessionKey);
            }
            else
            {
                packet = TuyaPacket.Pack55AA(TuyaCommandType.Heartbeat, seq, Array.Empty<byte>());
            }

            await SendAsync(packet, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends raw packet bytes over the active stream in a thread-safe manner.
        /// </summary>
        public async Task SendAsync(byte[] packet, CancellationToken cancellationToken = default)
        {
            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_stream != null)
                {
                    await _stream.WriteAsync(packet, cancellationToken).ConfigureAwait(false);
                    await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }

        #endregion

        #region Thread-Safe Request Dispatching

        private async Task<(TuyaCommandType Cmd, byte[] Payload)> SendReceiveCoreAsync(
            TuyaCommandType cmd, Func<uint, byte[]> packetFactory, string protocolName, CancellationToken cancellationToken)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

                        var tcs = new TaskCompletionSource<(TuyaCommandType Cmd, byte[] Payload)>(TaskCreationOptions.RunContinuationsAsynchronously);
                        uint seq = (uint)Interlocked.Increment(ref _seqNo);
                        _pendingRequest = new PendingRequest(seq, cmd, tcs);

                        byte[] packet = packetFactory(seq);
                        await SendAsync(packet, cancellationToken).ConfigureAwait(false);

                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        timeoutCts.CancelAfter(4000);
                        using var reg = timeoutCts.Token.Register(() => tcs.TrySetCanceled(timeoutCts.Token));

                        var (respCmd, respPayload) = await tcs.Task.ConfigureAwait(false);
                        return (respCmd, respPayload);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        Disconnect();
                        if (attempt == 1) throw new TimeoutException($"Timed out waiting for response to Tuya {protocolName} cmd {cmd}.");
                    }
                    catch
                    {
                        Disconnect();
                        if (attempt == 1) throw;
                    }
                    finally
                    {
                        _pendingRequest = null;
                    }
                }
                throw new InvalidOperationException($"Failed to exchange Tuya {protocolName} packet after retry.");
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Sends a 6699 message and returns the decrypted payload. Reconnects on connection loss.
        /// </summary>
        public Task<(TuyaCommandType Cmd, byte[] Plaintext)> SendReceive6699Async(TuyaCommandType cmd, byte[] payload, CancellationToken cancellationToken)
        {
            return SendReceiveCoreAsync(cmd, seq => TuyaPacket.Pack6699(cmd, seq, payload, _sessionKey!), "6699", cancellationToken);
        }

        /// <summary>
        /// Sends a 55AA message and returns the raw response payload. Reconnects on connection loss.
        /// </summary>
        public Task<(TuyaCommandType Cmd, byte[] Payload)> SendReceive55AAAsync(TuyaCommandType cmd, byte[] payload, CancellationToken cancellationToken)
        {
            return SendReceiveCoreAsync(cmd, seq => TuyaPacket.Pack55AA(cmd, seq, payload), "55AA", cancellationToken);
        }

        #endregion

        public void Dispose()
        {
            Disconnect();
            _lock.Dispose();
            _sendLock.Dispose();
            _connectLock.Dispose();
        }

        private sealed class PendingRequest
        {
            public uint SeqNo { get; }
            public TuyaCommandType ExpectedCmd { get; }
            public TaskCompletionSource<(TuyaCommandType Cmd, byte[] Payload)> Tcs { get; }

            public PendingRequest(uint seqNo, TuyaCommandType expectedCmd, TaskCompletionSource<(TuyaCommandType Cmd, byte[] Payload)> tcs)
            {
                SeqNo = seqNo;
                ExpectedCmd = expectedCmd;
                Tcs = tcs;
            }
        }
    }
}
