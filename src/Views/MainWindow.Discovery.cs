// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

using SmartLampApp.Models;

namespace SmartLampApp
{
    public partial class MainWindow : Window
    {
        private void BtnNewDevice_Click(object sender, RoutedEventArgs e)
        {
            TxtName.Text = "Smart Lamp";
            TxtIp.Text = "";
            TxtDevId.Text = "";
            PassLocalKey.Password = "";
            TxtLocalKeyVisible.Text = "";
            TxtGroup.Text = "Default Group";
            BtnSaveDevice.Content = "➕ Add Device";
            LstDiscovered.SelectedIndex = -1;
            TxtIp.Focus();
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
                    var known = _config.devices.FirstOrDefault(d => d.Ip == dev.Ip || (!string.IsNullOrEmpty(d.DevId) && d.DevId == dev.DevId));
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
                var known = _config.devices.FirstOrDefault(d => d.Ip == sel.Ip || (!string.IsNullOrEmpty(d.DevId) && d.DevId == sel.DevId));
                if (known != null)
                {
                    TxtName.Text = known.Name;
                    TxtIp.Text = known.Ip;
                    TxtDevId.Text = known.DevId;
                    PassLocalKey.Password = known.LocalKey;
                    TxtLocalKeyVisible.Text = known.LocalKey;
                    TxtGroup.Text = known.GroupName;
                    BtnSaveDevice.Content = "💾 Save Device";
                }
                else
                {
                    // New discovered device: auto-fill IP and Product Name, clear previous device credentials!
                    TxtName.Text = sel.ProductName;
                    TxtIp.Text = sel.Ip;
                    TxtDevId.Text = sel.DevId.StartsWith("lan_") ? "" : sel.DevId;
                    PassLocalKey.Password = "";
                    TxtLocalKeyVisible.Text = "";
                    TxtGroup.Text = "Default Group";
                    BtnSaveDevice.Content = "➕ Add This Device";
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
