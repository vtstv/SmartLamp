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
        public void SetAddingNewDeviceMode(bool isAdding)
        {
            _isAddingNewDevice = isAdding;
            if (isAdding)
            {
                if (LstDevicesMaster != null)
                {
                    _isUpdatingUi = true;
                    LstDevicesMaster.SelectedIndex = -1;
                    _isUpdatingUi = false;
                }
                if (BtnSaveDevice != null)
                {
                    BtnSaveDevice.Content = LocalizationService.T("AddDeviceBtn");
                    BtnSaveDevice.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                }
                if (BtnCancelNewDevice != null) BtnCancelNewDevice.Visibility = Visibility.Visible;
                if (BtnDeleteDevice != null) BtnDeleteDevice.Visibility = Visibility.Collapsed;
                if (BtnTestDevice != null) BtnTestDevice.IsEnabled = false;
            }
            else
            {
                if (BtnSaveDevice != null)
                {
                    BtnSaveDevice.Content = LocalizationService.T("SaveDevice");
                    BtnSaveDevice.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0066CC"));
                }
                if (BtnCancelNewDevice != null) BtnCancelNewDevice.Visibility = Visibility.Collapsed;
                if (BtnDeleteDevice != null) BtnDeleteDevice.Visibility = Visibility.Visible;
                if (BtnTestDevice != null) BtnTestDevice.IsEnabled = true;
            }
        }

        private void BtnCancelNewDevice_Click(object sender, RoutedEventArgs e)
        {
            SetAddingNewDeviceMode(false);

            if (_config.devices != null && _config.devices.Count > 0)
            {
                int selIndex = -1;
                for (int i = 0; i < _config.devices.Count; i++)
                {
                    if (_config.devices[i].DevId == _config.selected_dev_id)
                    {
                        selIndex = i;
                        break;
                    }
                }
                if (selIndex < 0) selIndex = 0;

                if (LstDevicesMaster != null)
                {
                    LstDevicesMaster.SelectedIndex = selIndex;
                }
                PopulateDeviceForm();
            }
            else
            {
                TxtName.Text = "";
                TxtIp.Text = "";
                TxtDevId.Text = "";
                PassLocalKey.Password = "";
                TxtLocalKeyVisible.Text = "";
                TxtGroup.Text = "";
            }
        }

        private void BtnNewDevice_Click(object sender, RoutedEventArgs e)
        {
            SetAddingNewDeviceMode(true);
            TxtName.Text = "Smart Lamp";
            TxtIp.Text = "";
            TxtDevId.Text = "";
            PassLocalKey.Password = "";
            TxtLocalKeyVisible.Text = "";
            TxtGroup.Text = "Default Group";
            if (RbIpLocal != null) RbIpLocal.IsChecked = true;
            if (LstDiscovered != null) LstDiscovered.SelectedIndex = -1;
            TxtName.Focus();
            TxtName.SelectAll();
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
                    var known = _config.devices.FirstOrDefault(d => d.Ip == dev.Ip || d.LocalIp == dev.Ip || (!string.IsNullOrEmpty(d.DevId) && d.DevId == dev.DevId))
                                ?? (_config.devices.Count == 1 ? _config.devices[0] : null);
                    if (known != null)
                    {
                        if (dev.DevId.StartsWith("lan_") || string.IsNullOrWhiteSpace(dev.DevId))
                            dev.DevId = known.DevId;
                        if (dev.ProductName == "Tuya Device" || dev.ProductName.StartsWith("Tuya Device ("))
                            dev.ProductName = known.Name;

                        string idDisplay = dev.DevId.StartsWith("lan_") ? "Port 6668" : $"ID: {dev.DevId}";
                        LstDiscovered.Items.Add($"✅ [Added] {dev.ProductName} - IP: {dev.Ip} ({idDisplay})");
                    }
                    else
                    {
                        string idDisplay = dev.DevId.StartsWith("lan_") ? "Port 6668" : $"ID: {dev.DevId}";
                        LstDiscovered.Items.Add($"➕ [New] {dev.ProductName} - IP: {dev.Ip} ({idDisplay})");
                    }
                }
            }
        }

        private void LstDiscovered_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int idx = LstDiscovered.SelectedIndex;
            if (idx >= 0 && idx < _discoveredList.Count)
            {
                var sel = _discoveredList[idx];
                var known = _config.devices.FirstOrDefault(d => d.Ip == sel.Ip || d.LocalIp == sel.Ip || (!string.IsNullOrEmpty(d.DevId) && d.DevId == sel.DevId))
                            ?? (_config.devices.Count == 1 ? _config.devices[0] : null);
                if (known != null)
                {
                    SetAddingNewDeviceMode(false);
                    TxtName.Text = known.Name;
                    TxtIp.Text = sel.Ip;
                    known.LocalIp = sel.Ip;
                    known.Ip = sel.Ip;
                    known.UseLocalIp = true;
                    if (RbIpLocal != null) RbIpLocal.IsChecked = true;
                    TxtDevId.Text = known.DevId;
                    PassLocalKey.Password = known.LocalKey;
                    TxtLocalKeyVisible.Text = known.LocalKey;
                    TxtGroup.Text = known.GroupName;
                    BtnSaveDevice.Content = LocalizationService.T("SaveDevice");
                }
                else
                {
                    // New discovered device: auto-fill IP and Product Name, clear previous device credentials!
                    SetAddingNewDeviceMode(true);
                    TxtName.Text = sel.ProductName;
                    TxtIp.Text = sel.Ip;
                    if (RbIpLocal != null) RbIpLocal.IsChecked = true;
                    TxtDevId.Text = sel.DevId.StartsWith("lan_") ? "" : sel.DevId;
                    PassLocalKey.Password = "";
                    TxtLocalKeyVisible.Text = "";
                    TxtGroup.Text = "Default Group";
                    if (string.IsNullOrEmpty(TxtDevId.Text))
                    {
                        TxtDevId.Focus();
                    }
                    else
                    {
                        PassLocalKey.Focus();
                    }
                }
            }
        }

        private async void BtnCloudFetch_Click(object sender, RoutedEventArgs e)
        {
            string accId = TxtAccId.Text.Trim();
            string accKey = GetEnteredAccessKey();
            string devId = TxtDevId.Text.Trim();

            if (string.IsNullOrWhiteSpace(accId) || string.IsNullOrWhiteSpace(accKey))
            {
                System.Windows.MessageBox.Show("Please enter your Tuya Access ID and Access Key (Secret).", "Missing Credentials", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (accKey.Length < 32)
            {
                var confirm = System.Windows.MessageBox.Show(
                    $"Warning: Access Key (Secret) is only {accKey.Length} characters long (standard Tuya secret is 32 characters).\n\nPlease check if characters were cut off when copying.\n\nDo you want to continue anyway?",
                    "Potentially Truncated Secret",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes) return;
            }

            if (string.IsNullOrWhiteSpace(devId))
            {
                var ask = System.Windows.MessageBox.Show(
                    "Device ID field is empty.\n\nTo fetch a specific key, enter its Device ID (from iot.tuya.com, e.g. bf0123456789abcdef).\n\nWould you like to run 'Sync All Devices' instead to find all devices from your Tuya cloud project?",
                    "Device ID Missing",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (ask == MessageBoxResult.Yes)
                {
                    BtnCloudSyncAll_Click(sender, e);
                }
                return;
            }

            BtnCloudFetch.Content = "Fetching key...";
            BtnCloudFetch.IsEnabled = false;

            var (key, err) = await _protocol.AutoFetchKeyFromCloudAsync(accId, accKey, _config.region, devId);

            BtnCloudFetch.Content = "⚡ Fetch Key";
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

        private async void BtnCloudSyncAll_Click(object sender, RoutedEventArgs e)
        {
            string accId = TxtAccId.Text.Trim();
            string accKey = GetEnteredAccessKey();

            if (string.IsNullOrWhiteSpace(accId) || string.IsNullOrWhiteSpace(accKey))
            {
                System.Windows.MessageBox.Show("Please enter your Tuya Access ID and Access Key (Secret).", "Missing Credentials", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (accKey.Length < 32)
            {
                var confirm = System.Windows.MessageBox.Show(
                    $"Warning: Access Key (Secret) is only {accKey.Length} characters long (standard Tuya secret is 32 characters).\n\nPlease check if characters were cut off when copying.\n\nDo you want to continue anyway?",
                    "Potentially Truncated Secret",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes) return;
            }

            BtnCloudSyncAll.Content = "Syncing devices...";
            BtnCloudSyncAll.IsEnabled = false;

            var (cloudDevices, err) = await _protocol.FetchAllCloudDevicesAsync(accId, accKey, _config.region);

            BtnCloudSyncAll.Content = "📥 Sync All Devices";
            BtnCloudSyncAll.IsEnabled = true;

            if (!string.IsNullOrEmpty(err) && cloudDevices.Count == 0)
            {
                System.Windows.MessageBox.Show($"Failed to sync devices from Tuya Cloud: {err}", "Cloud Sync Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int addedCount = 0;
            int updatedCount = 0;

            foreach (var cDev in cloudDevices)
            {
                // Match with discovered local IP if available
                var discovered = _discoveredList?.FirstOrDefault(d => d.DevId == cDev.DevId || (!string.IsNullOrEmpty(cDev.Ip) && d.Ip == cDev.Ip));
                if (discovered != null && !string.IsNullOrEmpty(discovered.Ip))
                {
                    cDev.Ip = discovered.Ip;
                }

                var existing = _config.devices.FirstOrDefault(d => d.DevId == cDev.DevId);
                if (existing != null)
                {
                    if (string.IsNullOrEmpty(existing.LocalKey) && !string.IsNullOrEmpty(cDev.LocalKey))
                    {
                        existing.LocalKey = cDev.LocalKey;
                        updatedCount++;
                    }
                    if (string.IsNullOrEmpty(existing.Ip) && !string.IsNullOrEmpty(cDev.Ip))
                    {
                        existing.Ip = cDev.Ip;
                    }
                }
                else
                {
                    _config.devices.Add(cDev);
                    addedCount++;
                }
            }

            _config.access_id = accId;
            _config.access_key = accKey;
            ConfigManager.Save(_config);
            _protocol.UpdateConfig(_config);

            PopulateDeviceSelector();
            BtnAutoDiscover_Click(sender, e);

            string msg = $"Tuya Cloud Sync completed!\n\nAdded: {addedCount} new device(s)\nUpdated: {updatedCount} existing device(s)\nTotal devices: {_config.devices.Count}";
            System.Windows.MessageBox.Show(msg, "Cloud Sync Successful", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
