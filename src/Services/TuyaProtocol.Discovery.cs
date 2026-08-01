// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace SmartLampApp.Services
{
    public partial class TuyaProtocol
    {
        public async Task<List<DiscoveredDevice>> AutoDiscoverDevicesAsync()
        {
            return await ScanLocalNetworkAsync();
        }

        public async Task<List<DiscoveredDevice>> ScanLocalNetworkAsync()
        {
            return await Task.Run(() =>
            {
                var list = new List<DiscoveredDevice>();
                try
                {
                    string pythonCmd = "import tinytuya, json; print(json.dumps(tinytuya.deviceScan(verbose=False)))";
                    string output = RunPythonCommandCLI(pythonCmd);

                    if (!string.IsNullOrWhiteSpace(output) && output.StartsWith("{"))
                    {
                        using var doc = JsonDocument.Parse(output);
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            var item = prop.Value;
                            list.Add(new DiscoveredDevice
                            {
                                Ip = item.TryGetProperty("ip", out var ip) ? ip.GetString() ?? "" : "",
                                DevId = item.TryGetProperty("gwId", out var id) ? id.GetString() ?? prop.Name : prop.Name,
                                Version = item.TryGetProperty("version", out var v) ? v.GetString() ?? "3.5" : "3.5",
                                ProductName = item.TryGetProperty("productKey", out var pk) ? pk.GetString() ?? "Tuya Smart Lamp" : "Tuya Smart Lamp"
                            });
                        }
                    }
                }
                catch { }
                return list;
            });
        }
    }
}
