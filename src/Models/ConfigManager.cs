using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SmartLampApp.Models
{
    public class DeviceInfo
    {
        public string Name { get; set; } = "Smart Lamp";
        public string Ip { get; set; } = "";
        public string DevId { get; set; } = "";
        public string LocalKey { get; set; } = "";
        public string Version { get; set; } = "3.5";
        public string GroupName { get; set; } = "Default Group";

        // Last saved state
        public int last_brightness { get; set; } = 50;
        public int last_temp { get; set; } = 4000;
        public string last_color_hex { get; set; } = "#00E5FF";
    }

    public class LampConfig
    {
        public string selected_dev_id { get; set; } = "";
        public string access_id { get; set; } = "";
        public string access_key { get; set; } = "";
        public string region { get; set; } = "eu";

        // Custom Global Hotkey Configuration
        public bool hotkey_mod_ctrl { get; set; } = true;
        public bool hotkey_mod_alt { get; set; } = true;
        public bool hotkey_mod_shift { get; set; } = false;
        public string hotkey_key { get; set; } = "L";

        // Application Window Exit Behavior
        public bool close_to_tray { get; set; } = true;

        // Integration Toggles
        public bool enable_google_home { get; set; } = false;
        public bool enable_mqtt { get; set; } = false;

        // Last saved state (Global Fallback)
        public int last_brightness { get; set; } = 50;
        public int last_temp { get; set; } = 4000;
        public string last_color_hex { get; set; } = "#00E5FF";

        public List<DeviceInfo> devices { get; set; } = new List<DeviceInfo>();

        // Legacy compatibility properties
        public string ip { get; set; } = "";
        public string dev_id { get; set; } = "";
        public string local_key { get; set; } = "";
        public string version { get; set; } = "3.5";
    }

    public static class ConfigManager
    {
        private static readonly string ConfigFileName = "smartlamp_config.json";

        public static string GetConfigPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localPath = Path.Combine(baseDir, ConfigFileName);
            if (File.Exists(localPath)) return localPath;

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appDir = Path.Combine(appData, "SmartLampApp");
            Directory.CreateDirectory(appDir);
            return Path.Combine(appDir, ConfigFileName);
        }

        public static LampConfig Load()
        {
            try
            {
                string path = GetConfigPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var cfg = JsonSerializer.Deserialize<LampConfig>(json);
                    if (cfg != null) return cfg;
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
