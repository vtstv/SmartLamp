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
using SmartLampApp.Services;

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
                CmbDeviceSelector.Items.Add(new ComboBoxItem { Content = LocalizationService.T("AddDeviceInSettings"), Tag = "none" });
                CmbDeviceSelector.SelectedIndex = 0;
                TxtDeviceInfo.Text = LocalizationService.T("NoDevicesConfigured");
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
                    string unit = LocalizationService.T(devCount == 1 ? "LampUnit" : "LampsUnit");
                    CmbDeviceSelector.Items.Add(new ComboBoxItem
                    {
                        Content = $"👥 {grp.Icon} {grp.Name} ({devCount} {unit})",
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
                SyncDeviceIpUi(active);
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
                    LblActiveDevice.Text = LocalizationService.T("ActiveRoomGroup");
                    TxtDeviceInfo.Text = $"{grp.Icon} {grp.Name}  |  " + LocalizationService.T("SynchronizedLamps", devCount, grp.SyncMode);

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
                    TxtStatus.Text = LocalizationService.T("GroupActive");
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

                LblActiveDevice.Text = LocalizationService.T("ActiveDevice");
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
            bool isLocal = RbIpLocal?.IsChecked == true || TuyaSharingCrypto.IsPrivateIp(ip);

            if (existing != null)
            {
                existing.Name = string.IsNullOrWhiteSpace(name) ? "Smart Lamp" : name;
                existing.Ip = ip;
                if (isLocal) existing.LocalIp = ip;
                else existing.ExternalIp = ip;
                existing.UseLocalIp = isLocal;
                existing.LocalKey = key;
                existing.GroupName = string.IsNullOrWhiteSpace(group) ? "Default Group" : group;
            }
            else
            {
                var newDev = new DeviceInfo
                {
                    Name = string.IsNullOrWhiteSpace(name) ? "Smart Lamp" : name,
                    Ip = ip,
                    DevId = devId,
                    LocalKey = key,
                    GroupName = string.IsNullOrWhiteSpace(group) ? "Default Group" : group,
                    UseLocalIp = isLocal
                };
                if (isLocal) newDev.LocalIp = ip;
                else newDev.ExternalIp = ip;
                _config.devices.Add(newDev);
            }

            _config.selected_dev_id = devId;
            _config.cloud_subscription_expired = false;
            ConfigManager.Save(_config);
            _protocol.UpdateConfig(_config);

            PopulateDeviceSelector();
            PopulateMasterDevicesList();
            BtnSaveDevice.Content = LocalizationService.T("SaveDevice");
            _ = RefreshStatusAsync();
            System.Windows.MessageBox.Show(LocalizationService.T("DeviceSavedMsg"), "SmartLamp", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnDeleteDevice_Click(object sender, RoutedEventArgs e)
        {
            var active = _protocol.GetActiveDevice();
            if (active == null || (string.IsNullOrWhiteSpace(active.DevId) && string.IsNullOrWhiteSpace(active.Ip)))
            {
                return;
            }

            var result = System.Windows.MessageBox.Show(LocalizationService.T("ConfirmDeleteDevice", active.Name, active.Ip), "SmartLamp", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _config.devices.RemoveAll(d => d.DevId == active.DevId || (!string.IsNullOrEmpty(active.Ip) && d.Ip == active.Ip));
                if (_config.groups != null)
                {
                    foreach (var grp in _config.groups)
                    {
                        if (!string.IsNullOrEmpty(active.DevId)) grp.DeviceIds.Remove(active.DevId);
                    }
                }

                if (_config.devices.Count == 0)
                {
                    _config.ip = "";
                    _config.dev_id = "";
                    _config.local_key = "";
                    _config.selected_dev_id = "";
                }
                else
                {
                    _config.selected_dev_id = _config.devices[0].DevId;
                    if (_config.dev_id == active.DevId)
                    {
                        _config.ip = _config.devices[0].Ip;
                        _config.dev_id = _config.devices[0].DevId;
                        _config.local_key = _config.devices[0].LocalKey;
                    }
                }

                ConfigManager.Save(_config);
                TuyaProtocol.ResetLocalClients();
                _protocol.UpdateConfig(_config);

                PopulateDeviceSelector();
                PopulateMasterDevicesList();
                _ = RefreshStatusAsync();
                System.Windows.MessageBox.Show(LocalizationService.T("DeviceDeletedMsg"), "SmartLamp", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public void PopulateMasterDevicesList()
        {
            if (LstDevicesMaster == null) return;
            _isUpdatingUi = true;
            LstDevicesMaster.Items.Clear();

            if (_config.devices.Count == 0)
            {
                LstDevicesMaster.Items.Add(LocalizationService.T("NoDevicesConfigured"));
                _isUpdatingUi = false;
                return;
            }

            int selIndex = -1;
            for (int i = 0; i < _config.devices.Count; i++)
            {
                var dev = _config.devices[i];
                string room = string.IsNullOrWhiteSpace(dev.GroupName) ? "Default" : dev.GroupName;
                LstDevicesMaster.Items.Add($"💡 {dev.Name}  •  IP: {dev.Ip}  •  Room: {room}");

                if (dev.DevId == _config.selected_dev_id)
                {
                    selIndex = i;
                }
            }

            if (selIndex >= 0)
            {
                LstDevicesMaster.SelectedIndex = selIndex;
            }
            _isUpdatingUi = false;
        }

        private void LstDevicesMaster_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || LstDevicesMaster.SelectedIndex < 0) return;
            int idx = LstDevicesMaster.SelectedIndex;
            if (idx >= 0 && idx < _config.devices.Count)
            {
                var dev = _config.devices[idx];
                _config.selected_dev_id = dev.DevId;
                _config.is_group_control_active = false;
                ConfigManager.Save(_config);
                _protocol.UpdateConfig(_config);

                PopulateDeviceForm();
                PopulateDeviceSelector();
                _ = RefreshStatusAsync();
            }
        }

        private async void BtnTestDevice_Click(object sender, RoutedEventArgs e)
        {
            BtnTestDevice.IsEnabled = false;
            BtnTestDevice.Content = "⏳ Testing...";
            try
            {
                var active = _protocol.GetActiveDevice();
                bool success = await _protocol.TogglePowerAsync(active);
                if (success)
                {
                    BtnTestDevice.Content = "🟢 Success";
                    await System.Threading.Tasks.Task.Delay(1000);
                }
                else
                {
                    BtnTestDevice.Content = "🔴 Failed";
                    await System.Threading.Tasks.Task.Delay(1200);
                }
            }
            catch
            {
                BtnTestDevice.Content = "🔴 Error";
                await System.Threading.Tasks.Task.Delay(1200);
            }
            finally
            {
                BtnTestDevice.Content = LocalizationService.T("TestDeviceBtn");
                BtnTestDevice.IsEnabled = true;
            }
        }
    }
}
