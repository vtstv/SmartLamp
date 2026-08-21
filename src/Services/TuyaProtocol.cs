// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Net.Http;
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

        private async Task<bool> DispatchCommandAsync(Func<Task<bool>> localAction, Func<Task<bool>> cloudAction)
        {
            string mode = _config.control_mode ?? "auto";
            string priority = _config.auto_priority ?? "cloud_first";

            if (mode == "local") return await localAction();
            if (mode == "cloud") return await cloudAction();

            bool hasCloud = !string.IsNullOrWhiteSpace(_config.access_id) && !string.IsNullOrWhiteSpace(_config.access_key);

            if ((priority == "cloud_first" || string.IsNullOrEmpty(_config.auto_priority)) && hasCloud)
            {
                bool cloudSuccess = await cloudAction();
                if (cloudSuccess) return true;
                return await localAction();
            }

            // Priority is local_first:
            bool daemonRunning = await IsBridgeDaemonRunningAsync();
            if (daemonRunning)
            {
                bool localSuccess = await localAction();
                if (localSuccess) return true;
                if (hasCloud) return await cloudAction();
            }
            else if (hasCloud)
            {
                bool cloudSuccess = await cloudAction();
                if (cloudSuccess) return true;
                return await localAction();
            }
            else
            {
                return await localAction();
            }

            return false;
        }

        public async Task<LampStatus> GetStatusAsync(DeviceInfo? target = null)
        {
            string mode = _config.control_mode ?? "auto";
            string priority = _config.auto_priority ?? "cloud_first";

            if (mode == "cloud") return await GetCloudStatusAsync(target);
            if (mode == "local") return await GetLocalStatusAsync(target);

            bool hasCloud = !string.IsNullOrWhiteSpace(_config.access_id) && !string.IsNullOrWhiteSpace(_config.access_key);

            if (priority == "cloud_first" && hasCloud)
            {
                var st = await GetCloudStatusAsync(target);
                if (st.IsOnline) return st;
                return await GetLocalStatusAsync(target);
            }
            else
            {
                bool daemonRunning = await IsBridgeDaemonRunningAsync();
                if (daemonRunning)
                {
                    var st = await GetLocalStatusAsync(target);
                    if (st.IsOnline) return st;
                    if (hasCloud) return await GetCloudStatusAsync(target);
                    return st;
                }
                else if (hasCloud)
                {
                    var st = await GetCloudStatusAsync(target);
                    if (st.IsOnline) return st;
                    return await GetLocalStatusAsync(target);
                }
                else
                {
                    return await GetLocalStatusAsync(target);
                }
            }
        }

        public async Task<bool> SetPowerAsync(bool on, DeviceInfo? target = null)
        {
            return await DispatchCommandAsync(
                () => SetPowerLocalAsync(on, target),
                () => SendCloudCommandAsync("set_power", new { power = on }, target)
            );
        }

        public async Task<bool> TogglePowerAsync(DeviceInfo? target = null)
        {
            return await DispatchCommandAsync(
                () => TogglePowerLocalAsync(target),
                () => SendCloudCommandAsync("toggle", null, target)
            );
        }

        public async Task<bool> SetBrightnessAsync(int percent, DeviceInfo? target = null)
        {
            percent = Math.Clamp(percent, 1, 100);
            return await DispatchCommandAsync(
                () => SetBrightnessLocalAsync(percent, target),
                () => SendCloudCommandAsync("set_brightness", new { brightness = percent }, target)
            );
        }

        public async Task<bool> SetColorTempAsync(int kelvin, DeviceInfo? target = null)
        {
            kelvin = Math.Clamp(kelvin, 2700, 6500);
            int valV2 = (int)Math.Round((kelvin - 2700.0) / 3800.0 * 1000.0);
            valV2 = Math.Clamp(valV2, 0, 1000);

            return await DispatchCommandAsync(
                () => SetColorTempLocalAsync(valV2, target),
                () => SendCloudCommandAsync("set_temp", new { temp = valV2 }, target)
            );
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

        public async Task<bool> SetPresetAsync(string mode, int brightness, int colorTempK, string hexCode, DeviceInfo? target = null)
        {
            brightness = Math.Clamp(brightness, 1, 100);
            colorTempK = Math.Clamp(colorTempK, 2700, 6500);
            int tempV2 = (int)Math.Round((colorTempK - 2700.0) / 3800.0 * 1000.0);
            tempV2 = Math.Clamp(tempV2, 0, 1000);

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
    }
}
