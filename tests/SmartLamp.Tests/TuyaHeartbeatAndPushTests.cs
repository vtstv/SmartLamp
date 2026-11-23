// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TuyaNet.Native;
using TuyaNet.Native.Crypto;
using TuyaNet.Native.Framing;
using TuyaNet.Native.Models;
using TuyaNet.Native.Session;
using Xunit;

namespace SmartLamp.Tests
{
    public class TuyaHeartbeatAndPushTests
    {
        private static readonly byte[] TestLocalKey = Encoding.UTF8.GetBytes("0123456789abcdef");

        [Fact]
        public async Task TuyaSession_SendHeartbeatAsync_v33_SendsFrame_AndRecordsPong()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var serverTask = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync(cts.Token);
                var stream = client.GetStream();

                var (cmd, seq, payload) = await TuyaPacket.Read55AAAsync(stream, cts.Token);
                Assert.Equal(TuyaCommandType.Heartbeat, cmd);
                Assert.Empty(payload);

                // Reply with Heartbeat pong
                byte[] pongPacket = TuyaPacket.Pack55AA(TuyaCommandType.Heartbeat, seq, Array.Empty<byte>());
                await stream.WriteAsync(pongPacket, cts.Token);
                await stream.FlushAsync(cts.Token);
            });

            using var session = new TuyaSession("127.0.0.1", port, TestLocalKey, TuyaProtocolVersion.V33);
            await session.SendHeartbeatAsync(cts.Token);

            await serverTask;

            // Wait a brief moment for ReceiveLoopAsync to process the pong
            for (int i = 0; i < 50 && session.LastPongTime == DateTime.MinValue; i++)
            {
                await Task.Delay(20);
            }

            Assert.NotEqual(DateTime.MinValue, session.LastPongTime);
            Assert.True((DateTime.UtcNow - session.LastPongTime).TotalSeconds < 5);

            listener.Stop();
        }

        [Fact]
        public async Task TuyaSession_OnStatusPushed_InvokedOnPushFrame()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            string statusPayload = "{\"dps\":{\"20\":true,\"22\":950}}";

            var serverTask = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync(cts.Token);
                var stream = client.GetStream();

                byte[] packet = TuyaPacket.Pack55AA(TuyaCommandType.Status, 1, Encoding.UTF8.GetBytes(statusPayload));
                await stream.WriteAsync(packet, cts.Token);
                await stream.FlushAsync(cts.Token);

                // Keep connection open long enough for client to read
                await Task.Delay(200, cts.Token);
            });

            using var session = new TuyaSession("127.0.0.1", port, TestLocalKey, TuyaProtocolVersion.V33);
            var pushTcs = new TaskCompletionSource<string>();
            session.OnStatusPushed += json => pushTcs.TrySetResult(json);

            await session.EnsureConnectedAsync(cts.Token);

            var receivedJson = await pushTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(statusPayload, receivedJson);

            await serverTask;
            listener.Stop();
        }

        [Fact]
        public async Task TuyaDevice_OnStatusPushed_And_SendHeartbeatAsync_WorkEndToEnd()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            string pushData = "{\"dps\":{\"20\":false,\"22\":400}}";

            var serverTask = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync(cts.Token);
                var stream = client.GetStream();

                // 1. Send push status
                byte[] pushPacket = TuyaPacket.Pack55AA(TuyaCommandType.Status, 1, Encoding.UTF8.GetBytes(pushData));
                await stream.WriteAsync(pushPacket, cts.Token);
                await stream.FlushAsync(cts.Token);

                // 2. Read heartbeat from device
                var (cmd, seq, payload) = await TuyaPacket.Read55AAAsync(stream, cts.Token);
                Assert.Equal(TuyaCommandType.Heartbeat, cmd);
                Assert.Empty(payload);

                // 3. Reply with heartbeat pong
                byte[] pong = TuyaPacket.Pack55AA(TuyaCommandType.Heartbeat, seq, Array.Empty<byte>());
                await stream.WriteAsync(pong, cts.Token);
                await stream.FlushAsync(cts.Token);
            });

            using var device = new TuyaDevice("127.0.0.1", "test_lamp", "0123456789abcdef", TuyaProtocolVersion.V33, port);
            var statusTcs = new TaskCompletionSource<string>();
            device.OnStatusPushed += json => statusTcs.TrySetResult(json);

            await device.EnsureConnectedAsync(cts.Token);

            var received = await statusTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(pushData, received);

            await device.SendHeartbeatAsync(cts.Token);
            await serverTask;

            for (int i = 0; i < 50 && device.LastPongTime == DateTime.MinValue; i++)
            {
                await Task.Delay(20);
            }

            Assert.NotEqual(DateTime.MinValue, device.LastPongTime);
            listener.Stop();
        }

        [Fact]
        public async Task TuyaSession_SendHeartbeatAsync_v35_CompletesHandshakeAndSendsGcmHeartbeat()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var serverTask = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync(cts.Token);
                var stream = client.GetStream();

                // Step 1: Read client nonce (Cmd 0x03)
                var (cmd1, _, localNonce) = await TuyaPacket.Read6699Async(stream, TestLocalKey, cts.Token);
                Assert.Equal(TuyaCommandType.SessKeyNegStart, cmd1);
                Assert.Equal(16, localNonce.Length);

                // Step 2: Send server nonce + HMAC(localKey, localNonce)
                byte[] remoteNonce = RandomNumberGenerator.GetBytes(16);
                byte[] localNonceHmac = TuyaCrypto.ComputeHmacSha256(TestLocalKey, localNonce);

                byte[] step2Payload = new byte[4 + 16 + 32];
                Buffer.BlockCopy(remoteNonce, 0, step2Payload, 4, 16);
                Buffer.BlockCopy(localNonceHmac, 0, step2Payload, 20, 32);

                byte[] step2Packet = TuyaPacket.Pack6699(TuyaCommandType.SessKeyNegResp, 1, step2Payload, TestLocalKey);
                await stream.WriteAsync(step2Packet, cts.Token);
                await stream.FlushAsync(cts.Token);

                // Step 3: Read client HMAC of remoteNonce (Cmd 0x05)
                var (cmd3, _, clientHmac) = await TuyaPacket.Read6699Async(stream, TestLocalKey, cts.Token);
                Assert.Equal(TuyaCommandType.SessKeyNegFinish, cmd3);

                // Derive session key
                byte[] xorNonce = new byte[16];
                for (int i = 0; i < 16; i++) xorNonce[i] = (byte)(localNonce[i] ^ remoteNonce[i]);

                byte[] iv = localNonce[..12];
                byte[] sessionKey = new byte[16];
                byte[] tag = new byte[16];
                TuyaCrypto.AesGcmEncrypt(TestLocalKey, iv, xorNonce, sessionKey, tag);

                // Step 4: Receive v3.5 AES-GCM Heartbeat (Cmd 0x09)
                var (hbCmd, hbSeq, hbPlaintext) = await TuyaPacket.Read6699Async(stream, sessionKey, cts.Token);
                Assert.Equal(TuyaCommandType.Heartbeat, hbCmd);
                Assert.Empty(hbPlaintext);

                // Step 5: Send v3.5 Heartbeat pong
                byte[] pong = TuyaPacket.Pack6699(TuyaCommandType.Heartbeat, hbSeq, Array.Empty<byte>(), sessionKey);
                await stream.WriteAsync(pong, cts.Token);
                await stream.FlushAsync(cts.Token);
            });

            using var session = new TuyaSession("127.0.0.1", port, TestLocalKey, TuyaProtocolVersion.V35);
            await session.SendHeartbeatAsync(cts.Token);

            await serverTask;

            for (int i = 0; i < 50 && session.LastPongTime == DateTime.MinValue; i++)
            {
                await Task.Delay(20);
            }

            Assert.NotEqual(DateTime.MinValue, session.LastPongTime);
            listener.Stop();
        }
    }
}
