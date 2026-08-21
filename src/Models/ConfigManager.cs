// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
        public string region { get; set; } = "eu";

        // Control Mode & Priority ("auto", "local", "cloud"; "local_first", "cloud_first")
        public string control_mode { get; set; } = "auto";
        public string auto_priority { get; set; } = "cloud_first";

        // Custom Global Hotkey Configuration
        public bool hotkey_mod_ctrl { get; set; } = true;
        public bool hotkey_mod_alt { get; set; } = true;
        public bool hotkey_mod_shift { get; set; } = false;
        public string hotkey_key { get; set; } = "L";

        // Application Window Exit & Startup Behavior
        public bool autostart { get; set; } = false;
        public bool close_to_tray { get; set; } = true;
        public bool enable_timer_notifications { get; set; } = true;
        public bool check_updates_on_startup { get; set; } = true;
        public string ignored_update_version { get; set; } = "";

        // Integration Toggles
        public bool enable_google_home { get; set; } = false;
        public bool enable_mqtt { get; set; } = false;

        // Last saved state (Global Fallback)
        public int last_brightness { get; set; } = 50;
        public int last_temp { get; set; } = 4000;
        public string last_color_hex { get; set; } = "#00E5FF";

        public List<DeviceInfo> devices { get; set; } = new List<DeviceInfo>();
        public List<UserPreset> custom_presets { get; set; } = new List<UserPreset>();

        // Legacy compatibility properties
        public string ip { get; set; } = "";
        public string dev_id { get; set; } = "";
        public string local_key { get; set; } = "";
        public string version { get; set; } = "3.5";
    }

    public static class ConfigManager
    {
        private static readonly string ConfigFileName = "smartlamp_config.json";
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SmartLampStudioSecuredKey_DPAPI");

        public static string EncryptSecret(string plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText) || plainText.StartsWith("enc:")) return plainText;
            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] cipherBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
                return "enc:" + Convert.ToBase64String(cipherBytes);
            }
            catch
            {
                return plainText;
            }
        }

        public static string DecryptSecret(string cipherText)
        {
            if (string.IsNullOrWhiteSpace(cipherText) || !cipherText.StartsWith("enc:")) return cipherText;
            try
            {
                string rawBase64 = cipherText.Substring(4);
                byte[] cipherBytes = Convert.FromBase64String(rawBase64);
                byte[] plainBytes = ProtectedData.Unprotect(cipherBytes, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return cipherText;
            }
        }

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
                    if (cfg != null)
                    {
                        // Decrypt secrets upon loading
                        cfg.access_key = DecryptSecret(cfg.access_key);
                        cfg.local_key = DecryptSecret(cfg.local_key);

                        if (cfg.devices != null)
                        {
                            foreach (var dev in cfg.devices)
                            {
                                dev.LocalKey = DecryptSecret(dev.LocalKey);
                                if (string.IsNullOrWhiteSpace(dev.LocalKey) && !string.IsNullOrWhiteSpace(cfg.local_key))
                                {
                                    dev.LocalKey = cfg.local_key;
                                }
                                if ((string.IsNullOrWhiteSpace(dev.DevId) || dev.DevId.Contains(".") || dev.DevId == dev.Ip) && !string.IsNullOrWhiteSpace(cfg.dev_id) && !cfg.dev_id.Contains("."))
                                {
                                    dev.DevId = cfg.dev_id;
                                }
                            }
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

                // Create a clone for saving so in-memory values remain decrypted in UI
                string inMemoryJson = JsonSerializer.Serialize(config);
                var copy = JsonSerializer.Deserialize<LampConfig>(inMemoryJson);

                if (copy != null)
                {
                    copy.access_key = EncryptSecret(copy.access_key);
                    copy.local_key = EncryptSecret(copy.local_key);

                    if (copy.devices != null)
                    {
                        foreach (var dev in copy.devices)
                        {
                            if (string.IsNullOrWhiteSpace(dev.LocalKey) && !string.IsNullOrWhiteSpace(copy.local_key))
                            {
                                dev.LocalKey = copy.local_key;
                            }
                            if ((string.IsNullOrWhiteSpace(dev.DevId) || dev.DevId.Contains(".") || dev.DevId == dev.Ip) && !string.IsNullOrWhiteSpace(copy.dev_id) && !copy.dev_id.Contains("."))
                            {
                                dev.DevId = copy.dev_id;
                            }
                            dev.LocalKey = EncryptSecret(dev.LocalKey);
                        }
                    }

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string encryptedJson = JsonSerializer.Serialize(copy, options);
                    File.WriteAllText(path, encryptedJson);
                }
            }
            catch { }
        }
    }
}
