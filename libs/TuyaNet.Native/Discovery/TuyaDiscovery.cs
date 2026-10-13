// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TuyaNet.Native.Crypto;
using TuyaNet.Native.Framing;
using TuyaNet.Native.Models;

namespace TuyaNet.Native.Discovery
{
    /// <summary>
    /// Hybrid Tuya device discovery engine combining multi-port UDP broadcast listening
    /// (ports 6666, 6667, 7000) with an active parallel TCP port 6668 local subnet sweep.
    /// Resilient to Wi-Fi/Ethernet router broadcast filtering (e.g. AVM FRITZ!Box, AP Isolation).
    /// </summary>
    public static class TuyaDiscovery
    {
        public static async Task<List<TuyaDiscoveredDevice>> ScanAsync(TimeSpan timeout = default, CancellationToken cancellationToken = default)
        {
            if (timeout == default) timeout = TimeSpan.FromMilliseconds(4000);

            var results = new Dictionary<string, TuyaDiscoveredDevice>(StringComparer.OrdinalIgnoreCase);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            UdpClient? udp6666 = null;
            UdpClient? udp6667 = null;
            UdpClient? udp7000 = null;

            try
            {
                // 1. Initialize UDP Listeners across all standard Tuya broadcast ports
                udp6666 = CreateUdpListener(6666);
                udp6667 = CreateUdpListener(6667);
                udp7000 = CreateUdpListener(7000);

                // 2. Broadcast active query probes to stimulate device replies
                await SendBroadcastQueriesAsync(udp7000, udp6667, udp6666).ConfigureAwait(false);

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
                        string ver = root.TryGetProperty("version", out var verElem) ? verElem.GetString() ?? "3.3" : "3.3";
                        string prod = root.TryGetProperty("productKey", out var pkElem) ? pkElem.GetString() ?? "Tuya Device" : "Tuya Device";

                        lock (results)
                        {
                            results[ip] = new TuyaDiscoveredDevice
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
                if (udp6666 != null) listenTasks.Add(ListenLoopAsync(udp6666));
                if (udp6667 != null) listenTasks.Add(ListenLoopAsync(udp6667));
                if (udp7000 != null) listenTasks.Add(ListenLoopAsync(udp7000));

                // 3. Parallel Active Subnet Sweep on TCP Port 6668 (bypasses router AP isolation / UDP filter)
                var tcpSweepTask = PerformTcpSubnetSweepAsync(results, cts.Token);

                await Task.WhenAll(
                    Task.WhenAny(Task.WhenAll(listenTasks), Task.Delay(timeout, cts.Token)),
                    tcpSweepTask
                ).ConfigureAwait(false);
            }
            finally
            {
                cts.Cancel();
                udp6666?.Dispose();
                udp6667?.Dispose();
                udp7000?.Dispose();
            }

            return new List<TuyaDiscoveredDevice>(results.Values);
        }

        private static UdpClient? CreateUdpListener(int port)
        {
            try
            {
                var client = new UdpClient();
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                client.Client.Bind(new IPEndPoint(IPAddress.Any, port));
                client.EnableBroadcast = true;
                return client;
            }
            catch
            {
                return null;
            }
        }

        private static async Task SendBroadcastQueriesAsync(UdpClient? udp7000, UdpClient? udp6667, UdpClient? udp6666)
        {
            try
            {
                byte[] queryBytes = Encoding.UTF8.GetBytes("{\"from\":\"app\"}");
                byte[] packet7000 = TuyaPacket.Pack6699(TuyaCommandType.ReqDevInfo, 0, queryBytes, TuyaCrypto.UdpKey);

                var broadcastTargets = new List<IPAddress> { IPAddress.Broadcast };

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
                            if (ipBytes[0] == 169 && ipBytes[1] == 254) continue; // skip APIPA

                            byte[] bcastBytes = new byte[4];
                            for (int i = 0; i < 4; i++) bcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
                            broadcastTargets.Add(new IPAddress(bcastBytes));
                        }
                    }
                }

                foreach (var bcast in broadcastTargets)
                {
                    try
                    {
                        if (udp7000 != null)
                            await udp7000.SendAsync(packet7000, packet7000.Length, new IPEndPoint(bcast, 7000)).ConfigureAwait(false);
                        if (udp6667 != null)
                            await udp6667.SendAsync(queryBytes, queryBytes.Length, new IPEndPoint(bcast, 6667)).ConfigureAwait(false);
                        if (udp6666 != null)
                            await udp6666.SendAsync(queryBytes, queryBytes.Length, new IPEndPoint(bcast, 6666)).ConfigureAwait(false);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static async Task PerformTcpSubnetSweepAsync(Dictionary<string, TuyaDiscoveredDevice> results, CancellationToken ct)
        {
            try
            {
                var candidateIps = GetLocalSubnetCandidateIps();
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 96,
                    CancellationToken = ct
                };

                await Parallel.ForEachAsync(candidateIps, parallelOptions, async (ip, token) =>
                {
                    if (token.IsCancellationRequested) return;

                    try
                    {
                        using var tcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                        {
                            NoDelay = true
                        };

                        using var connCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                        connCts.CancelAfter(800); // 800 ms connection timeout per IP

                        await tcp.ConnectAsync(ip, 6668, connCts.Token).ConfigureAwait(false);

                        // Connected! Immediately register device so it is guaranteed present in results
                        string ipStr = ip.ToString();
                        TuyaDiscoveredDevice dev;
                        lock (results)
                        {
                            if (!results.TryGetValue(ipStr, out dev!))
                            {
                                dev = new TuyaDiscoveredDevice
                                {
                                    DeviceId = $"lan_{ipStr}",
                                    Ip = ipStr,
                                    Version = "3.3 / 3.5",
                                    ProductKey = "Tuya Device",
                                    RawJson = $"{{\"ip\":\"{ipStr}\",\"port\":6668}}"
                                };
                                results[ipStr] = dev;
                            }
                        }

                        // Fast non-blocking reverse DNS enrichment (300 ms max)
                        try
                        {
                            var dnsTask = Dns.GetHostEntryAsync(ipStr);
                            var completed = await Task.WhenAny(dnsTask, Task.Delay(300, token)).ConfigureAwait(false);
                            if (completed == dnsTask && dnsTask.IsCompletedSuccessfully)
                            {
                                string hostName = dnsTask.Result.HostName;
                                if (hostName.EndsWith(".fritz.box", StringComparison.OrdinalIgnoreCase))
                                    hostName = hostName[..^10];
                                else if (hostName.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
                                    hostName = hostName[..^6];

                                if (!string.IsNullOrWhiteSpace(hostName))
                                {
                                    dev.ProductKey = $"Tuya Device ({hostName})";
                                }
                            }
                        }
                        catch { }
                    }
                    catch { }
                }).ConfigureAwait(false);
            }
            catch { }
        }

        private static List<IPAddress> GetLocalSubnetCandidateIps()
        {
            var list = new List<IPAddress>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
                        ni.Description.Contains("WSL", StringComparison.OrdinalIgnoreCase) ||
                        ni.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase) ||
                        ni.Description.Contains("VMware", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var ipProps = ni.GetIPProperties();
                    foreach (var u in ipProps.UnicastAddresses)
                    {
                        if (u.Address.AddressFamily == AddressFamily.InterNetwork && u.IPv4Mask != null)
                        {
                            byte[] ip = u.Address.GetAddressBytes();
                            byte[] mask = u.IPv4Mask.GetAddressBytes();

                            // Skip APIPA (169.254.x.x)
                            if (ip[0] == 169 && ip[1] == 254) continue;

                            // Standard /24 subnet (255.255.255.0)
                            if (mask[0] == 255 && mask[1] == 255 && mask[2] == 255 && mask[3] == 0)
                            {
                                for (int host = 1; host <= 254; host++)
                                {
                                    if (host == ip[3]) continue;
                                    list.Add(new IPAddress(new byte[] { ip[0], ip[1], ip[2], (byte)host }));
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return list;
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
                    if (payload[0] == '{') return Encoding.UTF8.GetString(payload);

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
            else
            {
                // Raw AES-128-ECB broadcast packet on ports 6666/6667
                try
                {
                    byte[] decrypted = TuyaCrypto.AesEcbDecrypt(data, TuyaCrypto.UdpKey);
                    string s = TuyaHeader.ExtractJsonString(decrypted);
                    if (!string.IsNullOrWhiteSpace(s) && (s.Contains("gwId") || s.Contains("ip")))
                        return s;
                }
                catch { }
            }

            return "";
        }
    }
}
