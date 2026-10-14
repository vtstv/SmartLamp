// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public async Task<string?> FindLocalIpForDeviceAsync(string devId, int timeoutMs = 2500)
        {
            if (string.IsNullOrWhiteSpace(devId)) return null;
            var list = await ScanLocalNetworkAsync(timeoutMs).ConfigureAwait(false);
            var match = list.FirstOrDefault(d => d.DevId == devId && TuyaSharingCrypto.IsPrivateIp(d.Ip));
            return match?.Ip;
        }
    }
}
