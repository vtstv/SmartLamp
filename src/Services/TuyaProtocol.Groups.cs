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
