// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SmartLampApp.Models;

namespace SmartLampApp.Services
{
    public class LampStatus
    {
        public bool IsOnline { get; set; } = false;
        public bool IsPowerOn { get; set; } = false;
        public string Mode { get; set; } = "white";
        public int Brightness { get; set; } = 0;
        public int ColorTempK { get; set; } = 0;
        public string ColorHex { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
    }

    public class DiscoveredDevice
    {
        public string Ip { get; set; } = "";
        public string DevId { get; set; } = "";
        public string Version { get; set; } = "3.5";
        public string ProductName { get; set; } = "Tuya Smart Device";
    }

    public class TuyaProtocol
    {
        private LampConfig _config;
        private DeviceInfo _activeDevice;
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private static Process? _bridgeDaemon = null;
        private static string? _extractedExePath = null;
        private const int ServerPort = 18889;

        public TuyaProtocol(LampConfig config)
        {
            _config = config;
            _activeDevice = GetActiveDevice();
            EnsureBridgeDaemonRunning();
        }

        public void UpdateConfig(LampConfig config)
        {
            _config = config;
            _activeDevice = GetActiveDevice();
        }

        public DeviceInfo GetActiveDevice()
        {
            if (_config.devices != null && _config.devices.Count > 0)
            {
                var dev = _config.devices.Find(d => d.DevId == _config.selected_dev_id || d.Ip == _config.selected_dev_id);
                if (dev == null) dev = _config.devices[0];

                if ((string.IsNullOrWhiteSpace(dev.DevId) || dev.DevId.Contains(".") || dev.DevId == dev.Ip) && !string.IsNullOrWhiteSpace(_config.dev_id) && !_config.dev_id.Contains("."))
                {
                    dev.DevId = _config.dev_id;
                }
                if (string.IsNullOrWhiteSpace(dev.LocalKey) && !string.IsNullOrWhiteSpace(_config.local_key))
                {
                    dev.LocalKey = _config.local_key;
                }
                return dev;
            }

            if (!string.IsNullOrWhiteSpace(_config.local_key))
            {
                return new DeviceInfo
                {
                    Name = "Smart Lamp 1",
                    Ip = string.IsNullOrWhiteSpace(_config.ip) ? "192.168.0.68" : _config.ip,
                    DevId = string.IsNullOrWhiteSpace(_config.dev_id) ? "bf5844a34bb0422e67tmbi" : _config.dev_id,
                    LocalKey = _config.local_key,
                    Version = string.IsNullOrWhiteSpace(_config.version) ? "3.5" : _config.version
                };
            }

            return new DeviceInfo();
        }

        public bool IsConfigured => _activeDevice != null && !string.IsNullOrWhiteSpace(_activeDevice.LocalKey);

        private static string GetBridgeExecutablePath()
        {
            if (_extractedExePath != null && File.Exists(_extractedExePath)) return _extractedExePath;

            // 1. Check local directory
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tuya_bridge.exe");
            if (File.Exists(localPath))
            {
                _extractedExePath = localPath;
                return localPath;
            }

            // 2. Extract embedded resource if not found in local dir
            string tempDir = Path.Combine(Path.GetTempPath(), "SmartLampStudio");
            Directory.CreateDirectory(tempDir);
            string tempPath = Path.Combine(tempDir, "tuya_bridge.exe");

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream("SmartLampApp.TuyaBridge.tuya_bridge.exe");
                if (stream != null)
                {
                    using var fileStream = File.Create(tempPath);
                    stream.CopyTo(fileStream);
                }
            }
            catch { }

            _extractedExePath = File.Exists(tempPath) ? tempPath : "python";
            return _extractedExePath;
        }

        private static void EnsureBridgeDaemonRunning()
        {
            _ = EnsureBridgeDaemonRunningAsync();
        }

        private static readonly SemaphoreSlim _daemonLock = new SemaphoreSlim(1, 1);

        private static async Task EnsureBridgeDaemonRunningAsync()
        {
            await _daemonLock.WaitAsync().ConfigureAwait(false);
            try
            {
                try
                {
                    var res = await _httpClient.GetAsync($"http://127.0.0.1:{ServerPort}/health").ConfigureAwait(false);
                    if (res.IsSuccessStatusCode) return;
                }
                catch { }

                if ((_bridgeDaemon != null && !_bridgeDaemon.HasExited) || Process.GetProcessesByName("tuya_bridge").Length > 0)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        await Task.Delay(250).ConfigureAwait(false);
                        try
                        {
                            var res = await _httpClient.GetAsync($"http://127.0.0.1:{ServerPort}/health").ConfigureAwait(false);
                            if (res.IsSuccessStatusCode) return;
                        }
                        catch { }
                    }
                }

                string exePath = GetBridgeExecutablePath();
                if (exePath == "python" || !File.Exists(exePath)) return;

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"--server {ServerPort}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                _bridgeDaemon = Process.Start(psi);
                if (_bridgeDaemon != null)
                {
                    ChildProcessTracker.AddProcess(_bridgeDaemon);
                }
            }
            catch { }
            finally
            {
                _daemonLock.Release();
            }
        }

        public static void StopDaemon()
        {
            try
            {
                if (_bridgeDaemon != null && !_bridgeDaemon.HasExited)
                {
                    _bridgeDaemon.Kill(true);
                    _bridgeDaemon.Dispose();
                    _bridgeDaemon = null;
                }
            }
            catch { }
        }

        private async Task<string> SendBridgeRequestAsync(object payload, string rawPythonFallback)
        {
            await _semaphore.WaitAsync();
            try
            {
                EnsureBridgeDaemonRunning();
                try
                {
                    string json = JsonSerializer.Serialize(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync($"http://127.0.0.1:{ServerPort}", content);
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStringAsync();
                    }
                }
                catch { }

                // Fallback to single-process CLI if daemon is unreachable
                return RunPythonCommandCLI(rawPythonFallback);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private string RunPythonCommandCLI(string pythonCode)
        {
            string exePath = GetBridgeExecutablePath();

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"-c \"{pythonCode}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null) return "";
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output;
        }

        public async Task<LampStatus> GetStatusAsync(DeviceInfo? target = null)
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

            string lowerOutput = output != null ? output.ToLowerInvariant() : "";

            if (string.IsNullOrWhiteSpace(output) ||
                lowerOutput.Contains("\"error\"") ||
                lowerOutput.Contains("\"err\"") ||
                lowerOutput.Contains("\"success\": false") ||
                lowerOutput.Contains("\"success\":false"))
            {
                status.ErrorMessage = "Device offline or key error";
                status.IsOnline = false;
                status.IsPowerOn = false;
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
                    // 1. Check Power DataPoints (DP 20, 1, 101, switch_led, switch_1, switch)
                    string[] powerDpKeys = new[] { "20", "1", "101", "switch_led", "switch_1", "switch" };
                    foreach (var key in powerDpKeys)
                    {
                        if (dps.TryGetProperty(key, out var pVal))
                        {
                            if (pVal.ValueKind == JsonValueKind.True)
                            {
                                status.IsPowerOn = true;
                                break;
                            }
                            if (pVal.ValueKind == JsonValueKind.Number && pVal.GetInt32() == 1)
                            {
                                status.IsPowerOn = true;
                                break;
                            }
                            if (pVal.ValueKind == JsonValueKind.String && pVal.GetString()?.ToLowerInvariant() == "true")
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

                    // 2. Brightness (DP 22 or 3)
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

                    // 3. Color Temp (DP 23 or 4)
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
                // Fallback string matching if JSON structure was unexpected
                status.IsPowerOn = lowerOutput.Contains("\"20\":true") || lowerOutput.Contains("\"20\": true") ||
                                   lowerOutput.Contains("\"1\":true") || lowerOutput.Contains("\"1\": true") ||
                                   lowerOutput.Contains("\"101\":true") || lowerOutput.Contains("\"101\": true") ||
                                   lowerOutput.Contains("\"20\":1") || lowerOutput.Contains("\"20\": 1");
            }

            return status;
        }

        public async Task<bool> SetPowerAsync(bool on, DeviceInfo? target = null)
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

        public async Task<bool> SetBrightnessAsync(int percent, DeviceInfo? target = null)
        {
            var dev = target ?? _activeDevice;
            if (dev == null || string.IsNullOrWhiteSpace(dev.LocalKey)) return false;
            percent = Math.Clamp(percent, 1, 100);

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

        public async Task<bool> SetColorTempAsync(int kelvin, DeviceInfo? target = null)
        {
            var dev = target ?? _activeDevice;
            if (dev == null || string.IsNullOrWhiteSpace(dev.LocalKey)) return false;
            int pct = kelvin >= 2700 ? (kelvin - 2700) / 38 : kelvin;
            pct = Math.Clamp(pct, 0, 100);
            int valV2 = pct * 10;

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

        public async Task<bool> SetColorHexAsync(string hexCode, DeviceInfo? target = null)
        {
            var dev = target ?? _activeDevice;
            if (dev == null || string.IsNullOrWhiteSpace(dev.LocalKey)) return false;
            hexCode = hexCode.TrimStart('#');
            if (hexCode.Length != 6) return false;

            int r = Convert.ToInt32(hexCode.Substring(0, 2), 16);
            int g = Convert.ToInt32(hexCode.Substring(2, 2), 16);
            int b = Convert.ToInt32(hexCode.Substring(4, 2), 16);
            string tuyaHex = RgbToTuyaV2Hex(r, g, b);

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

        public async Task<bool> SetSceneAsync(string sceneName, DeviceInfo? target = null)
        {
            if (sceneName == "Night") return await SetColorTempAsync(2700, target);
            if (sceneName == "Reading") return await SetColorTempAsync(4000, target);
            if (sceneName == "Working") return await SetColorTempAsync(6500, target);
            if (sceneName == "Relax") return await SetColorHexAsync("#FF7F00", target);
            if (sceneName == "Party") return await SetColorHexAsync("#FF00FF", target);
            if (sceneName == "Rainbow") return await SetColorHexAsync("#00FFFF", target);
            return false;
        }

        public async Task SetGroupPowerAsync(List<DeviceInfo> groupDevices, bool on)
        {
            var tasks = new List<Task>();
            foreach (var dev in groupDevices)
            {
                tasks.Add(SetPowerAsync(on, dev));
            }
            await Task.WhenAll(tasks);
        }

        public async Task SetGroupBrightnessAsync(List<DeviceInfo> groupDevices, int brightness)
        {
            var tasks = new List<Task>();
            foreach (var dev in groupDevices)
            {
                tasks.Add(SetBrightnessAsync(brightness, dev));
            }
            await Task.WhenAll(tasks);
        }

        public async Task SetGroupColorHexAsync(List<DeviceInfo> groupDevices, string hexCode)
        {
            var tasks = new List<Task>();
            foreach (var dev in groupDevices)
            {
                tasks.Add(SetColorHexAsync(hexCode, dev));
            }
            await Task.WhenAll(tasks);
        }

        public async Task<List<DiscoveredDevice>> AutoDiscoverDevicesAsync()
        {
            return await ScanLocalNetworkAsync();
        }

        public async Task<(string localKey, string error)> AutoFetchKeyFromCloudAsync(string accessId, string accessKey, string region, string devId = "")
        {
            return await FetchLocalKeyFromCloudAsync(accessId, accessKey, region, devId);
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

        private static string RgbToTuyaV2Hex(int r, int g, int b)
        {
            float rF = r / 255.0f;
            float gF = g / 255.0f;
            float bF = b / 255.0f;

            float maxC = Math.Max(rF, Math.Max(gF, bF));
            float minC = Math.Min(rF, Math.Min(gF, bF));
            float delta = maxC - minC;

            float h = 0f;
            if (delta > 0.00001f)
            {
                if (maxC == rF) h = (gF - bF) / delta % 6f;
                else if (maxC == gF) h = (bF - rF) / delta + 2f;
                else h = (rF - gF) / delta + 4f;
                h *= 60f;
                if (h < 0f) h += 360f;
            }

            float s = maxC == 0f ? 0f : delta / maxC;
            float v = maxC;

            int hVal = (int)Math.Round(h);
            int sVal = (int)Math.Round(s * 1000.0f);
            int vVal = (int)Math.Round(v * 1000.0f);

            return $"{hVal:x4}{sVal:x4}{vVal:x4}";
        }
    }
}
