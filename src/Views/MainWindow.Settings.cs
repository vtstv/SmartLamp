// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using SmartLampApp.Models;

using ColorConverter = System.Windows.Media.ColorConverter;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;

namespace SmartLampApp
{
    public partial class MainWindow : Window
    {
        private void ChkConfigToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;

            _config.close_to_tray = ChkCloseToTray.IsChecked == true;
            
            bool prevGoogle = _config.enable_google_home;
            _config.enable_google_home = ChkEnableGoogle.IsChecked == true;
            
            bool prevMqtt = _config.enable_mqtt;
            _config.enable_mqtt = ChkEnableMqtt.IsChecked == true;

            ConfigManager.Save(_config);

            NavGoogleHome.Visibility = _config.enable_google_home ? Visibility.Visible : Visibility.Collapsed;
            NavMqtt.Visibility = _config.enable_mqtt ? Visibility.Visible : Visibility.Collapsed;

            // Handle MQTT Bridge start/stop
            if (_config.enable_mqtt && !prevMqtt)
            {
                _mqttService.StartBridge(1883);
                System.Windows.MessageBox.Show("Home Assistant MQTT Bridge Enabled & Started on Port 1883.", "Integration Enabled", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (!_config.enable_mqtt && prevMqtt)
            {
                _mqttService.StopBridge();
                System.Windows.MessageBox.Show("Home Assistant MQTT Bridge Disabled & Stopped.", "Integration Disabled", MessageBoxButton.OK, MessageBoxImage.Information);
                if (ViewMqtt.Visibility == Visibility.Visible) NavDash_Click(null, null);
            }

            // Handle Google Home start/stop
            if (_config.enable_google_home && !prevGoogle)
            {
                _googleHomeService.StartServer(8088);
                System.Windows.MessageBox.Show("Google Smart Home Bridge Enabled & Started on Port 8088.", "Integration Enabled", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (!_config.enable_google_home && prevGoogle)
            {
                _googleHomeService.StopServer();
                System.Windows.MessageBox.Show("Google Smart Home Bridge Disabled & Stopped.", "Integration Disabled", MessageBoxButton.OK, MessageBoxImage.Information);
                if (ViewGoogleHome.Visibility == Visibility.Visible) NavDash_Click(null, null);
            }
        }

        private void NavDash_Click(object? sender, RoutedEventArgs? e)
        {
            ViewDashboard.Visibility = Visibility.Visible;
            ViewSettings.Visibility = Visibility.Collapsed;
            ViewGoogleHome.Visibility = Visibility.Collapsed;
            ViewMqtt.Visibility = Visibility.Collapsed;

            SetNavActive(NavDash);
        }

        private void NavSettings_Click(object sender, RoutedEventArgs e)
        {
            ViewDashboard.Visibility = Visibility.Collapsed;
            ViewSettings.Visibility = Visibility.Visible;
            ViewGoogleHome.Visibility = Visibility.Collapsed;
            ViewMqtt.Visibility = Visibility.Collapsed;

            SetNavActive(NavSettings);
        }

        private void NavGoogleHome_Click(object sender, RoutedEventArgs e)
        {
            ViewDashboard.Visibility = Visibility.Collapsed;
            ViewSettings.Visibility = Visibility.Collapsed;
            ViewGoogleHome.Visibility = Visibility.Visible;
            ViewMqtt.Visibility = Visibility.Collapsed;

            SetNavActive(NavGoogleHome);
        }

        private void NavMqtt_Click(object sender, RoutedEventArgs e)
        {
            ViewDashboard.Visibility = Visibility.Collapsed;
            ViewSettings.Visibility = Visibility.Collapsed;
            ViewGoogleHome.Visibility = Visibility.Collapsed;
            ViewMqtt.Visibility = Visibility.Visible;

            SetNavActive(NavMqtt);
        }

        private void SetNavActive(Button activeBtn)
        {
            NavDash.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F1F30"));
            NavDash.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA"));

            NavSettings.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F1F30"));
            NavSettings.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA"));

            NavGoogleHome.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F1F30"));
            NavGoogleHome.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA"));

            NavMqtt.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F1F30"));
            NavMqtt.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA"));

            activeBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00F0FF"));
            activeBtn.Foreground = new SolidColorBrush(Colors.Black);
        }

        private void CmbControlMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_config == null) return;
            if (CmbControlMode?.SelectedItem is ComboBoxItem selected)
            {
                string tag = (string)selected.Tag;
                _config.control_mode = tag;
                if (PanelAutoPriority != null)
                {
                    PanelAutoPriority.Visibility = tag == "auto" ? Visibility.Visible : Visibility.Collapsed;
                }
                ConfigManager.Save(_config);
                if (_protocol != null) _protocol.UpdateConfig(_config);
            }
        }

        private void CmbAutoPriority_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_config == null) return;
            if (CmbAutoPriority?.SelectedItem is ComboBoxItem selected)
            {
                string tag = (string)selected.Tag;
                _config.auto_priority = tag;
                ConfigManager.Save(_config);
                if (_protocol != null) _protocol.UpdateConfig(_config);
            }
        }

        private void BtnTuyaGuide_Click(object sender, RoutedEventArgs e)
        {
            OverlayTuyaGuide.Visibility = Visibility.Visible;
        }

        private void BtnCloseTuyaGuide_Click(object sender, RoutedEventArgs e)
        {
            OverlayTuyaGuide.Visibility = Visibility.Collapsed;
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch { }
            e.Handled = true;
        }

        private void BtnCreateShortcut_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string shortcutPath = System.IO.Path.Combine(desktopPath, "Toggle Smart Lamp.lnk");
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName 
                                 ?? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SmartLampApp.exe");

                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app_icon.ico");
                string iconLocation = System.IO.File.Exists(iconPath) ? iconPath : $"{exePath},0";

                Services.InstallationService.CreateShortcut(shortcutPath, exePath, "--toggle", "Toggle Smart Lamp Power", iconLocation);

                System.Windows.MessageBox.Show("Desktop shortcut 'Toggle Smart Lamp' successfully created!\n\nDouble-clicking it will instantly toggle your lamp ON/OFF.", "Desktop Shortcut Created", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to create shortcut: {ex.Message}", "Shortcut Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChkAutostart_Click(object sender, RoutedEventArgs e)
        {
            bool enable = ChkAutostart.IsChecked == true;
            _config.autostart = enable;
            ConfigManager.Save(_config);
            Services.InstallationService.SetAutostart(enable);
        }

        private void BtnInstallApp_Click(object sender, RoutedEventArgs e)
        {
            if (Services.InstallationService.IsInstalledInSystem())
            {
                var result = System.Windows.MessageBox.Show("Uninstall SmartLamp Studio shortcuts and registration?", "Uninstall App", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    Services.InstallationService.UninstallFromSystem(out string msg);
                    System.Windows.MessageBox.Show(msg, "Uninstalled", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateInstallButtonState();
                }
            }
            else
            {
                Services.InstallationService.InstallToSystem(out string msg);
                System.Windows.MessageBox.Show(msg, "App Installed", MessageBoxButton.OK, MessageBoxImage.Information);
                UpdateInstallButtonState();
            }
        }

        private void UpdateInstallButtonState()
        {
            if (Services.InstallationService.IsInstalledInSystem())
            {
                BtnInstallApp.Content = "🗑️ Uninstall App";
                BtnInstallApp.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC3545"));
            }
            else
            {
                BtnInstallApp.Content = "📦 Install App";
                BtnInstallApp.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981"));
            }
        }

        private void GoogleHomeService_OnLogMessage(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                TxtGoogleLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
                TxtGoogleLog.ScrollToEnd();
            });
        }

        private void BtnStartGoogleBridge_Click(object sender, RoutedEventArgs e)
        {
            _googleHomeService.StopServer();
            _googleHomeService.StartServer(8088);
            System.Windows.MessageBox.Show("Google Smart Home Local Bridge restarted on port 8088!", "Google Home", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnGoogleTestOn_Click(object sender, RoutedEventArgs e)
        {
            _isPowerOn = true;
            UpdatePowerButtonUi();
            await _protocol.SetPowerAsync(true);
            GoogleHomeService_OnLogMessage("Simulated Google Assistant Voice Command: 'Hey Google, turn on the lamp'");
        }

        private async void BtnGoogleTestOff_Click(object sender, RoutedEventArgs e)
        {
            _isPowerOn = false;
            UpdatePowerButtonUi();
            await _protocol.SetPowerAsync(false);
            GoogleHomeService_OnLogMessage("Simulated Google Assistant Voice Command: 'Hey Google, turn off the lamp'");
        }

        private void MqttService_OnLogMessage(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                TxtMqttLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
                TxtMqttLog.ScrollToEnd();
            });
        }

        private void BtnStartMqttBridge_Click(object sender, RoutedEventArgs e)
        {
            _mqttService.StopBridge();
            _mqttService.StartBridge(1883);
            System.Windows.MessageBox.Show("Home Assistant & MQTT REST Bridge restarted on port 1883!", "MQTT Bridge", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnCopyMqttDiscovery_Click(object sender, RoutedEventArgs e)
        {
            string discoveryPayload = _mqttService.GetHomeAssistantDiscoveryPayload();
            System.Windows.Clipboard.SetText(discoveryPayload);
            System.Windows.MessageBox.Show("Home Assistant Auto-Discovery JSON copied to Clipboard!", "HA Discovery", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnMqttTestOn_Click(object sender, RoutedEventArgs e)
        {
            _isPowerOn = true;
            UpdatePowerButtonUi();
            await _protocol.SetPowerAsync(true);
            MqttService_OnLogMessage("Simulated Home Assistant Command: Turn ON");
        }

        private async void BtnMqttTestOff_Click(object sender, RoutedEventArgs e)
        {
            _isPowerOn = false;
            UpdatePowerButtonUi();
            await _protocol.SetPowerAsync(false);
            MqttService_OnLogMessage("Simulated Home Assistant Command: Turn OFF");
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshStatusAsync();
        }

        private void BtnAuthor_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/vtstv",
                UseShellExecute = true
            });
        }
    }
}
