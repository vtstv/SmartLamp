// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SmartLampApp.Models;
using SmartLampApp.Services;
using Button = System.Windows.Controls.Button;

namespace SmartLampApp
{
    public partial class MainWindow : Window
    {
        private async Task ApplyColorHexAsync(string hex)
        {
            _currentMode = "colour";
            TxtHexCode.Text = hex;
            UpdateBulbGlow(hex, _isPowerOn);

            if (_config.is_group_control_active)
            {
                var grp = _protocol.GetActiveGroup();
                if (grp != null)
                {
                    grp.MasterColorHex = hex;
                    grp.MasterMode = "colour";
                    ConfigManager.Save(_config);
                    var devices = _protocol.GetDevicesForGroup(grp);
                    await _protocol.SetGroupColorHexAsync(devices, hex);
                    return;
                }
            }

            var active = _protocol.GetActiveDevice();
            if (active != null) active.last_color_hex = hex;
            _config.last_color_hex = hex;
            ConfigManager.Save(_config);
            await _protocol.SetColorHexAsync(hex);
        }

        private async void BtnColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                await ApplyColorHexAsync(hex);
            }
        }

        private async void BtnColorPicker_Click(object sender, RoutedEventArgs e)
        {
            var pickerWin = new ColorPickerWindow(TxtHexCode.Text) { Owner = this };
            if (pickerWin.ShowDialog() == true)
            {
                await ApplyColorHexAsync(pickerWin.SelectedHex);
            }
        }

        private async void BtnApplyHex_Click(object sender, RoutedEventArgs e)
        {
            await ApplyColorHexAsync(TxtHexCode.Text.Trim());
        }

        private async void BtnScene_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string sceneKey)
            {
                if (_config.is_group_control_active)
                {
                    var grp = _protocol.GetActiveGroup();
                    if (grp != null)
                    {
                        var devices = _protocol.GetDevicesForGroup(grp);
                        var tasks = new List<Task>();
                        foreach (var d in devices) tasks.Add(_protocol.SetSceneAsync(sceneKey, d));
                        await Task.WhenAll(tasks);
                        return;
                    }
                }
                await _protocol.SetSceneAsync(sceneKey);
            }
        }
    }
}
