// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartLampApp.Models;
using TuyaNet.Native;
using TuyaNet.Native.Discovery;

namespace SmartLampApp.Services
{
    public partial class TuyaProtocol
    {
        public async Task<List<DiscoveredDevice>> AutoDiscoverDevicesAsync()
        {
            return await ScanLocalNetworkAsync();
        }

        public async Task<List<DiscoveredDevice>> ScanLocalNetworkAsync(int timeoutMs = 3000)
        {
            var discovered = await TuyaDiscovery.ScanAsync(TimeSpan.FromMilliseconds(timeoutMs)).ConfigureAwait(false);
            var results = discovered.Select(d => new DiscoveredDevice
            {
                DevId = d.DeviceId,
                Ip = d.Ip,
                Version = d.Version,
                ProductName = string.IsNullOrWhiteSpace(d.ProductKey) ? "Tuya Smart Lamp" : d.ProductKey
            }).ToList();

            // If any known device is currently connected in _localClients, ensure it is represented
            if (_config?.devices != null)
            {
                foreach (var known in _config.devices)
                {
                    if (!string.IsNullOrWhiteSpace(known.Ip) && !results.Any(r => r.Ip == known.Ip))
                    {
                        bool isConnected = false;
                        foreach (var kvp in _localClients)
                        {
                            if (kvp.Key.Contains(known.Ip) && kvp.Value.IsConnected)
                            {
                                isConnected = true;
                                break;
                            }
                        }

                        if (isConnected)
                        {
                            results.Add(new DiscoveredDevice
                            {
                                DevId = known.DevId,
                                Ip = known.Ip,
                                Version = known.Version,
                                ProductName = known.Name
                            });
                        }
                    }
                }
            }

            return results;
        }

        public static Task<bool> ProbeDeviceAsync(string ip, string devId, string localKey, string version = "3.5", int timeoutMs = 1500)
        {
            return TuyaDevice.ProbeAsync(ip, devId, localKey, version, timeoutMs);
        }

        public async Task<string?> FindLocalIpForDeviceAsync(string devId, string? localKey = null, string version = "3.5", int timeoutMs = 3000)
        {
            if (string.IsNullOrWhiteSpace(devId)) return null;

            // 1. Look up localKey and version from config if not provided
            if (string.IsNullOrWhiteSpace(localKey) && _config?.devices != null)
            {
                var existing = _config.devices.FirstOrDefault(d => d.DevId == devId);
                if (existing != null)
                {
                    localKey = existing.LocalKey;
                    if (!string.IsNullOrWhiteSpace(existing.Version)) version = existing.Version;
                }
                else if (_config.dev_id == devId)
                {
                    localKey = _config.local_key;
                }
            }

            // 2. Scan network
            var list = await ScanLocalNetworkAsync(timeoutMs).ConfigureAwait(false);

            // 3. Direct match by device ID (if UDP broadcast provided real devId)
            var directMatch = list.FirstOrDefault(d => d.DevId == devId && TuyaSharingCrypto.IsPrivateIp(d.Ip));
            if (directMatch != null) return directMatch.Ip;

            // 4. Gather candidate private IPs discovered on subnet
            var candidateIps = list
                .Where(d => TuyaSharingCrypto.IsPrivateIp(d.Ip))
                .Select(d => d.Ip)
                .Distinct()
                .ToList();

            if (candidateIps.Count == 0) return null;

            // 5. Cryptographic probe against candidate IPs if localKey is known
            if (!string.IsNullOrWhiteSpace(localKey))
            {
                var probeTasks = candidateIps.Select(async ip =>
                {
                    bool ok = await ProbeDeviceAsync(ip, devId, localKey, version, 1500).ConfigureAwait(false);
                    if (!ok && (version == "3.5" || string.IsNullOrWhiteSpace(version)))
                    {
                        // Fallback attempt with 3.3 in case of protocol difference
                        ok = await ProbeDeviceAsync(ip, devId, localKey, "3.3", 800).ConfigureAwait(false);
                    }
                    return (ip, ok);
                }).ToList();

                var probeResults = await Task.WhenAll(probeTasks).ConfigureAwait(false);
                var matched = probeResults.FirstOrDefault(r => r.ok);
                if (!string.IsNullOrEmpty(matched.ip)) return matched.ip;
            }

            // 6. Single-device heuristic: If only 1 candidate IP found on subnet and <= 1 device in config
            if (candidateIps.Count == 1 && (_config?.devices == null || _config.devices.Count <= 1))
            {
                return candidateIps[0];
            }

            return null;
        }

        public async Task<(int resolved, int total)> AutoResolveAllDeviceIpsAsync(int timeoutMs = 3500)
        {
            if (_config?.devices == null || _config.devices.Count == 0) return (0, 0);

            var list = await ScanLocalNetworkAsync(timeoutMs).ConfigureAwait(false);
            var candidateIps = list
                .Where(d => TuyaSharingCrypto.IsPrivateIp(d.Ip))
                .Select(d => d.Ip)
                .Distinct()
                .ToList();

            int resolvedCount = 0;
            bool configChanged = false;
            var claimedIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dev in _config.devices)
            {
                if (string.IsNullOrWhiteSpace(dev.DevId)) continue;

                string? matchedIp = null;

                // 1. Direct match by DevId from UDP
                var direct = list.FirstOrDefault(d => d.DevId == dev.DevId && TuyaSharingCrypto.IsPrivateIp(d.Ip) && !claimedIps.Contains(d.Ip));
                if (direct != null)
                {
                    matchedIp = direct.Ip;
                }

                // 2. Cryptographic probe against candidate IPs
                if (string.IsNullOrEmpty(matchedIp) && !string.IsNullOrWhiteSpace(dev.LocalKey))
                {
                    var remainingIps = candidateIps.Where(ip => !claimedIps.Contains(ip)).ToList();
                    var probeTasks = remainingIps.Select(async ip =>
                    {
                        bool ok = await ProbeDeviceAsync(ip, dev.DevId, dev.LocalKey, dev.Version, 1200).ConfigureAwait(false);
                        if (!ok && (dev.Version == "3.5" || string.IsNullOrWhiteSpace(dev.Version)))
                        {
                            ok = await ProbeDeviceAsync(ip, dev.DevId, dev.LocalKey, "3.3", 800).ConfigureAwait(false);
                        }
                        return (ip, ok);
                    }).ToList();

                    var probeResults = await Task.WhenAll(probeTasks).ConfigureAwait(false);
                    var match = probeResults.FirstOrDefault(r => r.ok);
                    if (!string.IsNullOrEmpty(match.ip))
                    {
                        matchedIp = match.ip;
                    }
                }

                // 3. Fallback: single device heuristic
                if (string.IsNullOrEmpty(matchedIp) && _config.devices.Count == 1 && candidateIps.Count == 1)
                {
                    matchedIp = candidateIps[0];
                }

                if (!string.IsNullOrEmpty(matchedIp))
                {
                    claimedIps.Add(matchedIp);

                    if (dev.LocalIp != matchedIp || !dev.UseLocalIp || dev.Ip != matchedIp)
                    {
                        if (!string.IsNullOrEmpty(dev.Ip) && !TuyaSharingCrypto.IsPrivateIp(dev.Ip))
                        {
                            dev.ExternalIp = dev.Ip;
                        }
                        dev.LocalIp = matchedIp;
                        dev.Ip = matchedIp;
                        dev.UseLocalIp = true;
                        configChanged = true;
                    }
                    resolvedCount++;
                }
            }

            if (configChanged)
            {
                ConfigManager.Save(_config);
                UpdateConfig(_config);
                ResetLocalClients();
            }

            return (resolvedCount, _config.devices.Count);
        }
    }
}
