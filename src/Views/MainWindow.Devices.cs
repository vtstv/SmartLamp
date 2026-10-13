// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using SmartLampApp.Models;

using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace SmartLampApp
{
    public partial class MainWindow : Window
    {
        private void PopulateDeviceSelector()
        {
            _isUpdatingUi = true;
            CmbDeviceSelector.Items.Clear();

            if (_config.devices.Count == 0 && (_config.groups == null || _config.groups.Count == 0))
            {
                CmbDeviceSelector.Items.Add(new ComboBoxItem { Content = "➕ Add Device in Settings", Tag = "none" });
                CmbDeviceSelector.SelectedIndex = 0;
                TxtDeviceInfo.Text = "No devices configured yet. Switch to Settings tab to add one.";
                _isUpdatingUi = false;
                return;
            }

            foreach (var dev in _config.devices)
            {
                CmbDeviceSelector.Items.Add(new ComboBoxItem
                {
                    Content = $"💡 {dev.Name} ({dev.Ip})",
                    Tag = $"dev:{dev.DevId}"
                });
            }

            if (_config.groups != null && _config.groups.Count > 0)
            {
                foreach (var grp in _config.groups)
                {
                    int devCount = grp.DeviceIds?.Count ?? 0;
                    CmbDeviceSelector.Items.Add(new ComboBoxItem
                    {
                        Content = $"👥 {grp.Icon} {grp.Name} ({devCount} lamps)",
                        Tag = $"grp:{grp.Id}"
                    });
                }
            }

            int selectedIndex = -1;
            if (_config.is_group_control_active && !string.IsNullOrEmpty(_config.selected_group_id))
            {
                for (int i = 0; i < CmbDeviceSelector.Items.Count; i++)
                {
                    if (CmbDeviceSelector.Items[i] is ComboBoxItem cbi && (cbi.Tag as string) == $"grp:{_config.selected_group_id}")
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }

            if (selectedIndex < 0 && !string.IsNullOrEmpty(_config.selected_dev_id))
            {
                for (int i = 0; i < CmbDeviceSelector.Items.Count; i++)
                {
                    if (CmbDeviceSelector.Items[i] is ComboBoxItem cbi && (cbi.Tag as string) == $"dev:{_config.selected_dev_id}")
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }

            if (selectedIndex < 0 && CmbDeviceSelector.Items.Count > 0)
            {
                selectedIndex = 0;
            }

            CmbDeviceSelector.SelectedIndex = selectedIndex;
            ApplySelectionFromSelector();
            _isUpdatingUi = false;
        }

        private void PopulateDeviceForm()
        {
            var active = _protocol.GetActiveDevice();
            if (active != null)
            {
                TxtName.Text = active.Name;
                TxtIp.Text = active.Ip;
                TxtDevId.Text = active.DevId;

                PassLocalKey.Password = active.LocalKey;
                TxtLocalKeyVisible.Text = active.LocalKey;

                TxtGroup.Text = active.GroupName;
                if (!_config.is_group_control_active)
                {
                    TxtDeviceInfo.Text = $"IP: {active.Ip}  |  ID: {active.DevId}";

                    // Restore saved brightness, temperature, and color hex
                    int bVal = active.last_brightness > 0 ? active.last_brightness : (_config.last_brightness > 0 ? _config.last_brightness : 50);
                    SliderBright.Value = bVal;
                    TxtBrightVal.Text = $"{bVal}%";

                    int tVal = active.last_temp >= 2700 ? active.last_temp : (_config.last_temp >= 2700 ? _config.last_temp : 4000);
                    SliderTemp.Value = tVal;
                    TxtTempVal.Text = $"{tVal}K";

                    string hexVal = !string.IsNullOrWhiteSpace(active.last_color_hex) ? active.last_color_hex : (_config.last_color_hex ?? "#00E5FF");
                    TxtHexCode.Text = hexVal;
                }
            }

            TxtAccId.Text = _config.access_id;
            PassAccKey.Password = _config.access_key;
            TxtAccKeyVisible.Text = _config.access_key;

            string mode = string.IsNullOrWhiteSpace(_config.control_mode) ? "auto" : _config.control_mode;
            foreach (ComboBoxItem item in CmbControlMode.Items)
            {
                if ((string)item.Tag == mode)
                {
                    item.IsSelected = true;
                    break;
                }
            }

            string priority = string.IsNullOrWhiteSpace(_config.auto_priority) ? "cloud_first" : _config.auto_priority;
            foreach (ComboBoxItem item in CmbAutoPriority.Items)
            {
                if ((string)item.Tag == priority)
                {
                    item.IsSelected = true;
                    break;
                }
            }

            if (PanelAutoPriority != null)
            {
                PanelAutoPriority.Visibility = mode == "auto" ? Visibility.Visible : Visibility.Collapsed;
            }

            ChkCloseToTray.IsChecked = _config.close_to_tray;
            ChkEnableGoogle.IsChecked = _config.enable_google_home;
            ChkEnableMqtt.IsChecked = _config.enable_mqtt;

            NavGoogleHome.Visibility = _config.enable_google_home ? Visibility.Visible : Visibility.Collapsed;
            NavMqtt.Visibility = _config.enable_mqtt ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnToggleKeyVisibility_Click(object sender, RoutedEventArgs e)
        {
            _isKeyVisible = !_isKeyVisible;
            if (_isKeyVisible)
            {
                TxtLocalKeyVisible.Text = PassLocalKey.Password;
                PassLocalKey.Visibility = Visibility.Collapsed;
                TxtLocalKeyVisible.Visibility = Visibility.Visible;
                BtnToggleKeyVisibility.Content = "🙈";
            }
            else
            {
                PassLocalKey.Password = TxtLocalKeyVisible.Text;
                TxtLocalKeyVisible.Visibility = Visibility.Collapsed;
                PassLocalKey.Visibility = Visibility.Visible;
                BtnToggleKeyVisibility.Content = "👁️";
            }
        }

        private void BtnToggleAccKeyVisibility_Click(object sender, RoutedEventArgs e)
        {
            _isAccKeyVisible = !_isAccKeyVisible;
            if (_isAccKeyVisible)
            {
                TxtAccKeyVisible.Text = PassAccKey.Password;
                PassAccKey.Visibility = Visibility.Collapsed;
                TxtAccKeyVisible.Visibility = Visibility.Visible;
                BtnToggleAccKeyVisibility.Content = "🙈";
            }
            else
            {
                PassAccKey.Password = TxtAccKeyVisible.Text;
                TxtAccKeyVisible.Visibility = Visibility.Collapsed;
                PassAccKey.Visibility = Visibility.Visible;
                BtnToggleAccKeyVisibility.Content = "👁️";
            }
        }

        private string GetEnteredLocalKey()
        {
            return _isKeyVisible ? TxtLocalKeyVisible.Text.Trim() : PassLocalKey.Password.Trim();
        }

        private string GetEnteredAccessKey()
        {
            return _isAccKeyVisible ? TxtAccKeyVisible.Text.Trim() : PassAccKey.Password.Trim();
        }

        private void CmbDeviceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || CmbDeviceSelector.SelectedIndex < 0) return;
            ApplySelectionFromSelector();
        }

        private void ApplySelectionFromSelector()
        {
            if (CmbDeviceSelector.SelectedItem is not ComboBoxItem selectedItem || selectedItem.Tag is not string tag)
            {
                return;
            }

            if (tag.StartsWith("grp:"))
            {
                string grpId = tag.Substring(4);
                _config.is_group_control_active = true;
                _config.selected_group_id = grpId;
                ConfigManager.Save(_config);

                var grp = _config.groups?.Find(g => g.Id == grpId);
                if (grp != null)
                {
                    int devCount = grp.DeviceIds?.Count ?? 0;
                    LblActiveDevice.Text = "Active Room / Group:";
                    TxtDeviceInfo.Text = $"{grp.Icon} {grp.Name}  |  {devCount} synchronized lamps ({grp.SyncMode})";

                    _isPowerOn = grp.MasterPowerOn;
                    _currentMode = grp.MasterMode ?? "white";

                    int bVal = grp.MasterBrightness > 0 ? grp.MasterBrightness : 80;
                    SliderBright.Value = bVal;
                    TxtBrightVal.Text = $"{bVal}%";

                    int tVal = grp.MasterTempK >= 2700 ? grp.MasterTempK : 4000;
                    SliderTemp.Value = tVal;
                    TxtTempVal.Text = $"{tVal}K";

                    string hexVal = !string.IsNullOrWhiteSpace(grp.MasterColorHex) ? grp.MasterColorHex : "#00E5FF";
                    TxtHexCode.Text = hexVal;

                    PillStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                    TxtStatus.Text = "👥 GROUP ACTIVE";
                    if (PillCompactStatus != null) PillCompactStatus.Background = PillStatus.Background;
                    if (TxtCompactStatus != null) TxtCompactStatus.Text = TxtStatus.Text;
                    if (TxtCompactDevice != null) TxtCompactDevice.Text = $"{grp.Icon} {grp.Name}";

                    UpdatePowerButtonUi();
                }
            }
            else if (tag.StartsWith("dev:"))
            {
                string devId = tag.Substring(4);
                _config.is_group_control_active = false;
                _config.selected_dev_id = devId;
                ConfigManager.Save(_config);
                _protocol.UpdateConfig(_config);

                LblActiveDevice.Text = "Active Device:";
                PopulateDeviceForm();
                _ = RefreshStatusAsync();
            }
        }

        private void BtnSaveDevice_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtName.Text.Trim();
            string ip = TxtIp.Text.Trim();
            string devId = TxtDevId.Text.Trim();
            string key = GetEnteredLocalKey();
            string group = TxtGroup.Text.Trim();

            if (string.IsNullOrWhiteSpace(devId))
            {
                System.Windows.MessageBox.Show("Please enter a valid Device ID.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var existing = _config.devices.Find(d => d.DevId == devId);
            if (existing != null)
            {
                existing.Name = string.IsNullOrWhiteSpace(name) ? "Smart Lamp" : name;
                existing.Ip = ip;
                existing.LocalKey = key;
                existing.GroupName = string.IsNullOrWhiteSpace(group) ? "Default Group" : group;
            }
            else
            {
                _config.devices.Add(new DeviceInfo
                {
                    Name = string.IsNullOrWhiteSpace(name) ? "Smart Lamp" : name,
                    Ip = ip,
                    DevId = devId,
                    LocalKey = key,
                    GroupName = string.IsNullOrWhiteSpace(group) ? "Default Group" : group
                });
            }

            _config.selected_dev_id = devId;
            _config.cloud_subscription_expired = false;
            ConfigManager.Save(_config);
            _protocol.UpdateConfig(_config);

            PopulateDeviceSelector();
            _ = RefreshStatusAsync();
            System.Windows.MessageBox.Show("Device saved successfully!", "Device Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnDeleteDevice_Click(object sender, RoutedEventArgs e)
        {
            var active = _protocol.GetActiveDevice();
            if (active == null || string.IsNullOrWhiteSpace(active.DevId))
            {
                System.Windows.MessageBox.Show("No active device selected to delete.", "Delete Device", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = System.Windows.MessageBox.Show($"Are you sure you want to delete '{active.Name}' ({active.Ip})?", "Delete Device", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _config.devices.RemoveAll(d => d.DevId == active.DevId);
                _config.selected_dev_id = _config.devices.Count > 0 ? _config.devices[0].DevId : "";
                ConfigManager.Save(_config);
                _protocol.UpdateConfig(_config);

                PopulateDeviceSelector();
                _ = RefreshStatusAsync();
                System.Windows.MessageBox.Show("Device deleted successfully.", "Device Removed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void BtnAutoDiscover_Click(object sender, RoutedEventArgs e)
        {
            BtnAutoDiscover.Content = "Scanning Wi-Fi subnet...";
            BtnAutoDiscover.IsEnabled = false;
            LstDiscovered.Items.Clear();

            _discoveredList = await _protocol.AutoDiscoverDevicesAsync();

            BtnAutoDiscover.Content = "⚡ Scan Local Wi-Fi Subnet";
            BtnAutoDiscover.IsEnabled = true;

            if (_discoveredList.Count == 0)
            {
                LstDiscovered.Items.Add("No Tuya devices found on local Wi-Fi.");
            }
            else
            {
                foreach (var dev in _discoveredList)
                {
                    var known = _config.devices.FirstOrDefault(d => d.Ip == dev.Ip);
                    if (known != null)
                    {
                        if (dev.DevId.StartsWith("lan_") || string.IsNullOrWhiteSpace(dev.DevId))
                            dev.DevId = known.DevId;
                        if (dev.ProductName == "Tuya Device" || dev.ProductName.StartsWith("Tuya Device ("))
                            dev.ProductName = known.Name;
                    }

                    string idDisplay = dev.DevId.StartsWith("lan_") ? "Port 6668" : $"ID: {dev.DevId}";
                    LstDiscovered.Items.Add($"💡 {dev.ProductName} - IP: {dev.Ip} ({idDisplay})");
                }
            }
        }

        private void LstDiscovered_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int idx = LstDiscovered.SelectedIndex;
            if (idx >= 0 && idx < _discoveredList.Count)
            {
                var sel = _discoveredList[idx];
                var known = _config.devices.FirstOrDefault(d => d.Ip == sel.Ip);
                if (known != null)
                {
                    TxtName.Text = known.Name;
                    TxtIp.Text = known.Ip;
                    TxtDevId.Text = known.DevId;
                    PassLocalKey.Password = known.LocalKey;
                    TxtLocalKeyVisible.Text = known.LocalKey;
                    TxtGroup.Text = known.GroupName;
                }
                else
                {
                    TxtName.Text = sel.ProductName;
                    TxtIp.Text = sel.Ip;
                    if (!sel.DevId.StartsWith("lan_"))
                    {
                        TxtDevId.Text = sel.DevId;
                    }
                }
            }
        }

        private async void BtnCloudFetch_Click(object sender, RoutedEventArgs e)
        {
            string accId = TxtAccId.Text.Trim();
            string accKey = GetEnteredAccessKey();

            if (string.IsNullOrWhiteSpace(accId) || string.IsNullOrWhiteSpace(accKey))
            {
                System.Windows.MessageBox.Show("Please enter your Tuya Access ID and Access Key (Secret).", "Missing Credentials", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnCloudFetch.Content = "Fetching keys from Tuya Cloud...";
            BtnCloudFetch.IsEnabled = false;

            var (key, err) = await _protocol.AutoFetchKeyFromCloudAsync(accId, accKey, _config.region);

            BtnCloudFetch.Content = "⚡ Fetch Key from Cloud";
            BtnCloudFetch.IsEnabled = true;

            if (!string.IsNullOrEmpty(key))
            {
                PassLocalKey.Password = key;
                TxtLocalKeyVisible.Text = key;
                _config.access_id = accId;
                _config.access_key = accKey;

                ConfigManager.Save(_config);
                System.Windows.MessageBox.Show($"Retrieved Local Key from Tuya Cloud!\n\nLocal Key: {key}", "Cloud Sync Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show($"Cloud Sync Error: {err}", "Tuya Cloud Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
