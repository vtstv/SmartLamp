// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartLampApp.Models;

namespace SmartLampApp.Services
{
    public class BackupPreferences
    {
        public string language { get; set; } = "en";
        public string control_mode { get; set; } = "auto";
        public string auto_priority { get; set; } = "cloud_first";
        public string region { get; set; } = "eu";
        public string access_id { get; set; } = "";
        public string access_key { get; set; } = "";
        public bool autostart { get; set; } = false;
        public bool close_to_tray { get; set; } = true;
        public bool enable_timer_notifications { get; set; } = true;
        public bool compact_mode { get; set; } = false;
        public bool enable_google_home { get; set; } = false;
        public bool enable_mqtt { get; set; } = false;
        public bool hotkey_mod_ctrl { get; set; } = true;
        public bool hotkey_mod_alt { get; set; } = true;
        public bool hotkey_mod_shift { get; set; } = false;
        public string hotkey_key { get; set; } = "L";
    }

    public class SmartLampBackup
    {
        [JsonPropertyName("$schema")]
        public string Schema { get; set; } = "https://smartlamp.app/schemas/backup-v1.json";

        [JsonPropertyName("backup_version")]
        public int BackupVersion { get; set; } = 1;

        [JsonPropertyName("app_version")]
        public string AppVersion { get; set; } = "3.5.0";

        [JsonPropertyName("exported_at")]
        public DateTime ExportedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("exported_by")]
        public string ExportedBy { get; set; } = "SmartLamp";

        [JsonPropertyName("devices")]
        public List<DeviceInfo> Devices { get; set; } = new();

        [JsonPropertyName("groups")]
        public List<DeviceGroup> Groups { get; set; } = new();

        [JsonPropertyName("custom_presets")]
        public List<UserPreset> CustomPresets { get; set; } = new();

        [JsonPropertyName("preferences")]
        public BackupPreferences Preferences { get; set; } = new();
    }

    public static class BackupService
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static SmartLampBackup CreateBackup(LampConfig config)
        {
            var backup = new SmartLampBackup
            {
                BackupVersion = 1,
                AppVersion = AppVersion.Version,
                ExportedAt = DateTime.UtcNow,
                ExportedBy = "SmartLamp",
                Devices = new List<DeviceInfo>(),
                Groups = new List<DeviceGroup>(),
                CustomPresets = new List<UserPreset>(),
                Preferences = new BackupPreferences
                {
                    language = config.language ?? "en",
                    control_mode = config.control_mode ?? "auto",
                    auto_priority = config.auto_priority ?? "cloud_first",
                    region = config.region ?? "eu",
                    access_id = config.access_id ?? "",
                    access_key = ConfigManager.DecryptSecret(config.access_key ?? ""),
                    autostart = config.autostart,
                    close_to_tray = config.close_to_tray,
                    enable_timer_notifications = config.enable_timer_notifications,
                    compact_mode = config.compact_mode,
                    enable_google_home = config.enable_google_home,
                    enable_mqtt = config.enable_mqtt,
                    hotkey_mod_ctrl = config.hotkey_mod_ctrl,
                    hotkey_mod_alt = config.hotkey_mod_alt,
                    hotkey_mod_shift = config.hotkey_mod_shift,
                    hotkey_key = config.hotkey_key ?? "L"
                }
            };

            if (config.devices != null)
            {
                foreach (var d in config.devices)
                {
                    backup.Devices.Add(new DeviceInfo
                    {
                        Name = d.Name,
                        Ip = d.Ip,
                        DevId = d.DevId,
                        LocalKey = ConfigManager.DecryptSecret(d.LocalKey ?? ""),
                        Version = d.Version,
                        GroupName = d.GroupName,
                        last_power_on = d.last_power_on,
                        last_brightness = d.last_brightness,
                        last_temp = d.last_temp,
                        last_color_hex = d.last_color_hex
                    });
                }
            }

            if (config.groups != null)
            {
                foreach (var g in config.groups)
                {
                    backup.Groups.Add(new DeviceGroup
                    {
                        Id = g.Id,
                        Name = g.Name,
                        Icon = g.Icon,
                        SortOrder = g.SortOrder,
                        DeviceIds = new List<string>(g.DeviceIds ?? new()),
                        MasterPowerOn = g.MasterPowerOn,
                        MasterBrightness = g.MasterBrightness,
                        MasterTempK = g.MasterTempK,
                        MasterColorHex = g.MasterColorHex,
                        MasterMode = g.MasterMode,
                        SyncMode = g.SyncMode,
                        TurnOffAllOnPowerOff = g.TurnOffAllOnPowerOff,
                        TurnOnAllOnPowerOn = g.TurnOnAllOnPowerOn
                    });
                }
            }

            if (config.custom_presets != null)
            {
                foreach (var p in config.custom_presets)
                {
                    backup.CustomPresets.Add(new UserPreset
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Mode = p.Mode,
                        Brightness = p.Brightness,
                        ColorTempK = p.ColorTempK,
                        ColorHex = p.ColorHex
                    });
                }
            }

            return backup;
        }

        public static string ExportToJson(LampConfig config)
        {
            var backup = CreateBackup(config);
            return JsonSerializer.Serialize(backup, JsonOpts);
        }

        public static void ExportToFile(string filePath, LampConfig config)
        {
            string json = ExportToJson(config);
            File.WriteAllText(filePath, json);
        }

        public static SmartLampBackup? ParseBackup(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                // First attempt: Standard envelope backup format
                using (var doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("backup_version", out _) ||
                        doc.RootElement.TryGetProperty("$schema", out _))
                    {
                        var backup = JsonSerializer.Deserialize<SmartLampBackup>(json, JsonOpts);
                        if (backup != null)
                        {
                            return backup;
                        }
                    }

                    // Fallback attempt: Raw smartlamp_config.json direct import
                    var rawConfig = JsonSerializer.Deserialize<LampConfig>(json, JsonOpts);
                    if (rawConfig != null && ((rawConfig.devices != null && rawConfig.devices.Count > 0) || 
                                              !string.IsNullOrWhiteSpace(rawConfig.access_id) || 
                                              !string.IsNullOrWhiteSpace(rawConfig.ip)))
                    {
                        return CreateBackup(rawConfig);
                    }
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        public static LampConfig RestoreBackup(LampConfig currentConfig, SmartLampBackup backup, bool mergeMode = false)
        {
            if (backup == null) return currentConfig;

            if (!mergeMode)
            {
                // Full Overwrite Mode
                var target = new LampConfig();

                // Preferences
                if (backup.Preferences != null)
                {
                    target.language = backup.Preferences.language ?? "en";
                    target.control_mode = backup.Preferences.control_mode ?? "auto";
                    target.auto_priority = backup.Preferences.auto_priority ?? "cloud_first";
                    target.region = backup.Preferences.region ?? "eu";
                    target.access_id = backup.Preferences.access_id ?? "";
                    target.access_key = backup.Preferences.access_key ?? "";
                    target.autostart = backup.Preferences.autostart;
                    target.close_to_tray = backup.Preferences.close_to_tray;
                    target.enable_timer_notifications = backup.Preferences.enable_timer_notifications;
                    target.compact_mode = backup.Preferences.compact_mode;
                    target.enable_google_home = backup.Preferences.enable_google_home;
                    target.enable_mqtt = backup.Preferences.enable_mqtt;
                    target.hotkey_mod_ctrl = backup.Preferences.hotkey_mod_ctrl;
                    target.hotkey_mod_alt = backup.Preferences.hotkey_mod_alt;
                    target.hotkey_mod_shift = backup.Preferences.hotkey_mod_shift;
                    target.hotkey_key = backup.Preferences.hotkey_key ?? "L";
                }

                target.devices = new List<DeviceInfo>(backup.Devices ?? new());
                target.groups = new List<DeviceGroup>(backup.Groups ?? new());
                target.custom_presets = new List<UserPreset>(backup.CustomPresets ?? new());

                if (target.devices.Count > 0)
                {
                    target.selected_dev_id = target.devices[0].DevId;
                }
                if (target.groups.Count > 0)
                {
                    target.selected_group_id = target.groups[0].Id;
                }

                return target;
            }
            else
            {
                // Merge Mode: Keep existing settings, append non-existent devices, groups, and presets
                if (backup.Devices != null)
                {
                    currentConfig.devices ??= new List<DeviceInfo>();
                    foreach (var d in backup.Devices)
                    {
                        var existing = currentConfig.devices.Find(x => 
                            (!string.IsNullOrEmpty(x.DevId) && x.DevId == d.DevId) || 
                            (!string.IsNullOrEmpty(x.Ip) && x.Ip == d.Ip));
                        if (existing == null)
                        {
                            currentConfig.devices.Add(d);
                        }
                    }
                }

                if (backup.Groups != null)
                {
                    currentConfig.groups ??= new List<DeviceGroup>();
                    foreach (var g in backup.Groups)
                    {
                        var existing = currentConfig.groups.Find(x => x.Id == g.Id || x.Name.Equals(g.Name, StringComparison.OrdinalIgnoreCase));
                        if (existing == null)
                        {
                            currentConfig.groups.Add(g);
                        }
                    }
                }

                if (backup.CustomPresets != null)
                {
                    currentConfig.custom_presets ??= new List<UserPreset>();
                    foreach (var p in backup.CustomPresets)
                    {
                        var existing = currentConfig.custom_presets.Find(x => x.Id == p.Id || x.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase));
                        if (existing == null)
                        {
                            currentConfig.custom_presets.Add(p);
                        }
                    }
                }

                return currentConfig;
            }
        }
    }
}
