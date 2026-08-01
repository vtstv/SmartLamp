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

    public partial class TuyaProtocol
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

                // Wait for the daemon to become ready after starting
                for (int i = 0; i < 40; i++) // up to 10 seconds
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
            catch { }
            finally
            {
                _daemonLock.Release();
            }
        }

        public async Task WaitForBridgeReadyAsync()
        {
            await EnsureBridgeDaemonRunningAsync();
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

        private async Task<bool> DispatchCommandAsync(Func<Task<bool>> localAction, Func<Task<bool>> cloudAction)
        {
            string mode = _config.control_mode ?? "auto";
            string priority = _config.auto_priority ?? "local_first";

            if (mode == "local") return await localAction();
            if (mode == "cloud") return await cloudAction();

            if (priority == "cloud_first")
            {
                bool cloudSuccess = await cloudAction();
                if (cloudSuccess) return true;
                return await localAction();
            }
            else
            {
                bool localSuccess = await localAction();
                if (localSuccess) return true;
                return await cloudAction();
            }
        }

        public async Task<LampStatus> GetStatusAsync(DeviceInfo? target = null)
        {
            string mode = _config.control_mode ?? "auto";
            string priority = _config.auto_priority ?? "local_first";

            if (mode == "cloud")
            {
                return await GetCloudStatusAsync(target);
            }
            if (mode == "local")
            {
                return await GetLocalStatusAsync(target);
            }

            if (priority == "cloud_first")
            {
                var st = await GetCloudStatusAsync(target);
                if (st.IsOnline) return st;
                return await GetLocalStatusAsync(target);
            }
            else
            {
                var st = await GetLocalStatusAsync(target);
                if (st.IsOnline) return st;
                return await GetCloudStatusAsync(target);
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

        public async Task<bool> SetPowerAsync(bool on, DeviceInfo? target = null)
        {
            return await DispatchCommandAsync(
                () => SetPowerLocalAsync(on, target),
                () => SendCloudCommandAsync("set_power", new { power = on }, target)
            );
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

        public async Task<bool> TogglePowerAsync(DeviceInfo? target = null)
        {
            return await DispatchCommandAsync(
                () => TogglePowerLocalAsync(target),
                () => SendCloudCommandAsync("toggle", null, target)
            );
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

        public async Task<bool> SetBrightnessAsync(int percent, DeviceInfo? target = null)
        {
            percent = Math.Clamp(percent, 1, 100);
            return await DispatchCommandAsync(
                () => SetBrightnessLocalAsync(percent, target),
                () => SendCloudCommandAsync("set_brightness", new { brightness = percent }, target)
            );
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

        public async Task<bool> SetColorTempAsync(int kelvin, DeviceInfo? target = null)
        {
            int pct = kelvin >= 2700 ? (kelvin - 2700) / 38 : kelvin;
            pct = Math.Clamp(pct, 0, 100);
            int valV2 = pct * 10;

            return await DispatchCommandAsync(
                () => SetColorTempLocalAsync(valV2, target),
                () => SendCloudCommandAsync("set_temp", new { temp = valV2 }, target)
            );
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

        public async Task<bool> SetColorHexAsync(string hexCode, DeviceInfo? target = null)
        {
            hexCode = hexCode.TrimStart('#');
            if (hexCode.Length != 6) return false;

            int r = Convert.ToInt32(hexCode.Substring(0, 2), 16);
            int g = Convert.ToInt32(hexCode.Substring(2, 2), 16);
            int b = Convert.ToInt32(hexCode.Substring(4, 2), 16);
            string tuyaHex = RgbToTuyaV2Hex(r, g, b);

            return await DispatchCommandAsync(
                () => SetColorHexLocalAsync(tuyaHex, target),
                () => SendCloudCommandAsync("set_color", new { hex = tuyaHex }, target)
            );
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

        public async Task<bool> SetPresetAsync(string mode, int brightness, int colorTempK, string hexCode, DeviceInfo? target = null)
        {
            brightness = Math.Clamp(brightness, 1, 100);
            int pct = colorTempK >= 2700 ? (colorTempK - 2700) / 38 : colorTempK;
            pct = Math.Clamp(pct, 0, 100);
            int tempV2 = pct * 10;

            string cleanHex = hexCode.TrimStart('#');
            string tuyaHex = "000003e803e8";
            if (cleanHex.Length == 6)
            {
                int r = Convert.ToInt32(cleanHex.Substring(0, 2), 16);
                int g = Convert.ToInt32(cleanHex.Substring(2, 2), 16);
                int b = Convert.ToInt32(cleanHex.Substring(4, 2), 16);
                tuyaHex = RgbToTuyaV2Hex(r, g, b);
            }

            return await DispatchCommandAsync(
                () => SetPresetLocalAsync(mode, brightness, tempV2, tuyaHex, target),
                () => SendCloudCommandAsync("set_preset", new { mode = mode, brightness = brightness, temp = tempV2, hex = tuyaHex }, target)
            );
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
    }
}
