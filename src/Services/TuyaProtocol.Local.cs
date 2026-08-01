// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Text.Json;
using System.Threading.Tasks;
using SmartLampApp.Models;

namespace SmartLampApp.Services
{
    public partial class TuyaProtocol
    {
        private async Task<LampStatus> GetLocalStatusAsync(DeviceInfo? target = null)
        {
            var dev = target ?? _activeDevice;
            var status = new LampStatus();
            if (dev == null || string.IsNullOrWhiteSpace(dev.LocalKey))
            {
                status.ErrorMessage = "Local Key not configured";
                status.IsOnline = false;
                status.IsPowerOn = false;
                return status;
            }

            var payload = new
            {
                action = "status",
                dev_id = dev.DevId,
                ip = dev.Ip,
                local_key = dev.LocalKey,
                version = dev.Version
            };

            string pythonFallback = $"import tinytuya, json; b=tinytuya.BulbDevice('{dev.DevId}', '{dev.Ip}', '{dev.LocalKey}', version={dev.Version}); print(json.dumps(b.status()))";
            string output = await SendBridgeRequestAsync(payload, pythonFallback);

            if (string.IsNullOrWhiteSpace(output) ||
                output.ToLowerInvariant().Contains("\"error\"") ||
                output.ToLowerInvariant().Contains("\"err\"") ||
                output.ToLowerInvariant().Contains("\"success\": false"))
            {
                status.ErrorMessage = "Device offline or key error";
                status.IsOnline = false;
                return status;
            }

            return ParseStatusJson(output);
        }

        private LampStatus ParseStatusJson(string output)
        {
            var status = new LampStatus();
            string lowerOutput = output != null ? output.ToLowerInvariant() : "";

            if (string.IsNullOrWhiteSpace(output) || lowerOutput.Contains("\"error\""))
            {
                status.ErrorMessage = "Device error";
                status.IsOnline = false;
                return status;
            }

            status.IsOnline = true;

            try
            {
                using var doc = JsonDocument.Parse(output);
                var root = doc.RootElement;
                JsonElement dps;
                if (root.TryGetProperty("status", out var stElement) && stElement.TryGetProperty("dps", out dps) ||
                    root.TryGetProperty("dps", out dps))
                {
                    string[] powerDpKeys = new[] { "20", "1", "101", "switch_led", "switch_1", "switch" };
                    foreach (var key in powerDpKeys)
                    {
                        if (dps.TryGetProperty(key, out var pVal))
                        {
                            if (pVal.ValueKind == JsonValueKind.True ||
                                (pVal.ValueKind == JsonValueKind.Number && pVal.GetInt32() == 1) ||
                                (pVal.ValueKind == JsonValueKind.String && pVal.GetString()?.ToLowerInvariant() == "true"))
                            {
                                status.IsPowerOn = true;
                                break;
                            }
                            if (pVal.ValueKind == JsonValueKind.False)
                            {
                                status.IsPowerOn = false;
                                break;
                            }
                        }
                    }

                    if (dps.TryGetProperty("22", out var b22))
                    {
                        int bRaw = b22.GetInt32();
                        status.Brightness = Math.Max(1, bRaw / 10);
                    }
                    else if (dps.TryGetProperty("3", out var b3))
                    {
                        int bRaw = b3.GetInt32();
                        status.Brightness = Math.Max(1, bRaw > 255 ? bRaw / 10 : (bRaw * 100 / 255));
                    }

                    if (dps.TryGetProperty("23", out var t23))
                    {
                        int tRaw = t23.GetInt32();
                        int pct = Math.Clamp(tRaw / 10, 0, 100);
                        status.ColorTempK = 2700 + (pct * 38);
                    }
                    else if (dps.TryGetProperty("4", out var t4))
                    {
                        int tRaw = t4.GetInt32();
                        int pct = Math.Clamp(tRaw > 255 ? tRaw / 10 : (tRaw * 100 / 255), 0, 100);
                        status.ColorTempK = 2700 + (pct * 38);
                    }
                }
            }
            catch
            {
                status.IsPowerOn = lowerOutput.Contains("\"20\":true") || lowerOutput.Contains("\"20\": true") ||
                                   lowerOutput.Contains("\"1\":true") || lowerOutput.Contains("\"1\": true") ||
                                   lowerOutput.Contains("\"101\":true") || lowerOutput.Contains("\"101\": true") ||
                                   lowerOutput.Contains("\"20\":1") || lowerOutput.Contains("\"20\": 1");
            }

            return status;
        }

        private async Task<bool> SetPowerLocalAsync(bool on, DeviceInfo? target = null)
        {
            var dev = target ?? _activeDevice;
            if (dev == null || string.IsNullOrWhiteSpace(dev.LocalKey)) return false;

            var payload = new
            {
                action = "set_power",
                dev_id = dev.DevId,
                ip = dev.Ip,
                local_key = dev.LocalKey,
                version = dev.Version,
                power = on
            };
            string stateStr = on ? "True" : "False";
            string fallback = $"import tinytuya; b=tinytuya.BulbDevice('{dev.DevId}', '{dev.Ip}', '{dev.LocalKey}', version={dev.Version}); b.set_status({stateStr}, switch=20)";
            string output = await SendBridgeRequestAsync(payload, fallback);
            return output.Contains("\"success\": true") || !output.Contains("Error");
        }

        private async Task<bool> TogglePowerLocalAsync(DeviceInfo? target = null)
        {
            var dev = target ?? _activeDevice;
            if (dev == null || string.IsNullOrWhiteSpace(dev.LocalKey)) return false;

            var payload = new
            {
                action = "toggle",
                dev_id = dev.DevId,
                ip = dev.Ip,
                local_key = dev.LocalKey,
                version = dev.Version
            };
            string fallback = $"import tinytuya, json; b=tinytuya.BulbDevice('{dev.DevId}', '{dev.Ip}', '{dev.LocalKey}', version={dev.Version}); s=b.status(); cur=s.get('dps',{{}}).get('20',False); b.set_status(not cur, switch=20)";
            string output = await SendBridgeRequestAsync(payload, fallback);
            return output.Contains("\"success\": true") || !output.Contains("Error");
        }

        private async Task<bool> SetBrightnessLocalAsync(int percent, DeviceInfo? target = null)
        {
            var dev = target ?? _activeDevice;
            if (dev == null || string.IsNullOrWhiteSpace(dev.LocalKey)) return false;

            var payload = new
            {
                action = "set_brightness",
                dev_id = dev.DevId,
                ip = dev.Ip,
                local_key = dev.LocalKey,
                version = dev.Version,
                brightness = percent
            };
            string fallback = $"import tinytuya; b=tinytuya.BulbDevice('{dev.DevId}', '{dev.Ip}', '{dev.LocalKey}', version={dev.Version}); b.set_brightness_percentage({percent})";
            string output = await SendBridgeRequestAsync(payload, fallback);
            return output.Contains("\"success\": true") || !output.Contains("Error");
        }

        private async Task<bool> SetColorTempLocalAsync(int valV2, DeviceInfo? target = null)
        {
            var dev = target ?? _activeDevice;
            if (dev == null || string.IsNullOrWhiteSpace(dev.LocalKey)) return false;

            var payload = new
            {
                action = "set_temp",
                dev_id = dev.DevId,
                ip = dev.Ip,
                local_key = dev.LocalKey,
                version = dev.Version,
                temp = valV2
            };
            string fallback = $"import tinytuya; b=tinytuya.BulbDevice('{dev.DevId}', '{dev.Ip}', '{dev.LocalKey}', version={dev.Version}); b.set_multiple_values({{20: True, 21: 'white', 23: {valV2}}})";
            string output = await SendBridgeRequestAsync(payload, fallback);
            return output.Contains("\"success\": true") || !output.Contains("Error");
        }

        private async Task<bool> SetColorHexLocalAsync(string tuyaHex, DeviceInfo? target = null)
        {
            var dev = target ?? _activeDevice;
            if (dev == null || string.IsNullOrWhiteSpace(dev.LocalKey)) return false;

            var payload = new
            {
                action = "set_color",
                dev_id = dev.DevId,
                ip = dev.Ip,
                local_key = dev.LocalKey,
                version = dev.Version,
                hex = tuyaHex
            };
            string fallback = $"import tinytuya; b=tinytuya.BulbDevice('{dev.DevId}', '{dev.Ip}', '{dev.LocalKey}', version={dev.Version}); b.set_multiple_values({{20: True, 21: 'colour', 24: '{tuyaHex}'}})";
            string output = await SendBridgeRequestAsync(payload, fallback);
            return output.Contains("\"success\": true") || !output.Contains("Error");
        }

        private async Task<bool> SetPresetLocalAsync(string mode, int brightness, int tempV2, string tuyaHex, DeviceInfo? target = null)
        {
            var dev = target ?? _activeDevice;
            if (dev == null || string.IsNullOrWhiteSpace(dev.LocalKey)) return false;
            int brightV2 = brightness * 10;

            if (mode == "colour")
            {
                var payload = new
                {
                    action = "set_preset",
                    dev_id = dev.DevId,
                    ip = dev.Ip,
                    local_key = dev.LocalKey,
                    version = dev.Version,
                    mode = "colour",
                    brightness = brightness,
                    hex = tuyaHex
                };
                string fallback = $"import tinytuya; b=tinytuya.BulbDevice('{dev.DevId}', '{dev.Ip}', '{dev.LocalKey}', version={dev.Version}); b.set_multiple_values({{20: True, 21: 'colour', 22: {brightV2}, 24: '{tuyaHex}'}})";
                string output = await SendBridgeRequestAsync(payload, fallback);
                return output.Contains("\"success\": true") || !output.Contains("Error");
            }
            else
            {
                var payload = new
                {
                    action = "set_preset",
                    dev_id = dev.DevId,
                    ip = dev.Ip,
                    local_key = dev.LocalKey,
                    version = dev.Version,
                    mode = "white",
                    brightness = brightness,
                    temp = tempV2
                };
                string fallback = $"import tinytuya; b=tinytuya.BulbDevice('{dev.DevId}', '{dev.Ip}', '{dev.LocalKey}', version={dev.Version}); b.set_multiple_values({{20: True, 21: 'white', 22: {brightV2}, 23: {tempV2}}})";
                string output = await SendBridgeRequestAsync(payload, fallback);
                return output.Contains("\"success\": true") || !output.Contains("Error");
            }
        }
    }
}
