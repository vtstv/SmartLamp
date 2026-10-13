// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System.Text.Json;
using SmartLampApp.Models;
using Xunit;

namespace SmartLamp.Tests
{
    public class ConfigManagerTests
    {
        [Fact]
        public void LampConfig_DefaultValues_AreConsistent()
        {
            var config = new LampConfig();

            Assert.NotNull(config.devices);
            Assert.NotNull(config.groups);
            Assert.NotNull(config.custom_presets);
            Assert.Equal("auto", config.control_mode);
            Assert.Equal("cloud_first", config.auto_priority);
            Assert.Equal("en", config.language);
            Assert.False(config.compact_mode);
        }

        [Fact]
        public void DeviceInfo_SerializationAndDeserialization_PreservesAllFields()
        {
            var dev = new DeviceInfo
            {
                Name = "Study Lamp",
                Ip = "192.168.1.120",
                DevId = "bf0123456789abcdef",
                LocalKey = "abcdef0123456789",
                Version = "3.5",
                GroupName = "Office",
                last_power_on = true,
                last_brightness = 85,
                last_temp = 5000,
                last_color_hex = "#FFAA00"
            };

            string json = JsonSerializer.Serialize(dev);
            var restored = JsonSerializer.Deserialize<DeviceInfo>(json);

            Assert.NotNull(restored);
            Assert.Equal(dev.Name, restored.Name);
            Assert.Equal(dev.Ip, restored.Ip);
            Assert.Equal(dev.DevId, restored.DevId);
            Assert.Equal(dev.LocalKey, restored.LocalKey);
            Assert.Equal(dev.Version, restored.Version);
            Assert.Equal(dev.GroupName, restored.GroupName);
            Assert.Equal(dev.last_power_on, restored.last_power_on);
            Assert.Equal(dev.last_brightness, restored.last_brightness);
            Assert.Equal(dev.last_temp, restored.last_temp);
            Assert.Equal(dev.last_color_hex, restored.last_color_hex);
        }

        [Fact]
        public void DeviceGroup_AddDeviceAndSyncMode_WorksCorrectly()
        {
            var group = new DeviceGroup
            {
                Name = "Living Room",
                Icon = "🛋️",
                SyncMode = GroupSyncMode.Proportional,
                MasterBrightness = 90
            };

            group.DeviceIds.Add("dev_1");
            group.DeviceIds.Add("dev_2");

            Assert.Equal(2, group.DeviceIds.Count);
            Assert.Contains("dev_1", group.DeviceIds);
            Assert.Contains("dev_2", group.DeviceIds);
            Assert.Equal(GroupSyncMode.Proportional, group.SyncMode);
        }

        [Fact]
        public void UserPreset_DefaultValues_AreValid()
        {
            var preset = new UserPreset
            {
                Name = "Night Reading",
                Brightness = 25,
                ColorTempK = 2700,
                ColorHex = "#FF8000",
                Mode = "white"
            };

            Assert.False(string.IsNullOrWhiteSpace(preset.Id));
            Assert.Equal("Night Reading", preset.Name);
            Assert.Equal(25, preset.Brightness);
            Assert.Equal(2700, preset.ColorTempK);
        }
    }
}
