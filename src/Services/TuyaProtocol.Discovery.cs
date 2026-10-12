// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SmartLampApp.Services
{
    public partial class TuyaProtocol
    {
        public async Task<List<DiscoveredDevice>> AutoDiscoverDevicesAsync()
        {
            return await ScanLocalNetworkAsync();
        }

        public async Task<List<DiscoveredDevice>> ScanLocalNetworkAsync(int timeoutMs = 2500)
        {
            var results = new Dictionary<string, DiscoveredDevice>();
            using var cts = new CancellationTokenSource(timeoutMs);

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

                // Send 6699 discovery broadcast request to trigger 3.4/3.5 devices
                if (udp7000 != null)
                {
                    byte[] queryBytes = Encoding.UTF8.GetBytes("{\"from\":\"app\"}");
                    byte[] packet = TuyaLocalClient.Pack6699Direct(1, 0, queryBytes, TuyaLocalClient.UdpKey);

                    try
                    {
                        await udp7000.SendAsync(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, 7000)).ConfigureAwait(false);
                    }
                    catch { }

                    // Also broadcast on local interface subnets
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
                        string json = TuyaLocalClient.DecryptUdpPacket(data, TuyaLocalClient.UdpKey);
                        if (string.IsNullOrWhiteSpace(json) || !json.Contains("gwId")) return;

                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        string devId = root.TryGetProperty("gwId", out var idElem) ? idElem.GetString() ?? "" : "";
                        if (string.IsNullOrWhiteSpace(devId)) return;

                        string ip = root.TryGetProperty("ip", out var ipElem) ? ipElem.GetString() ?? remote.Address.ToString() : remote.Address.ToString();
                        string ver = root.TryGetProperty("version", out var verElem) ? verElem.GetString() ?? "3.5" : "3.5";
                        string prod = root.TryGetProperty("productKey", out var pkElem) ? pkElem.GetString() ?? "Tuya Smart Lamp" : "Tuya Smart Lamp";

                        lock (results)
                        {
                            results[devId] = new DiscoveredDevice
                            {
                                DevId = devId,
                                Ip = ip,
                                Version = ver,
                                ProductName = prod
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

                await Task.WhenAny(Task.WhenAll(listenTasks), Task.Delay(timeoutMs, cts.Token)).ConfigureAwait(false);
            }
            finally
            {
                cts.Cancel();
                udp7000?.Dispose();
                udp6667?.Dispose();
            }

            return new List<DiscoveredDevice>(results.Values);
        }
    }
}
