// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SmartLampApp.Models;
using SmartLampApp.Services;

using MessageBox = System.Windows.MessageBox;

namespace SmartLampApp
{
    public partial class MainWindow : Window
    {
        private void SyncDeviceIpUi(DeviceInfo? active)
        {
            if (active == null || TxtIp == null) return;

            if (string.IsNullOrEmpty(active.LocalIp) && TuyaSharingCrypto.IsPrivateIp(active.Ip))
            {
                active.LocalIp = active.Ip;
            }
            else if (string.IsNullOrEmpty(active.ExternalIp) && !string.IsNullOrEmpty(active.Ip) && !TuyaSharingCrypto.IsPrivateIp(active.Ip))
            {
                active.ExternalIp = active.Ip;
            }

            _isUpdatingUi = true;
            try
            {
                if (active.UseLocalIp && !string.IsNullOrEmpty(active.LocalIp))
                {
                    TxtIp.Text = active.LocalIp;
                    if (RbIpLocal != null) RbIpLocal.IsChecked = true;
                }
                else if (!active.UseLocalIp && !string.IsNullOrEmpty(active.ExternalIp))
                {
                    TxtIp.Text = active.ExternalIp;
                    if (RbIpExternal != null) RbIpExternal.IsChecked = true;
                }
                else
                {
                    TxtIp.Text = active.Ip;
                    if (RbIpLocal != null) RbIpLocal.IsChecked = TuyaSharingCrypto.IsPrivateIp(active.Ip);
                    if (RbIpExternal != null) RbIpExternal.IsChecked = !TuyaSharingCrypto.IsPrivateIp(active.Ip);
                }
            }
            finally
            {
                _isUpdatingUi = false;
            }
        }

        private void RbIpType_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || _protocol == null || TxtIp == null) return;
            if (_isAddingNewDevice) return;
            var active = _protocol.GetActiveDevice();
            if (active == null) return;

            if (RbIpLocal?.IsChecked == true)
            {
                active.UseLocalIp = true;
                if (!string.IsNullOrEmpty(active.LocalIp))
                {
                    TxtIp.Text = active.LocalIp;
                    active.Ip = active.LocalIp;
                    ConfigManager.Save(_config);
                    _protocol.UpdateConfig(_config);
                    TuyaProtocol.ResetLocalClients();
                    _ = RefreshStatusAsync();
                }
                else
                {
                    string localKey = GetEnteredLocalKey();
                    _ = AutoDetectLocalIpAsync(active.DevId, localKey, active.Version, showFeedback: false);
                }
            }
            else if (RbIpExternal?.IsChecked == true)
            {
                active.UseLocalIp = false;
                if (!string.IsNullOrEmpty(active.ExternalIp))
                {
                    TxtIp.Text = active.ExternalIp;
                    active.Ip = active.ExternalIp;
                    ConfigManager.Save(_config);
                    _protocol.UpdateConfig(_config);
                    TuyaProtocol.ResetLocalClients();
                    _ = RefreshStatusAsync();
                }
            }
        }

        private async void BtnAutoDetectIp_Click(object sender, RoutedEventArgs e)
        {
            string devId = TxtDevId.Text.Trim();
            if (string.IsNullOrEmpty(devId))
            {
                MessageBox.Show(this, "Please enter a valid Device ID first.", "SmartLamp", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string localKey = GetEnteredLocalKey();
            var active = _protocol?.GetActiveDevice();
            string version = active?.Version ?? "3.5";

            BtnAutoDetectIp.IsEnabled = false;
            BtnAutoDetectIp.Content = "⏳ Scanning...";
            try
            {
                await AutoDetectLocalIpAsync(devId, localKey, version, showFeedback: true);
            }
            finally
            {
                BtnAutoDetectIp.IsEnabled = true;
                BtnAutoDetectIp.Content = "🔍 Auto-LAN";
            }
        }

        private async void BtnAutoResolveAll_Click(object sender, RoutedEventArgs e)
        {
            if (_protocol == null || _config?.devices == null || _config.devices.Count == 0)
            {
                MessageBox.Show(this, "No devices configured to resolve.", "SmartLamp", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            BtnAutoResolveAll.IsEnabled = false;
            BtnAutoResolveAll.Content = "⏳ Resolving...";
            try
            {
                var (resolved, total) = await _protocol.AutoResolveAllDeviceIpsAsync(3500);

                var active = _protocol.GetActiveDevice();
                SyncDeviceIpUi(active);
                PopulateMasterDevicesList();
                PopulateDeviceSelector();
                _ = RefreshStatusAsync();

                if (resolved > 0)
                {
                    MessageBox.Show(this,
                        $"⚡ Auto-LAN Complete!\n\nSuccessfully resolved {resolved} of {total} lamp(s) to local LAN IPs (~25 ms ultra-fast control).\n\nAll detected lamps are now configured for instant local control.",
                        "SmartLamp Auto-LAN", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(this,
                        $"Scanned local network, but could not resolve local IPs for the {total} configured device(s).\n\nPlease verify that the PC and smart lamps are on the same Wi-Fi subnet and powered on.",
                        "SmartLamp Auto-LAN", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            finally
            {
                BtnAutoResolveAll.IsEnabled = true;
                BtnAutoResolveAll.Content = LocalizationService.T("AutoResolveAllBtn");
            }
        }

        private async Task AutoDetectLocalIpAsync(string devId, string? localKey = null, string version = "3.5", bool showFeedback = false)
        {
            if (string.IsNullOrEmpty(devId) || _protocol == null) return;

            if (string.IsNullOrEmpty(localKey))
            {
                localKey = GetEnteredLocalKey();
            }

            // 1. Check if already known in _discoveredList with matching devId
            string? foundIp = _discoveredList?.FirstOrDefault(d => d.DevId == devId && TuyaSharingCrypto.IsPrivateIp(d.Ip))?.Ip;

            // 2. Perform intelligent local search with cryptographic probe
            if (string.IsNullOrEmpty(foundIp))
            {
                foundIp = await _protocol.FindLocalIpForDeviceAsync(devId, localKey, version, 3000);
            }

            if (!string.IsNullOrEmpty(foundIp))
            {
                var active = _protocol.GetActiveDevice();
                if (active != null && (active.DevId == devId || _config?.devices?.Count == 1))
                {
                    if (!string.IsNullOrEmpty(active.Ip) && !TuyaSharingCrypto.IsPrivateIp(active.Ip))
                    {
                        active.ExternalIp = active.Ip;
                    }
                    active.LocalIp = foundIp;
                    active.Ip = foundIp;
                    active.UseLocalIp = true;

                    TxtIp.Text = foundIp;
                    if (RbIpLocal != null) RbIpLocal.IsChecked = true;

                    ConfigManager.Save(_config);
                    _protocol.UpdateConfig(_config);
                    TuyaProtocol.ResetLocalClients();
                    PopulateMasterDevicesList();
                    PopulateDeviceSelector();
                    _ = RefreshStatusAsync();
                }

                if (showFeedback)
                {
                    MessageBox.Show(this, $"🟢 Local IP found: {foundIp}\nAssigned for instant local control (~25 ms latency)!", "SmartLamp", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                if (showFeedback)
                {
                    MessageBox.Show(this, $"Could not find device '{devId}' on the local network.\nEnsure lamp and PC are on the same Wi-Fi / subnet.", "SmartLamp", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
}
