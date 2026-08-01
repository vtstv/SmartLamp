// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

using SmartLampApp.Models;

namespace SmartLampApp.Services
{
    public partial class TuyaProtocol
    {
        private async Task<bool> SendCloudCommandAsync(string cmdType, object? extra = null, DeviceInfo? target = null)
        {
            var dev = target ?? _activeDevice;
            if (dev == null || string.IsNullOrWhiteSpace(_config.access_id) || string.IsNullOrWhiteSpace(_config.access_key)) return false;

            string plainKey = ConfigManager.DecryptSecret(_config.access_key);
            var payloadDict = new Dictionary<string, object>
            {
                ["action"] = "cloud_command",
                ["cmd_type"] = cmdType,
                ["dev_id"] = dev.DevId,
                ["region"] = string.IsNullOrWhiteSpace(_config.region) ? "eu" : _config.region,
                ["access_id"] = _config.access_id,
                ["access_key"] = plainKey
            };

            if (extra != null)
            {
                foreach (var prop in extra.GetType().GetProperties())
                {
                    payloadDict[prop.Name] = prop.GetValue(extra) ?? "";
                }
            }

            string output = await SendBridgeRequestAsync(payloadDict, "");
            return output.Contains("\"success\": true") || (!output.Contains("Error") && !string.IsNullOrWhiteSpace(output));
        }

        private async Task<LampStatus> GetCloudStatusAsync(DeviceInfo? target = null)
        {
            var dev = target ?? _activeDevice;
            var status = new LampStatus();
            if (dev == null || string.IsNullOrWhiteSpace(_config.access_id) || string.IsNullOrWhiteSpace(_config.access_key))
            {
                status.ErrorMessage = "Tuya Cloud credentials not set";
                status.IsOnline = false;
                return status;
            }

            string plainKey = ConfigManager.DecryptSecret(_config.access_key);
            var payload = new
            {
                action = "cloud_command",
                cmd_type = "status",
                dev_id = dev.DevId,
                region = string.IsNullOrWhiteSpace(_config.region) ? "eu" : _config.region,
                access_id = _config.access_id,
                access_key = plainKey
            };

            string output = await SendBridgeRequestAsync(payload, "");
            if (string.IsNullOrWhiteSpace(output) || output.Contains("\"error\""))
            {
                status.ErrorMessage = "Cloud status request failed";
                status.IsOnline = false;
                return status;
            }

            return ParseStatusJson(output);
        }

        public async Task<(string localKey, string error)> AutoFetchKeyFromCloudAsync(string accessId, string accessKey, string region, string devId = "")
        {
            return await FetchLocalKeyFromCloudAsync(accessId, accessKey, region, devId);
        }

        public async Task<(string localKey, string error)> FetchLocalKeyFromCloudAsync(string accessId, string accessKey, string region, string devId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string pythonCmd = $"import tinytuya, json; c=tinytuya.Cloud('{region}', '{accessId}', '{accessKey}'); print(json.dumps(c.getdevices()))";
                    string output = RunPythonCommandCLI(pythonCmd);
                    if (string.IsNullOrWhiteSpace(output) || output.Contains("error"))
                    {
                        return ("", "Failed to connect to Tuya Cloud. Verify credentials.");
                    }

                    using var doc = JsonDocument.Parse(output);
                    var root = doc.RootElement;

                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in root.EnumerateArray())
                        {
                            string id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                            if (id == devId || string.IsNullOrEmpty(devId))
                            {
                                string key = item.TryGetProperty("key", out var kProp) ? kProp.GetString() ?? "" : "";
                                if (!string.IsNullOrEmpty(key)) return (key, "");
                            }
                        }
                    }
                    return ("", "Device not found in cloud account.");
                }
                catch (Exception ex)
                {
                    return ("", ex.Message);
                }
            });
        }
    }
}
