// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using SmartLampApp.Models;
using SmartLampApp.Services;

using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using MessageBox = System.Windows.MessageBox;
using Brushes = System.Windows.Media.Brushes;

namespace SmartLampApp
{
    public partial class MainWindow : Window
    {
        private enum WizardMethod
        {
            WifiDiscovery,
            CloudSync,
            Manual
        }

        private WizardMethod _wizardMethod = WizardMethod.WifiDiscovery;
        private int _wizardCurrentStep = 1;
        private List<DiscoveredDevice> _wizardDiscoveredDevices = new();
        private List<DeviceInfo> _wizardCloudDevices = new();
        private DeviceInfo? _wizardCandidateDevice;

        public void BtnOpenWizard_Click(object sender, RoutedEventArgs e)
        {
            _wizardCurrentStep = 1;
            _wizardCandidateDevice = null;
            TxtWizTestResult.Text = LocalizationService.T("WizardTestPrompt");
            TxtWizTestResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA"));

            if (!string.IsNullOrWhiteSpace(_config.access_id))
            {
                TxtWizAccId.Text = _config.access_id;
            }
            if (!string.IsNullOrWhiteSpace(_config.access_key))
            {
                TxtWizAccKey.Text = _config.access_key;
            }

            OverlayDeviceWizard.Visibility = Visibility.Visible;
            UpdateWizardStepUi();
        }

        private void BtnCloseWizard_Click(object sender, RoutedEventArgs e)
        {
            OverlayDeviceWizard.Visibility = Visibility.Collapsed;
        }

        private void BtnWizardChooseWifi_Click(object sender, RoutedEventArgs e)
        {
            _wizardMethod = WizardMethod.WifiDiscovery;
            _wizardCurrentStep = 2;
            UpdateWizardStepUi();

            if (_wizardDiscoveredDevices.Count == 0)
            {
                BtnWizRescan_Click(sender, e);
            }
        }

        private void BtnWizardChooseCloud_Click(object sender, RoutedEventArgs e)
        {
            _wizardMethod = WizardMethod.CloudSync;
            _wizardCurrentStep = 2;
            UpdateWizardStepUi();
        }

        private void BtnWizardChooseManual_Click(object sender, RoutedEventArgs e)
        {
            _wizardMethod = WizardMethod.Manual;
            _wizardCurrentStep = 2;
            UpdateWizardStepUi();
        }

        private async void BtnWizRescan_Click(object sender, RoutedEventArgs e)
        {
            BtnWizRescan.Content = LocalizationService.T("WizardScanning");
            BtnWizRescan.IsEnabled = false;
            LstWizDiscovered.Items.Clear();

            _wizardDiscoveredDevices = await _protocol.AutoDiscoverDevicesAsync();

            BtnWizRescan.Content = LocalizationService.T("WizardScanNow");
            BtnWizRescan.IsEnabled = true;

            if (_wizardDiscoveredDevices.Count == 0)
            {
                LstWizDiscovered.Items.Add(LocalizationService.T("WizardNoDevicesFound"));
            }
            else
            {
                foreach (var dev in _wizardDiscoveredDevices)
                {
                    var known = _config.devices.FirstOrDefault(d => d.Ip == dev.Ip || (!string.IsNullOrEmpty(d.DevId) && d.DevId == dev.DevId));
                    string status = known != null ? "✅ [Added]" : "➕ [New]";
                    string idDisplay = dev.DevId.StartsWith("lan_") ? "Port 6668" : $"ID: {dev.DevId}";
                    LstWizDiscovered.Items.Add($"{status} {dev.ProductName} — IP: {dev.Ip} ({idDisplay})");
                }
                LstWizDiscovered.SelectedIndex = 0;
            }
        }

        private void LstWizDiscovered_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int idx = LstWizDiscovered.SelectedIndex;
            if (idx >= 0 && idx < _wizardDiscoveredDevices.Count)
            {
                var sel = _wizardDiscoveredDevices[idx];
                TxtWizDevId.Text = sel.DevId.StartsWith("lan_") ? "" : sel.DevId;

                var known = _config.devices.FirstOrDefault(d => d.Ip == sel.Ip || (!string.IsNullOrEmpty(d.DevId) && d.DevId == sel.DevId));
                if (known != null && !string.IsNullOrWhiteSpace(known.LocalKey))
                {
                    TxtWizLocalKey.Text = known.LocalKey;
                    TxtWizKeyStatus.Text = LocalizationService.T("WizardKeyDetected");
                    TxtWizKeyStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                }
                else
                {
                    TxtWizLocalKey.Text = "";
                    TxtWizKeyStatus.Text = LocalizationService.T("WizardKeyNeededPrompt");
                    TxtWizKeyStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEA00"));
                }
            }
        }

        private async void BtnWizLoadCloud_Click(object sender, RoutedEventArgs e)
        {
            string accId = TxtWizAccId.Text.Trim();
            string accKey = TxtWizAccKey.Text.Trim();

            if (string.IsNullOrWhiteSpace(accId) || string.IsNullOrWhiteSpace(accKey))
            {
                MessageBox.Show("Please enter your Tuya Access ID and Access Key.", "Credentials Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnWizLoadCloud.Content = "⏳ Loading...";
            BtnWizLoadCloud.IsEnabled = false;
            LstWizCloudDevices.Items.Clear();

            var (devices, err) = await _protocol.FetchAllCloudDevicesAsync(accId, accKey, _config.region);
            _wizardCloudDevices = devices;

            BtnWizLoadCloud.Content = LocalizationService.T("WizardCloudFetchBtn");
            BtnWizLoadCloud.IsEnabled = true;

            if (!string.IsNullOrEmpty(err) && devices.Count == 0)
            {
                MessageBox.Show($"Cloud fetch error: {err}", "Tuya Cloud", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_wizardCloudDevices.Count == 0)
            {
                LstWizCloudDevices.Items.Add("No devices found in this Tuya Cloud project.");
            }
            else
            {
                foreach (var d in _wizardCloudDevices)
                {
                    string ipPart = string.IsNullOrEmpty(d.Ip) ? "Cloud Online" : d.Ip;
                    LstWizCloudDevices.Items.Add($"💡 {d.Name} — ID: {d.DevId} ({ipPart})");
                }
                LstWizCloudDevices.SelectedIndex = 0;
            }
        }

        private void LstWizCloudDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Selection handler for cloud list
        }

        private void BtnWizStep2Back_Click(object sender, RoutedEventArgs e)
        {
            _wizardCurrentStep = 1;
            UpdateWizardStepUi();
        }

        private void BtnWizStep2Next_Click(object sender, RoutedEventArgs e)
        {
            if (_wizardMethod == WizardMethod.WifiDiscovery)
            {
                int idx = LstWizDiscovered.SelectedIndex;
                if (idx < 0 || idx >= _wizardDiscoveredDevices.Count)
                {
                    MessageBox.Show(LocalizationService.T("WizardSelectDevicePrompt"), "Device Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var sel = _wizardDiscoveredDevices[idx];
                string devId = TxtWizDevId.Text.Trim();
                string key = TxtWizLocalKey.Text.Trim();

                if (string.IsNullOrWhiteSpace(devId))
                {
                    MessageBox.Show("Please specify or confirm the Device ID.", "Device ID Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _wizardCandidateDevice = new DeviceInfo
                {
                    Name = sel.ProductName,
                    Ip = sel.Ip,
                    DevId = devId,
                    LocalKey = key,
                    GroupName = "Living Room"
                };
            }
            else if (_wizardMethod == WizardMethod.CloudSync)
            {
                int idx = LstWizCloudDevices.SelectedIndex;
                if (idx < 0 || idx >= _wizardCloudDevices.Count)
                {
                    MessageBox.Show("Please select a device from the cloud list above.", "Device Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var sel = _wizardCloudDevices[idx];
                _wizardCandidateDevice = new DeviceInfo
                {
                    Name = sel.Name,
                    Ip = sel.Ip,
                    DevId = sel.DevId,
                    LocalKey = sel.LocalKey,
                    GroupName = string.IsNullOrWhiteSpace(sel.GroupName) ? "Living Room" : sel.GroupName
                };
            }
            else // Manual
            {
                string ip = TxtWizManualIp.Text.Trim();
                string devId = TxtWizManualId.Text.Trim();
                string key = TxtWizManualKey.Text.Trim();

                if (string.IsNullOrWhiteSpace(devId) || string.IsNullOrWhiteSpace(ip))
                {
                    MessageBox.Show("Please enter both the IP address and Device ID.", "Fields Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _wizardCandidateDevice = new DeviceInfo
                {
                    Name = "Smart Lamp",
                    Ip = ip,
                    DevId = devId,
                    LocalKey = key,
                    GroupName = "Living Room"
                };
            }

            TxtWizFinalName.Text = string.IsNullOrWhiteSpace(_wizardCandidateDevice.Name) ? "Smart Lamp" : _wizardCandidateDevice.Name;
            TxtWizFinalGroup.Text = string.IsNullOrWhiteSpace(_wizardCandidateDevice.GroupName) ? "Living Room" : _wizardCandidateDevice.GroupName;
            TxtWizTestResult.Text = LocalizationService.T("WizardTestPrompt");
            TxtWizTestResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA"));

            _wizardCurrentStep = 3;
            UpdateWizardStepUi();
        }

        private void BtnWizStep3Back_Click(object sender, RoutedEventArgs e)
        {
            _wizardCurrentStep = 2;
            UpdateWizardStepUi();
        }

        private async void BtnWizTestToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_wizardCandidateDevice == null) return;

            BtnWizTestToggle.IsEnabled = false;
            BtnWizTestToggle.Content = "⏳ Testing...";
            try
            {
                bool success = await _protocol.TogglePowerAsync(_wizardCandidateDevice);
                if (success)
                {
                    TxtWizTestResult.Text = LocalizationService.T("WizardTestSuccess");
                    TxtWizTestResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                }
                else
                {
                    TxtWizTestResult.Text = LocalizationService.T("WizardTestFail");
                    TxtWizTestResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                }
            }
            catch (Exception ex)
            {
                TxtWizTestResult.Text = $"🔴 {ex.Message}";
                TxtWizTestResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
            }
            finally
            {
                BtnWizTestToggle.Content = LocalizationService.T("WizardTestConnection");
                BtnWizTestToggle.IsEnabled = true;
            }
        }

        private void BtnWizFinish_Click(object sender, RoutedEventArgs e)
        {
            if (_wizardCandidateDevice == null) return;

            _wizardCandidateDevice.Name = string.IsNullOrWhiteSpace(TxtWizFinalName.Text) ? "Smart Lamp" : TxtWizFinalName.Text.Trim();
            _wizardCandidateDevice.GroupName = string.IsNullOrWhiteSpace(TxtWizFinalGroup.Text) ? "Living Room" : TxtWizFinalGroup.Text.Trim();

            // Save to config devices list
            var existing = _config.devices.FirstOrDefault(d => d.DevId == _wizardCandidateDevice.DevId);
            if (existing != null)
            {
                existing.Name = _wizardCandidateDevice.Name;
                existing.Ip = _wizardCandidateDevice.Ip;
                existing.LocalKey = _wizardCandidateDevice.LocalKey;
                existing.GroupName = _wizardCandidateDevice.GroupName;
            }
            else
            {
                _config.devices.Add(_wizardCandidateDevice);
            }

            _config.selected_dev_id = _wizardCandidateDevice.DevId;
            _config.is_group_control_active = false;

            // If access keys were entered in wizard, persist them
            if (!string.IsNullOrWhiteSpace(TxtWizAccId.Text))
            {
                _config.access_id = TxtWizAccId.Text.Trim();
            }
            if (!string.IsNullOrWhiteSpace(TxtWizAccKey.Text))
            {
                _config.access_key = TxtWizAccKey.Text.Trim();
            }

            ConfigManager.Save(_config);
            _protocol.UpdateConfig(_config);

            PopulateDeviceSelector();
            PopulateMasterDevicesList();
            PopulateDeviceForm();
            _ = RefreshStatusAsync();

            OverlayDeviceWizard.Visibility = Visibility.Collapsed;
            MessageBox.Show(LocalizationService.T("WizardSuccessMsg", _wizardCandidateDevice.Name), "SmartLamp", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateWizardStepUi()
        {
            var activeCol = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#651FFF"));
            var doneCol = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            var idleCol = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222236"));
            var textActiveCol = Brushes.White;
            var textMutedCol = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8888AA"));

            if (_wizardCurrentStep == 1)
            {
                PanelWizardStep1.Visibility = Visibility.Visible;
                PanelWizardStep2.Visibility = Visibility.Collapsed;
                PanelWizardStep3.Visibility = Visibility.Collapsed;

                TxtWizardSubtitle.Text = LocalizationService.T("WizardStep1Subtitle");

                BadgeStep1.Background = activeCol;
                TxtBadgeStep1.Foreground = textActiveCol;

                BadgeStep2.Background = idleCol;
                TxtBadgeStep2.Foreground = textMutedCol;

                BadgeStep3.Background = idleCol;
                TxtBadgeStep3.Foreground = textMutedCol;
            }
            else if (_wizardCurrentStep == 2)
            {
                PanelWizardStep1.Visibility = Visibility.Collapsed;
                PanelWizardStep2.Visibility = Visibility.Visible;
                PanelWizardStep3.Visibility = Visibility.Collapsed;

                TxtWizardSubtitle.Text = LocalizationService.T("WizardStep2Subtitle");

                BadgeStep1.Background = doneCol;
                TxtBadgeStep1.Foreground = textActiveCol;

                BadgeStep2.Background = activeCol;
                TxtBadgeStep2.Foreground = textActiveCol;

                BadgeStep3.Background = idleCol;
                TxtBadgeStep3.Foreground = textMutedCol;

                SubPanelWizWifi.Visibility = (_wizardMethod == WizardMethod.WifiDiscovery) ? Visibility.Visible : Visibility.Collapsed;
                SubPanelWizCloud.Visibility = (_wizardMethod == WizardMethod.CloudSync) ? Visibility.Visible : Visibility.Collapsed;
                SubPanelWizManual.Visibility = (_wizardMethod == WizardMethod.Manual) ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (_wizardCurrentStep == 3)
            {
                PanelWizardStep1.Visibility = Visibility.Collapsed;
                PanelWizardStep2.Visibility = Visibility.Collapsed;
                PanelWizardStep3.Visibility = Visibility.Visible;

                TxtWizardSubtitle.Text = LocalizationService.T("WizardStep3Subtitle");

                BadgeStep1.Background = doneCol;
                TxtBadgeStep1.Foreground = textActiveCol;

                BadgeStep2.Background = doneCol;
                TxtBadgeStep2.Foreground = textActiveCol;

                BadgeStep3.Background = activeCol;
                TxtBadgeStep3.Foreground = textActiveCol;
            }
        }
    }
}
