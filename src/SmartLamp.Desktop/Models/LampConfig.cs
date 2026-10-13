// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SmartLamp.Desktop.Models
{
    public class DeviceInfo
    {
        public string Name { get; set; } = "Smart Lamp";
        public string Ip { get; set; } = "";
        public string DevId { get; set; } = "";
        public string LocalKey { get; set; } = "";
        public string Version { get; set; } = "3.5";
        public string GroupName { get; set; } = "Default Group";

        public bool last_power_on { get; set; } = false;
        public int last_brightness { get; set; } = 50;
        public int last_temp { get; set; } = 4000;
        public string last_color_hex { get; set; } = "#00E5FF";
    }

    public class UserPreset
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Custom Preset";
        public string Mode { get; set; } = "white"; // "white" or "colour"
        public int Brightness { get; set; } = 50;
        public int ColorTempK { get; set; } = 4000;
        public string ColorHex { get; set; } = "#00E5FF";
    }

    public class LampConfig
    {
        public string selected_dev_id { get; set; } = "";
        public string access_id { get; set; } = "";
        public string access_key { get; set; } = "";
        public string control_mode { get; set; } = "auto";
        public string auto_priority { get; set; } = "cloud_first";
        public string language { get; set; } = "en"; // "en", "de", "ru"
        public bool compact_mode { get; set; } = false;

        // App Behavior & Integrations
        public bool close_to_tray { get; set; } = true;
        public bool enable_timer_notifications { get; set; } = true;
        public bool enable_google_home { get; set; } = false;
        public bool enable_mqtt { get; set; } = false;
        public bool check_updates_on_startup { get; set; } = true;

        public int last_brightness { get; set; } = 50;
        public int last_temp { get; set; } = 4000;
        public string last_color_hex { get; set; } = "#00E5FF";

        public List<DeviceInfo> devices { get; set; } = new List<DeviceInfo>();
        public List<UserPreset> custom_presets { get; set; } = new List<UserPreset>();

        // Legacy compatibility
        public string ip { get; set; } = "";
        public string dev_id { get; set; } = "";
        public string local_key { get; set; } = "";
        public string version { get; set; } = "3.5";
    }

    public static class ConfigManager
    {
        public static string GetConfigPath()
        {
            if (OperatingSystem.IsWindows())
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string local = Path.Combine(baseDir, "smartlamp_config.json");
                if (File.Exists(local)) return local;

                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string appDir = Path.Combine(appData, "SmartLampApp");
                Directory.CreateDirectory(appDir);
                return Path.Combine(appDir, "smartlamp_config.json");
            }
            else
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string confDir = Path.Combine(home, ".config", "smartlamp");
                Directory.CreateDirectory(confDir);

                string confFile = Path.Combine(confDir, "config.json");
                string altFile = Path.Combine(confDir, "smartlamp_config.json");
                if (!File.Exists(confFile) && File.Exists(altFile)) return altFile;
                return confFile;
            }
        }

        public static LampConfig Load()
        {
            try
            {
                string path = GetConfigPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var cfg = JsonSerializer.Deserialize<LampConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (cfg != null)
                    {
                        // Legacy single device migration if devices list is empty
                        if (cfg.devices.Count == 0 && !string.IsNullOrWhiteSpace(cfg.ip))
                        {
                            cfg.devices.Add(new DeviceInfo
                            {
                                Name = "Smart Lamp 1",
                                Ip = cfg.ip,
                                DevId = cfg.dev_id,
                                LocalKey = cfg.local_key,
                                Version = cfg.version
                            });
                            cfg.selected_dev_id = cfg.dev_id;
                        }

                        // Ensure selected_dev_id
                        if (string.IsNullOrWhiteSpace(cfg.selected_dev_id) && cfg.devices.Count > 0)
                        {
                            cfg.selected_dev_id = cfg.devices[0].DevId;
                        }

                        return cfg;
                    }
                }
            }
            catch { }
            return new LampConfig();
        }

        public static void Save(LampConfig config)
        {
            try
            {
                string path = GetConfigPath();
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(path, json);
            }
            catch { }
        }
    }
}
