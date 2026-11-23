// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using SmartLampApp.Models;
using TuyaNet.Native;

namespace SmartLampApp.Services
{
    public partial class TuyaProtocol
    {
        private static readonly ConcurrentDictionary<string, TuyaDevice> _localClients = new();
        public static void ResetLocalClients() => _localClients.Clear();

        private TuyaDevice GetLocalDevice(DeviceInfo dev)
        {
            string key = $"{dev.DevId}_{dev.Ip}_{dev.LocalKey}_{dev.Version}";
            return _localClients.GetOrAdd(key, _ =>
            {
                var device = new TuyaDevice(dev.Ip, dev.DevId, dev.LocalKey, dev.Version);
                device.OnStatusPushed += (json) =>
                {
                    try
                    {
                        var status = ParseStatusJson(json);
                        if (status.IsOnline)
                        {
                            dev.last_power_on = status.IsPowerOn;
                        }
                        OnStatusPushed?.Invoke(status);
                    }
                    catch { }
                };
                return device;
            });
        }

        /// <summary>
        /// Explicitly establishes local TCP connection and handshake for the active or target device.
        /// </summary>
        public async Task<bool> EnsureLocalSessionAsync(DeviceInfo? target = null)
        {
            var dev = target ?? _activeDevice;
            if (dev == null || string.IsNullOrWhiteSpace(dev.LocalKey) || string.IsNullOrWhiteSpace(dev.Ip))
            {
                return false;
            }

            try
            {
                var device = GetLocalDevice(dev);
                using var cts = new CancellationTokenSource(4000);
                await device.EnsureConnectedAsync(cts.Token).ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Disconnects the local TCP session and releases socket resources so mobile apps or other clients can connect.
        /// </summary>
        public async Task DisconnectLocalAsync(DeviceInfo? target = null)
        {
            var dev = target ?? _activeDevice;
            if (dev != null)
            {
                string key = $"{dev.DevId}_{dev.Ip}_{dev.LocalKey}_{dev.Version}";
                if (_localClients.TryRemove(key, out var device))
                {
                    try { device.Disconnect(); } catch { }
                    try { device.Dispose(); } catch { }
                }
            }

            foreach (var kvp in _localClients)
            {
                if (_localClients.TryRemove(kvp.Key, out var device))
                {
                    try { device.Disconnect(); } catch { }
                    try { device.Dispose(); } catch { }
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Sends a lightweight Tuya heartbeat frame (0x09) to keep the local session alive.
        /// </summary>
        public async Task<bool> SendHeartbeatAsync(DeviceInfo? target = null)
        {
            var dev = target ?? _activeDevice;
            if (dev == null || string.IsNullOrWhiteSpace(dev.LocalKey) || string.IsNullOrWhiteSpace(dev.Ip))
            {
                return false;
            }

            try
            {
                var device = GetLocalDevice(dev);
                using var cts = new CancellationTokenSource(3000);
                await device.SendHeartbeatAsync(cts.Token).ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }

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

            try
            {
                var device = GetLocalDevice(dev);
                using var cts = new CancellationTokenSource(4000);
                string output = await device.GetStatusJsonAsync(cts.Token).ConfigureAwait(false);
                var result = ParseStatusJson(output);
                if (result.IsOnline)
                {
                    dev.last_power_on = result.IsPowerOn;
                }
                return result;
            }
            catch (Exception ex)
            {
                status.ErrorMessage = ex.Message;
                status.IsOnline = false;
                status.IsPowerOn = false;
                return status;
            }
        }

        public static LampStatus ParseStatusJson(string output)
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
                if ((root.TryGetProperty("data", out var dataElement) && dataElement.TryGetProperty("dps", out dps)) ||
                    (root.TryGetProperty("status", out var stElement) && stElement.TryGetProperty("dps", out dps)) ||
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

                    if (dps.TryGetProperty("21", out var m21))
                    {
                        status.Mode = m21.GetString() ?? "white";
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
                        int kelvin = (int)Math.Round(2700.0 + (Math.Clamp(tRaw, 0, 1000) / 1000.0) * 3800.0);
                        status.ColorTempK = Math.Clamp(kelvin, 2700, 6500);
                    }
                    else if (dps.TryGetProperty("4", out var t4))
                    {
                        int tRaw = t4.GetInt32();
                        double normalized = tRaw > 255 ? Math.Clamp(tRaw, 0, 1000) / 1000.0 : Math.Clamp(tRaw, 0, 255) / 255.0;
                        int kelvin = (int)Math.Round(2700.0 + normalized * 3800.0);
                        status.ColorTempK = Math.Clamp(kelvin, 2700, 6500);
                    }

                    if (dps.TryGetProperty("24", out var c24))
                    {
                        string tuyaHex = c24.GetString() ?? "";
                        if (tuyaHex.Length >= 12)
                        {
                            try
                            {
                                int h = Convert.ToInt32(tuyaHex.Substring(0, 4), 16);
                                int s = Convert.ToInt32(tuyaHex.Substring(4, 4), 16);
                                int v = Convert.ToInt32(tuyaHex.Substring(8, 4), 16);
                                status.ColorHex = TuyaHsvToRgbHex(h, s, v);
                            }
                            catch { }
                        }
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

            try
            {
                var device = GetLocalDevice(dev);
                using var cts = new CancellationTokenSource(3000);
                bool ok = await device.SetPowerAsync(on, "20", cts.Token).ConfigureAwait(false);
                if (ok)
                {
                    dev.last_power_on = on;
                    ConfigManager.Save(_config);
                }
                return ok;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> TogglePowerLocalAsync(DeviceInfo? target = null)
        {
            var dev = target ?? _activeDevice;
            if (dev == null || string.IsNullOrWhiteSpace(dev.LocalKey)) return false;

            try
            {
                var device = GetLocalDevice(dev);
                var curStatus = await GetLocalStatusAsync(dev).ConfigureAwait(false);
                bool targetPower = curStatus.IsOnline ? !curStatus.IsPowerOn : !dev.last_power_on;
                using var cts = new CancellationTokenSource(3000);
                bool ok = await device.SetPowerAsync(targetPower, "20", cts.Token).ConfigureAwait(false);
                if (ok)
                {
                    dev.last_power_on = targetPower;
                    ConfigManager.Save(_config);
                }
                return ok;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> SetBrightnessLocalAsync(int percent, DeviceInfo? target = null)
        {
            var dev = target ?? _activeDevice;
            if (dev == null || string.IsNullOrWhiteSpace(dev.LocalKey)) return false;

            try
            {
                var device = GetLocalDevice(dev);
                int brightVal = Math.Clamp(percent * 10, 10, 1000);
                var dps = new Dictionary<string, object>
                {
                    ["20"] = true,
                    ["22"] = brightVal
                };
                using var cts = new CancellationTokenSource(3000);
                return await device.SetDpsAsync(dps, cts.Token).ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> SetColorTempLocalAsync(int valV2, DeviceInfo? target = null)
        {
            var dev = target ?? _activeDevice;
            if (dev == null || string.IsNullOrWhiteSpace(dev.LocalKey)) return false;

            try
            {
                var device = GetLocalDevice(dev);
                var dps = new Dictionary<string, object>
                {
                    ["20"] = true,
                    ["21"] = "white",
                    ["23"] = Math.Clamp(valV2, 0, 1000)
                };
                using var cts = new CancellationTokenSource(3000);
                return await device.SetDpsAsync(dps, cts.Token).ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> SetColorHexLocalAsync(string tuyaHex, DeviceInfo? target = null)
        {
            var dev = target ?? _activeDevice;
            if (dev == null || string.IsNullOrWhiteSpace(dev.LocalKey)) return false;

            try
            {
                var device = GetLocalDevice(dev);
                var dps = new Dictionary<string, object>
                {
                    ["20"] = true,
                    ["21"] = "colour",
                    ["24"] = tuyaHex
                };
                using var cts = new CancellationTokenSource(3000);
                return await device.SetDpsAsync(dps, cts.Token).ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> SetPresetLocalAsync(string mode, int brightness, int tempV2, string tuyaHex, DeviceInfo? target = null)
        {
            var dev = target ?? _activeDevice;
            if (dev == null || string.IsNullOrWhiteSpace(dev.LocalKey)) return false;

            try
            {
                var device = GetLocalDevice(dev);
                int brightV2 = Math.Clamp(brightness * 10, 10, 1000);
                var dps = new Dictionary<string, object>
                {
                    ["20"] = true,
                    ["21"] = mode,
                    ["22"] = brightV2
                };
                if (mode == "colour")
                {
                    dps["24"] = tuyaHex;
                }
                else
                {
                    dps["23"] = Math.Clamp(tempV2, 0, 1000);
                }
                using var cts = new CancellationTokenSource(3000);
                return await device.SetDpsAsync(dps, cts.Token).ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }
    }
}
