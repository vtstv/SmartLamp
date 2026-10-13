// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using SmartLampApp.Models;
using SmartLampApp.Services;
using Xunit;

namespace SmartLamp.Tests
{
    public class BackupServiceTests
    {
        [Fact]
        public void CreateBackup_ProducesCorrectStructure()
        {
            var config = new LampConfig
            {
                language = "ru",
                control_mode = "local",
                auto_priority = "local_first",
                region = "eu",
                access_id = "test_acc_id",
                access_key = "test_acc_key",
                devices = new List<DeviceInfo>
                {
                    new DeviceInfo
                    {
                        Name = "Office Lamp",
                        Ip = "192.168.1.100",
                        DevId = "dev_12345",
                        LocalKey = "localkey_abcdef",
                        Version = "3.5",
                        GroupName = "Office"
                    }
                },
                groups = new List<DeviceGroup>
                {
                    new DeviceGroup
                    {
                        Name = "Office",
                        DeviceIds = new List<string> { "dev_12345" }
                    }
                },
                custom_presets = new List<UserPreset>
                {
                    new UserPreset
                    {
                        Name = "Night Reading",
                        Brightness = 30,
                        ColorTempK = 2700
                    }
                }
            };

            var backup = BackupService.CreateBackup(config);

            Assert.Equal(1, backup.BackupVersion);
            Assert.Equal("ru", backup.Preferences.language);
            Assert.Equal("local", backup.Preferences.control_mode);
            Assert.Single(backup.Devices);
            Assert.Equal("Office Lamp", backup.Devices[0].Name);
            Assert.Equal("dev_12345", backup.Devices[0].DevId);
            Assert.Equal("localkey_abcdef", backup.Devices[0].LocalKey);
            Assert.Single(backup.Groups);
            Assert.Single(backup.CustomPresets);
        }

        [Fact]
        public void ExportAndParseBackup_RoundtripsSuccessfully()
        {
            var config = new LampConfig
            {
                language = "de",
                devices = new List<DeviceInfo>
                {
                    new DeviceInfo { Name = "Ceiling Lamp", DevId = "dev_01", Ip = "10.0.0.5", LocalKey = "key_01" },
                    new DeviceInfo { Name = "Table Lamp", DevId = "dev_02", Ip = "10.0.0.6", LocalKey = "key_02" }
                }
            };

            string json = BackupService.ExportToJson(config);
            var parsed = BackupService.ParseBackup(json);

            Assert.NotNull(parsed);
            Assert.Equal(2, parsed.Devices.Count);
            Assert.Equal("Ceiling Lamp", parsed.Devices[0].Name);
            Assert.Equal("Table Lamp", parsed.Devices[1].Name);
            Assert.Equal("de", parsed.Preferences.language);
        }

        [Fact]
        public void ParseBackup_InvalidJson_ReturnsNull()
        {
            var parsed = BackupService.ParseBackup("This is definitely not JSON {{{");
            Assert.Null(parsed);
        }

        [Fact]
        public void ParseBackup_RawConfigFile_FallbackParses()
        {
            string rawConfigJson = @"{
                ""access_id"": ""cloud_id_999"",
                ""access_key"": ""cloud_secret_888"",
                ""control_mode"": ""cloud"",
                ""region"": ""us"",
                ""devices"": [
                    { ""Name"": ""Living Room Bulb"", ""DevId"": ""bulb_01"", ""Ip"": ""192.168.0.50"", ""LocalKey"": ""key_999"" }
                ]
            }";

            var parsed = BackupService.ParseBackup(rawConfigJson);

            Assert.NotNull(parsed);
            Assert.Equal("cloud_id_999", parsed.Preferences.access_id);
            Assert.Equal("us", parsed.Preferences.region);
            Assert.Single(parsed.Devices);
            Assert.Equal("Living Room Bulb", parsed.Devices[0].Name);
        }

        [Fact]
        public void RestoreBackup_FullOverwrite_ReplacesExisting()
        {
            var current = new LampConfig
            {
                language = "en",
                devices = new List<DeviceInfo>
                {
                    new DeviceInfo { Name = "Old Device", DevId = "old_dev" }
                }
            };

            var backup = new SmartLampBackup
            {
                Preferences = new BackupPreferences { language = "ru" },
                Devices = new List<DeviceInfo>
                {
                    new DeviceInfo { Name = "New Device 1", DevId = "new_dev_1" },
                    new DeviceInfo { Name = "New Device 2", DevId = "new_dev_2" }
                }
            };

            var restored = BackupService.RestoreBackup(current, backup, mergeMode: false);

            Assert.Equal("ru", restored.language);
            Assert.Equal(2, restored.devices.Count);
            Assert.DoesNotContain(restored.devices, d => d.DevId == "old_dev");
        }

        [Fact]
        public void RestoreBackup_MergeMode_AppendsWithoutDuplicates()
        {
            var current = new LampConfig
            {
                language = "en",
                devices = new List<DeviceInfo>
                {
                    new DeviceInfo { Name = "Existing Device", DevId = "dev_1", Ip = "192.168.1.10" }
                }
            };

            var backup = new SmartLampBackup
            {
                Preferences = new BackupPreferences { language = "de" },
                Devices = new List<DeviceInfo>
                {
                    new DeviceInfo { Name = "Existing Device Duplicated", DevId = "dev_1", Ip = "192.168.1.10" },
                    new DeviceInfo { Name = "Brand New Device", DevId = "dev_2", Ip = "192.168.1.11" }
                }
            };

            var restored = BackupService.RestoreBackup(current, backup, mergeMode: true);

            // Language kept as current in merge mode
            Assert.Equal("en", restored.language);
            // Device count is 2 (dev_1 not duplicated, dev_2 added)
            Assert.Equal(2, restored.devices.Count);
            Assert.Contains(restored.devices, d => d.DevId == "dev_1");
            Assert.Contains(restored.devices, d => d.DevId == "dev_2");
        }
    }
}
