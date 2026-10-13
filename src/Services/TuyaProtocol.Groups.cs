// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System.Collections.Generic;
using System.Threading.Tasks;
using SmartLampApp.Models;

namespace SmartLampApp.Services
{
    public partial class TuyaProtocol
    {
        public List<DeviceInfo> GetDevicesForGroup(DeviceGroup group)
        {
            var list = new List<DeviceInfo>();
            if (group == null || _config?.devices == null) return list;
            foreach (var devId in group.DeviceIds)
            {
                var d = _config.devices.Find(dev => dev.DevId == devId);
                if (d != null) list.Add(d);
            }
            return list;
        }

        public DeviceGroup? GetActiveGroup()
        {
            if (_config?.groups == null || _config.groups.Count == 0) return null;
            var grp = _config.groups.Find(g => g.Id == _config.selected_group_id);
            return grp ?? _config.groups[0];
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

        public async Task SetGroupBrightnessAsync(List<DeviceInfo> groupDevices, int brightness, GroupSyncMode syncMode = GroupSyncMode.Synchronized, int baseMasterBrightness = 50)
        {
            var tasks = new List<Task>();
            foreach (var dev in groupDevices)
            {
                int targetBrightness = brightness;
                if (syncMode == GroupSyncMode.Proportional && baseMasterBrightness > 0)
                {
                    double ratio = (double)brightness / baseMasterBrightness;
                    int current = dev.last_brightness > 0 ? dev.last_brightness : 50;
                    targetBrightness = Math.Clamp((int)Math.Round(current * ratio), 1, 100);
                }
                tasks.Add(SetBrightnessAsync(targetBrightness, dev));
            }
            await Task.WhenAll(tasks);
        }

        public async Task SetGroupColorTempAsync(List<DeviceInfo> groupDevices, int kelvin)
        {
            var tasks = new List<Task>();
            foreach (var dev in groupDevices)
            {
                tasks.Add(SetColorTempAsync(kelvin, dev));
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

        public async Task SetGroupPresetAsync(List<DeviceInfo> groupDevices, string mode, int brightness, int colorTempK, string hexCode)
        {
            var tasks = new List<Task>();
            foreach (var dev in groupDevices)
            {
                tasks.Add(SetPresetAsync(mode, brightness, colorTempK, hexCode, dev));
            }
            await Task.WhenAll(tasks);
        }
    }
}
