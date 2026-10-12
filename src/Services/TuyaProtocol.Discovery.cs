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

        public async Task<List<DiscoveredDevice>> ScanLocalNetworkAsync(int timeoutMs = 2500)
        {
            var discovered = await TuyaDiscovery.ScanAsync(TimeSpan.FromMilliseconds(timeoutMs)).ConfigureAwait(false);
            return discovered.Select(d => new DiscoveredDevice
            {
                DevId = d.DeviceId,
                Ip = d.Ip,
                Version = d.Version,
                ProductName = string.IsNullOrWhiteSpace(d.ProductKey) ? "Tuya Smart Lamp" : d.ProductKey
            }).ToList();
        }
    }
}
