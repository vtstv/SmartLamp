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
        private void SyncDeviceIpUi(DeviceInfo active)
        {
            if (string.IsNullOrEmpty(active.LocalIp) && TuyaSharingCrypto.IsPrivateIp(active.Ip))
            {
                active.LocalIp = active.Ip;
            }
            else if (string.IsNullOrEmpty(active.ExternalIp) && !string.IsNullOrEmpty(active.Ip) && !TuyaSharingCrypto.IsPrivateIp(active.Ip))
            {
                active.ExternalIp = active.Ip;
            }

            _isUpdatingUi = true;
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
            _isUpdatingUi = false;
        }

        private void RbIpType_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            var active = _protocol.GetActiveDevice();
            if (active == null) return;

            if (RbIpLocal?.IsChecked == true)
            {
                active.UseLocalIp = true;
                if (!string.IsNullOrEmpty(active.LocalIp))
                {
                    TxtIp.Text = active.LocalIp;
                }
                else
                {
                    _ = AutoDetectLocalIpAsync(active.DevId, showFeedback: false);
                }
            }
            else if (RbIpExternal?.IsChecked == true)
            {
                active.UseLocalIp = false;
                if (!string.IsNullOrEmpty(active.ExternalIp))
                {
                    TxtIp.Text = active.ExternalIp;
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

            BtnAutoDetectIp.IsEnabled = false;
            BtnAutoDetectIp.Content = "⏳ Scanning...";
            try
            {
                await AutoDetectLocalIpAsync(devId, showFeedback: true);
            }
            finally
            {
                BtnAutoDetectIp.IsEnabled = true;
                BtnAutoDetectIp.Content = "🔍 Auto-LAN";
            }
        }

        private async Task AutoDetectLocalIpAsync(string devId, bool showFeedback = false)
        {
            if (string.IsNullOrEmpty(devId)) return;

            // 1. Check if already known in _discoveredList
            string? foundIp = _discoveredList?.FirstOrDefault(d => d.DevId == devId && TuyaSharingCrypto.IsPrivateIp(d.Ip))?.Ip;

            // 2. If not, perform UDP scan
            if (string.IsNullOrEmpty(foundIp))
            {
                foundIp = await _protocol.FindLocalIpForDeviceAsync(devId, 2500);
            }

            if (!string.IsNullOrEmpty(foundIp))
            {
                var active = _protocol.GetActiveDevice();
                if (active != null && active.DevId == devId)
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
