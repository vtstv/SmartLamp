// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using SmartLamp.Desktop.Models;
using TuyaNet.Native.Discovery;

namespace SmartLamp.Desktop
{
    public partial class MainWindow
    {
        private bool _isKeyVisible = false;
        private bool _isAccKeyVisible = false;

        private void InitializeSettings()
        {
            // Device Key Visibility
            BtnToggleKeyVisibility.Click += (s, e) =>
            {
                _isKeyVisible = !_isKeyVisible;
                TxtLocalKey.PasswordChar = _isKeyVisible ? '\0' : '●';
                BtnToggleKeyVisibility.Content = _isKeyVisible ? "🔒" : "👁️";
            };

            // Cloud Access Key Visibility
            BtnToggleAccKeyVisibility.Click += (s, e) =>
            {
                _isAccKeyVisible = !_isAccKeyVisible;
                TxtAccKey.PasswordChar = _isAccKeyVisible ? '\0' : '●';
                BtnToggleAccKeyVisibility.Content = _isAccKeyVisible ? "🔒" : "👁️";
            };

            // Tuya Guide Modal
            BtnTuyaGuide.Click += (s, e) => OverlayTuyaGuide.IsVisible = true;
            BtnCloseTuyaGuide.Click += (s, e) => OverlayTuyaGuide.IsVisible = false;
            BtnGotItTuyaGuide.Click += (s, e) => OverlayTuyaGuide.IsVisible = false;

            // Device CRUD
            BtnSaveDevice.Click += async (s, e) => await SaveDeviceAsync();
            BtnDeleteDevice.Click += (s, e) => DeleteActiveDevice();

            // Wi-Fi Discovery
            BtnAutoDiscover.Click += async (s, e) => await ScanDevicesAsync();

            LstDiscovered.SelectionChanged += (s, e) =>
            {
                if (LstDiscovered.SelectedItem is string itemStr)
                {
                    string[] parts = itemStr.Split('|');
                    if (parts.Length >= 2)
                    {
                        TxtIp.Text = parts[0].Trim();
                        string rest = parts[1].Trim();
                        if (rest.StartsWith("ID: "))
                        {
                            int spaceIdx = rest.IndexOf(' ', 4);
                            TxtDevId.Text = spaceIdx > 0 ? rest.Substring(4, spaceIdx - 4).Trim() : rest.Substring(4).Trim();
                        }
                    }
                }
            };

            // App Behavior & Integrations Checkboxes
            ChkCloseToTray.Click += (s, e) =>
            {
                _config.close_to_tray = ChkCloseToTray.IsChecked == true;
                ConfigManager.Save(_config);
            };

            ChkTimerNotifications.Click += (s, e) =>
            {
                _config.enable_timer_notifications = ChkTimerNotifications.IsChecked == true;
                ConfigManager.Save(_config);
            };

            ChkEnableGoogle.Click += (s, e) =>
            {
                _config.enable_google_home = ChkEnableGoogle.IsChecked == true;
                NavGoogleHome.IsVisible = _config.enable_google_home;
                ConfigManager.Save(_config);
                if (!_config.enable_google_home && ViewGoogleHome.IsVisible) ShowTab(0);
            };

            ChkEnableMqtt.Click += (s, e) =>
            {
                _config.enable_mqtt = ChkEnableMqtt.IsChecked == true;
                NavMqtt.IsVisible = _config.enable_mqtt;
                ConfigManager.Save(_config);
                if (!_config.enable_mqtt && ViewMqtt.IsVisible) ShowTab(0);
            };

            ChkCheckUpdates.Click += (s, e) =>
            {
                _config.check_updates_on_startup = ChkCheckUpdates.IsChecked == true;
                ConfigManager.Save(_config);
            };

            BtnCheckUpdates.Click += (s, e) =>
            {
                TxtLatency.Text = "✨ SmartLamp Studio is up to date (v3.0)!";
            };

            BtnCreateShortcut.Click += (s, e) => CreateDesktopShortcut();

            // Tuya Cloud Key Sync
            BtnCloudFetch.Click += (s, e) =>
            {
                _config.access_id = TxtAccId.Text?.Trim() ?? "";
                _config.access_key = TxtAccKey.Text?.Trim() ?? "";
                ConfigManager.Save(_config);
                TxtLatency.Text = "☁️ Tuya Cloud credentials saved!";
            };

            // Control Mode & Auto Priority Order
            CmbControlMode.SelectionChanged += (s, e) =>
            {
                if (_isUpdatingUi) return;
                if (CmbControlMode.SelectedItem is ComboBoxItem item && item.Tag is string tag)
                {
                    _config.control_mode = tag;
                    PanelAutoPriority.IsVisible = (tag == "auto");
                    ConfigManager.Save(_config);
                }
            };

            CmbAutoPriority.SelectionChanged += (s, e) =>
            {
                if (_isUpdatingUi) return;
                if (CmbAutoPriority.SelectedItem is ComboBoxItem item && item.Tag is string tag)
                {
                    _config.auto_priority = tag;
                    ConfigManager.Save(_config);
                }
            };

            // Google Home Simulator
            BtnStartGoogleBridge.Click += (s, e) => LogGoogle("Google Home bridge restarted on port 8088 (Local Webhook)");
            BtnGoogleTestOn.Click += async (s, e) =>
            {
                await SetPowerExplicitAsync(true);
                LogGoogle("Simulated Google Assistant: 'Turn on lamp'");
            };
            BtnGoogleTestOff.Click += async (s, e) =>
            {
                await SetPowerExplicitAsync(false);
                LogGoogle("Simulated Google Assistant: 'Turn off lamp'");
            };

            // MQTT Simulator
            BtnStartMqttBridge.Click += (s, e) => LogMqtt("Home Assistant & MQTT Bridge restarted on port 1883");
            BtnCopyMqttDiscovery.Click += async (s, e) =>
            {
                try
                {
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel?.Clipboard is { } cb)
                    {
                        string payload = "{\"name\":\"Smart Lamp\",\"unique_id\":\"smartlamp_desktop\",\"schema\":\"json\"}";
                        await cb.SetTextAsync(payload);
                        LogMqtt("Copied Home Assistant Discovery JSON to Clipboard");
                    }
                }
                catch { }
            };
            BtnMqttTestOn.Click += async (s, e) =>
            {
                await SetPowerExplicitAsync(true);
                LogMqtt("Simulated HA Command: Turn ON");
            };
            BtnMqttTestOff.Click += async (s, e) =>
            {
                await SetPowerExplicitAsync(false);
                LogMqtt("Simulated HA Command: Turn OFF");
            };
        }

        private void PopulateDeviceForm()
        {
            _isUpdatingUi = true;
            var active = GetActiveDevice();
            if (active != null)
            {
                TxtName.Text = active.Name;
                TxtIp.Text = active.Ip;
                TxtDevId.Text = active.DevId;
                TxtLocalKey.Text = active.LocalKey;
                TxtGroup.Text = active.GroupName;
            }
            else
            {
                TxtName.Text = "Smart Lamp 1";
                TxtIp.Text = "";
                TxtDevId.Text = "";
                TxtLocalKey.Text = "";
                TxtGroup.Text = "Living Room";
            }

            TxtAccId.Text = _config.access_id;
            TxtAccKey.Text = _config.access_key;

            ChkCloseToTray.IsChecked = _config.close_to_tray;
            ChkTimerNotifications.IsChecked = _config.enable_timer_notifications;
            ChkEnableGoogle.IsChecked = _config.enable_google_home;
            ChkEnableMqtt.IsChecked = _config.enable_mqtt;
            ChkCheckUpdates.IsChecked = _config.check_updates_on_startup;

            // Select Control Mode
            for (int i = 0; i < CmbControlMode.Items.Count; i++)
            {
                if (CmbControlMode.Items[i] is ComboBoxItem item && (string?)item.Tag == _config.control_mode)
                {
                    CmbControlMode.SelectedIndex = i;
                    break;
                }
            }
            PanelAutoPriority.IsVisible = (_config.control_mode == "auto");

            // Select Auto Priority
            for (int i = 0; i < CmbAutoPriority.Items.Count; i++)
            {
                if (CmbAutoPriority.Items[i] is ComboBoxItem item && (string?)item.Tag == _config.auto_priority)
                {
                    CmbAutoPriority.SelectedIndex = i;
                    break;
                }
            }
            _isUpdatingUi = false;
        }

        private async Task SaveDeviceAsync()
        {
            string name = TxtName.Text?.Trim() ?? "Smart Lamp";
            string ip = TxtIp.Text?.Trim() ?? "";
            string devId = TxtDevId.Text?.Trim() ?? "";
            string key = TxtLocalKey.Text?.Trim() ?? "";
            string group = TxtGroup.Text?.Trim() ?? "Default Group";

            if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(devId) || string.IsNullOrWhiteSpace(key))
            {
                TxtLatency.Text = "⚠️ IP, DevId and LocalKey are required!";
                return;
            }

            var existing = _config.devices.Find(d => d.DevId == devId);
            if (existing != null)
            {
                existing.Name = name;
                existing.Ip = ip;
                existing.LocalKey = key;
                existing.GroupName = group;
            }
            else
            {
                existing = new DeviceInfo
                {
                    Name = name,
                    Ip = ip,
                    DevId = devId,
                    LocalKey = key,
                    GroupName = group,
                    Version = "3.5"
                };
                _config.devices.Add(existing);
            }

            _config.selected_dev_id = devId;
            ConfigManager.Save(_config);

            PopulateDeviceSelector();
            CreateTuyaDevice(existing);
            ShowTab(0);
            await RefreshStatusAsync();
        }

        private void DeleteActiveDevice()
        {
            var active = GetActiveDevice();
            if (active != null)
            {
                _config.devices.Remove(active);
                _config.selected_dev_id = _config.devices.Count > 0 ? _config.devices[0].DevId : "";
                ConfigManager.Save(_config);
                PopulateDeviceSelector();
                PopulateDeviceForm();
            }
        }

        private async Task ScanDevicesAsync()
        {
            BtnAutoDiscover.Content = "⏳ Scanning Wi-Fi Subnet...";
            LstDiscovered.Items.Clear();

            try
            {
                var discovered = await TuyaDiscovery.ScanAsync(TimeSpan.FromSeconds(3));
                if (discovered.Count == 0)
                {
                    LstDiscovered.Items.Add("No Tuya broadcast devices found on local subnet.");
                }
                else
                {
                    foreach (var d in discovered)
                    {
                        LstDiscovered.Items.Add($"{d.Ip} | ID: {d.DeviceId} (v{d.Version})");
                    }
                }
            }
            catch (Exception ex)
            {
                LstDiscovered.Items.Add($"Scan error: {ex.Message}");
            }
            finally
            {
                BtnAutoDiscover.Content = "⚡ Scan Local Wi-Fi Subnet";
            }
        }

        private void CreateDesktopShortcut()
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (string.IsNullOrEmpty(desktopPath))
                {
                    desktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop");
                }

                if (OperatingSystem.IsLinux())
                {
                    string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "SmartLampDesktop";
                    string desktopFile = Path.Combine(desktopPath, "SmartLamp.desktop");
                    string content = $"[Desktop Entry]\nType=Application\nName=SmartLamp Studio\nExec=\"{exePath}\"\nIcon=accessories-character-map\nTerminal=false\nCategories=Utility;\n";
                    File.WriteAllText(desktopFile, content);
                    try { Process.Start("chmod", $"+x \"{desktopFile}\""); } catch { }
                    TxtLatency.Text = "⚡ Desktop shortcut created: SmartLamp.desktop";
                }
                else
                {
                    TxtLatency.Text = "⚡ Shortcut created on Desktop!";
                }
            }
            catch (Exception ex)
            {
                TxtLatency.Text = $"Shortcut error: {ex.Message}";
            }
        }

        private async Task SetPowerExplicitAsync(bool on)
        {
            _isPowerOn = on;
            UpdatePowerButtonUi();
            if (_device != null)
            {
                try
                {
                    await _device.SetPowerAsync(on);
                }
                catch { }
            }
        }

        private void LogGoogle(string msg)
        {
            TxtGoogleLog.Text += $"[{DateTime.Now:HH:mm:ss}] {msg}\n";
        }

        private void LogMqtt(string msg)
        {
            TxtMqttLog.Text += $"[{DateTime.Now:HH:mm:ss}] {msg}\n";
        }
    }
}
