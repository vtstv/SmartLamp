// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using TuyaNet.Native.Crypto;
using TuyaNet.Native.Framing;
using TuyaNet.Native.Models;

namespace TuyaNet.Native.Discovery
{
    /// <summary>
    /// Service for discovering Tuya devices on the local network via UDP broadcasts.
    /// </summary>
    public static class TuyaDiscovery
    {
        /// <summary>
        /// Scans the local network for Tuya devices on UDP ports 6666, 6667, and 7000.
        /// </summary>
        /// <param name="timeout">Duration to listen for broadcast responses.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of discovered devices.</returns>
        public static async Task<List<TuyaDiscoveredDevice>> ScanAsync(TimeSpan timeout = default, CancellationToken cancellationToken = default)
        {
            if (timeout == default) timeout = TimeSpan.FromMilliseconds(2500);

            var results = new Dictionary<string, TuyaDiscoveredDevice>();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            UdpClient? udp7000 = null;
            UdpClient? udp6667 = null;

            try
            {
                try
                {
                    udp7000 = new UdpClient();
                    udp7000.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    udp7000.Client.Bind(new IPEndPoint(IPAddress.Any, 7000));
                    udp7000.EnableBroadcast = true;
                }
                catch { }

                try
                {
                    udp6667 = new UdpClient();
                    udp6667.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    udp6667.Client.Bind(new IPEndPoint(IPAddress.Any, 6667));
                    udp6667.EnableBroadcast = true;
                }
                catch { }

                if (udp7000 != null)
                {
                    byte[] queryBytes = Encoding.UTF8.GetBytes("{\"from\":\"app\"}");
                    byte[] packet = TuyaPacket.Pack6699(TuyaCommandType.ReqDevInfo, 0, queryBytes, TuyaCrypto.UdpKey);

                    try
                    {
                        await udp7000.SendAsync(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, 7000)).ConfigureAwait(false);
                    }
                    catch { }

                    foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                            continue;

                        var ipProps = ni.GetIPProperties();
                        foreach (var u in ipProps.UnicastAddresses)
                        {
                            if (u.Address.AddressFamily == AddressFamily.InterNetwork && u.IPv4Mask != null)
                            {
                                byte[] ipBytes = u.Address.GetAddressBytes();
                                byte[] maskBytes = u.IPv4Mask.GetAddressBytes();
                                byte[] bcastBytes = new byte[4];
                                for (int i = 0; i < 4; i++) bcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
                                var bcastAddr = new IPAddress(bcastBytes);
                                try
                                {
                                    await udp7000.SendAsync(packet, packet.Length, new IPEndPoint(bcastAddr, 7000)).ConfigureAwait(false);
                                }
                                catch { }
                            }
                        }
                    }
                }

                void ProcessPacket(byte[] data, IPEndPoint remote)
                {
                    try
                    {
                        string json = DecryptUdpData(data);
                        if (string.IsNullOrWhiteSpace(json) || !json.Contains("gwId")) return;

                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        string devId = root.TryGetProperty("gwId", out var idElem) ? idElem.GetString() ?? "" : "";
                        if (string.IsNullOrWhiteSpace(devId)) return;

                        string ip = root.TryGetProperty("ip", out var ipElem) ? ipElem.GetString() ?? remote.Address.ToString() : remote.Address.ToString();
                        string ver = root.TryGetProperty("version", out var verElem) ? verElem.GetString() ?? "3.5" : "3.5";
                        string prod = root.TryGetProperty("productKey", out var pkElem) ? pkElem.GetString() ?? "Tuya Device" : "Tuya Device";

                        lock (results)
                        {
                            results[devId] = new TuyaDiscoveredDevice
                            {
                                DeviceId = devId,
                                Ip = ip,
                                Version = ver,
                                ProductKey = prod,
                                RawJson = json
                            };
                        }
                    }
                    catch { }
                }

                async Task ListenLoopAsync(UdpClient? client)
                {
                    if (client == null) return;
                    while (!cts.IsCancellationRequested)
                    {
                        try
                        {
                            var res = await client.ReceiveAsync(cts.Token).ConfigureAwait(false);
                            ProcessPacket(res.Buffer, res.RemoteEndPoint);
                        }
                        catch (OperationCanceledException) { break; }
                        catch { }
                    }
                }

                var listenTasks = new List<Task>();
                if (udp7000 != null) listenTasks.Add(ListenLoopAsync(udp7000));
                if (udp6667 != null) listenTasks.Add(ListenLoopAsync(udp6667));

                await Task.WhenAny(Task.WhenAll(listenTasks), Task.Delay(timeout, cts.Token)).ConfigureAwait(false);
            }
            finally
            {
                cts.Cancel();
                udp7000?.Dispose();
                udp6667?.Dispose();
            }

            return new List<TuyaDiscoveredDevice>(results.Values);
        }

        private static string DecryptUdpData(byte[] data)
        {
            if (data == null || data.Length < 16) return "";

            uint prefix = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(0, 4));
            if (prefix == TuyaHeader.Prefix6699 && data.Length >= 38)
            {
                uint length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(14, 4));
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
                        TuyaCrypto.AesGcmDecrypt(TuyaCrypto.UdpKey, iv, ciphertext, tag, plaintext, aad);
                        return TuyaHeader.ExtractJsonString(plaintext);
                    }
                }
            }
            else if (prefix == TuyaHeader.Prefix55AA && data.Length >= 24)
            {
                uint length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(12, 4));
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
                        byte[] decrypted = TuyaCrypto.AesEcbDecrypt(payload, TuyaCrypto.UdpKey);
                        return TuyaHeader.ExtractJsonString(decrypted);
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
    }
}
