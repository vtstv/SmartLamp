// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

using SmartLampApp.Models;
using SmartLampApp.Services;

using ColorConverter = System.Windows.Media.ColorConverter;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;

namespace SmartLampApp
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer? _brightDebounceTimer;
        private DispatcherTimer? _tempDebounceTimer;

        private void UpdateStatusPill(string text, string hexColor)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
            PillStatus.Background = brush;
            TxtStatus.Text = text;
            if (PillCompactStatus != null) PillCompactStatus.Background = brush;
            if (TxtCompactStatus != null) TxtCompactStatus.Text = text;
        }

        private async Task RefreshStatusAsync(bool showConnecting = false)
        {
            if (_isRefreshingStatus) return;
            _isRefreshingStatus = true;
            try
            {
                if (_config.is_group_control_active)
                {
                    var grp = _protocol.GetActiveGroup();
                    if (grp != null)
                    {
                        UpdateStatusPill("👥 GROUP ACTIVE", "#10B981");
                        if (TxtCompactDevice != null) TxtCompactDevice.Text = $"{grp.Icon} {grp.Name}";
                        _isPowerOn = grp.MasterPowerOn;
                        UpdatePowerButtonUi();
                        return;
                    }
                }

                if (!_protocol.IsConfigured)
                {
                    UpdateStatusPill("⚠️ KEY REQUIRED", "#FF9100");
                    _isPowerOn = false;
                    UpdatePowerButtonUi();
                    return;
                }

                if (showConnecting)
                {
                    UpdateStatusPill("⏳ CONNECTING...", "#0284C7");
                }

                var status = await Task.Run(() => _protocol.GetStatusAsync());

                if (!status.IsOnline)
                {
                    UpdateStatusPill("❌ OFFLINE", "#FF1744");
                    _isPowerOn = false;
                    UpdatePowerButtonUi();
                    return;
                }

                UpdateStatusPill("🟢 ONLINE", "#10B981");

                var currentActive = _protocol.GetActiveDevice();
                if (TxtCompactDevice != null && currentActive != null)
                {
                    TxtCompactDevice.Text = currentActive.Name;
                }

                ApplyStatusToUi(status);
            }
            finally
            {
                _isRefreshingStatus = false;
            }
        }

        private void OnIncomingPushStatus(LampStatus status)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (status == null || !status.IsOnline) return;
                UpdateStatusPill("🟢 ONLINE", "#10B981");
                ApplyStatusToUi(status);
            });
        }

        private void ApplyStatusToUi(LampStatus status)
        {
            bool isUserActive = (DateTime.UtcNow - _lastUserCommandTime).TotalSeconds < 4.0;
            if (!isUserActive)
            {
                _isPowerOn = status.IsPowerOn;
                if (!string.IsNullOrWhiteSpace(status.Mode))
                {
                    _currentMode = status.Mode;
                }

                var active = _protocol.GetActiveDevice();
                if (active != null) active.last_power_on = status.IsPowerOn;
                if (status.Brightness > 0)
                {
                    _isUpdatingUi = true;
                    try
                    {
                        SliderBright.Value = status.Brightness;
                        TxtBrightVal.Text = $"{status.Brightness}%";
                        if (SliderCompactBright != null) SliderCompactBright.Value = status.Brightness;
                        if (TxtCompactBrightVal != null) TxtCompactBrightVal.Text = $"{status.Brightness}%";
                        if (active != null) active.last_brightness = status.Brightness;
                        _config.last_brightness = status.Brightness;
                    }
                    finally { _isUpdatingUi = false; }
                }
                if (status.ColorTempK >= 2700)
                {
                    _isUpdatingUi = true;
                    try
                    {
                        SliderTemp.Value = status.ColorTempK;
                        TxtTempVal.Text = $"{status.ColorTempK}K";
                        if (SliderCompactTemp != null) SliderCompactTemp.Value = status.ColorTempK;
                        if (TxtCompactTempVal != null) TxtCompactTempVal.Text = $"{status.ColorTempK}K";
                        if (active != null) active.last_temp = status.ColorTempK;
                        _config.last_temp = status.ColorTempK;
                    }
                    finally { _isUpdatingUi = false; }
                }
                if (!string.IsNullOrWhiteSpace(status.ColorHex) && status.Mode == "colour")
                {
                    TxtHexCode.Text = status.ColorHex;
                }
            }
            UpdatePowerButtonUi();
        }

        public static Color KelvinToColor(int kelvin)
        {
            kelvin = Math.Clamp(kelvin, 2700, 6500);
            float t = (kelvin - 2700f) / (6500f - 2700f);
            byte r, g, b;
            if (t <= 0.5f)
            {
                float localT = t / 0.5f;
                r = 255;
                g = (byte)(179 + (242 - 179) * localT);
                b = (byte)(71 + (214 - 71) * localT);
            }
            else
            {
                float localT = (t - 0.5f) / 0.5f;
                r = (byte)(255 - (255 - 212) * localT);
                g = (byte)(242 - (242 - 236) * localT);
                b = (byte)(214 + (255 - 214) * localT);
            }
            return Color.FromRgb(r, g, b);
        }

        private void UpdatePowerButtonUi()
        {
            if (_isPowerOn)
            {
                BtnPower.Content = LocalizationService.T("PoweredOn");
                BtnPower.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                if (BtnCompactPower != null)
                {
                    BtnCompactPower.Content = LocalizationService.T("PoweredOn");
                    BtnCompactPower.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                }
                UpdateBulbGlow(TxtHexCode?.Text ?? "#FFFFFF", true);
            }
            else
            {
                BtnPower.Content = LocalizationService.T("PoweredOff");
                BtnPower.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1744"));
                if (BtnCompactPower != null)
                {
                    BtnCompactPower.Content = LocalizationService.T("PoweredOff");
                    BtnCompactPower.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1744"));
                }
                UpdateBulbGlow("#333344", false);
            }
        }

        private void UpdateBulbGlow(string hexCode, bool isPowerOn)
        {
            try
            {
                if (!isPowerOn)
                {
                    BulbGlowFrame.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A3E"));
                    if (CompactBulbGlow != null)
                    {
                        CompactBulbGlow.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A3E"));
                    }
                    return;
                }

                Color c;
                if (_currentMode == "white")
                {
                    int k = (int)SliderTemp.Value;
                    c = KelvinToColor(k);
                }
                else
                {
                    c = (Color)ColorConverter.ConvertFromString(hexCode);
                }
                BulbGlowFrame.Background = new SolidColorBrush(c);
                if (CompactBulbGlow != null)
                {
                    CompactBulbGlow.Background = new SolidColorBrush(c);
                }
            }
            catch { }
        }

        public async Task<bool> TogglePowerFromExternalTriggerAsync()
        {
            if (!_protocol.IsConfigured && (!_config.is_group_control_active || _protocol.GetActiveGroup() == null || _protocol.GetDevicesForGroup(_protocol.GetActiveGroup()!).Count == 0))
            {
                _isPowerOn = false;
                UpdatePowerButtonUi();
                return false;
            }

            bool prev = _isPowerOn;
            _isPowerOn = !_isPowerOn;
            _lastUserCommandTime = DateTime.UtcNow;
            UpdatePowerButtonUi();

            if (_config.is_group_control_active)
            {
                var grp = _protocol.GetActiveGroup();
                if (grp != null)
                {
                    grp.MasterPowerOn = _isPowerOn;
                    ConfigManager.Save(_config);
                    var devices = _protocol.GetDevicesForGroup(grp);
                    await _protocol.SetGroupPowerAsync(devices, _isPowerOn);
                    return true;
                }
            }

            bool success = false;
            try
            {
                success = await _protocol.SetPowerAsync(_isPowerOn);
            }
            catch
            {
                success = false;
            }

            if (!success)
            {
                _isPowerOn = prev;
                UpdatePowerButtonUi();
            }

            return success;
        }

        public async Task<bool> SetPowerFromExternalTriggerAsync(bool powerOn)
        {
            if (!_protocol.IsConfigured && (!_config.is_group_control_active || _protocol.GetActiveGroup() == null || _protocol.GetDevicesForGroup(_protocol.GetActiveGroup()!).Count == 0))
            {
                _isPowerOn = false;
                UpdatePowerButtonUi();
                return false;
            }

            bool prev = _isPowerOn;
            _isPowerOn = powerOn;
            _lastUserCommandTime = DateTime.UtcNow;
            UpdatePowerButtonUi();

            if (_config.is_group_control_active)
            {
                var grp = _protocol.GetActiveGroup();
                if (grp != null)
                {
                    grp.MasterPowerOn = _isPowerOn;
                    ConfigManager.Save(_config);
                    var devices = _protocol.GetDevicesForGroup(grp);
                    await _protocol.SetGroupPowerAsync(devices, _isPowerOn);
                    return true;
                }
            }

            bool success = false;
            try
            {
                success = await _protocol.SetPowerAsync(_isPowerOn);
            }
            catch
            {
                success = false;
            }

            if (!success)
            {
                _isPowerOn = prev;
                UpdatePowerButtonUi();
            }

            return success;
        }

        private async void BtnPower_Click(object sender, RoutedEventArgs e)
        {
            bool success = await TogglePowerFromExternalTriggerAsync();
            if (!success)
            {
                System.Windows.MessageBox.Show("Command failed. Please make sure Smart Life mobile app is CLOSED on your phone (Tuya devices allow only 1 socket connection at a time).", "Device Connection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void InitializeDebouncers()
        {
            _brightDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _brightDebounceTimer.Tick += async (s, e) =>
            {
                _brightDebounceTimer.Stop();
                if (_isUpdatingUi || _protocol == null) return;
                int val = (int)SliderBright.Value;

                if (_config.is_group_control_active)
                {
                    var grp = _protocol.GetActiveGroup();
                    if (grp != null)
                    {
                        int baseMaster = grp.MasterBrightness > 0 ? grp.MasterBrightness : 50;
                        grp.MasterBrightness = val;
                        ConfigManager.Save(_config);
                        if (!grp.MasterPowerOn) return;
                        var devices = _protocol.GetDevicesForGroup(grp);
                        await _protocol.SetGroupBrightnessAsync(devices, val, grp.SyncMode, baseMaster);
                        return;
                    }
                }

                var active = _protocol.GetActiveDevice();
                if (active != null) active.last_brightness = val;
                _config.last_brightness = val;
                ConfigManager.Save(_config);

                if (!_isPowerOn) return;

                await _protocol.SetBrightnessAsync(val);
            };

            _tempDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _tempDebounceTimer.Tick += async (s, e) =>
            {
                _tempDebounceTimer.Stop();
                if (_isUpdatingUi || _protocol == null) return;
                int kVal = (int)SliderTemp.Value;

                _currentMode = "white";
                UpdateBulbGlow(TxtHexCode.Text, _isPowerOn);

                if (_config.is_group_control_active)
                {
                    var grp = _protocol.GetActiveGroup();
                    if (grp != null)
                    {
                        grp.MasterTempK = kVal;
                        grp.MasterMode = "white";
                        ConfigManager.Save(_config);
                        if (!grp.MasterPowerOn) return;
                        var devices = _protocol.GetDevicesForGroup(grp);
                        await _protocol.SetGroupColorTempAsync(devices, kVal);
                        return;
                    }
                }

                var active = _protocol.GetActiveDevice();
                if (active != null) active.last_temp = kVal;
                _config.last_temp = kVal;
                ConfigManager.Save(_config);

                if (!_isPowerOn) return;

                await _protocol.SetColorTempAsync(kVal);
            };
        }

        private void SliderBright_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi || _protocol == null) return;
            int val = (int)e.NewValue;
            TxtBrightVal.Text = $"{val}%";
            if (TxtCompactBrightVal != null) TxtCompactBrightVal.Text = $"{val}%";
            if (SliderCompactBright != null && (int)SliderCompactBright.Value != val)
            {
                SliderCompactBright.Value = val;
            }

            if (_brightDebounceTimer == null) InitializeDebouncers();
            _brightDebounceTimer?.Stop();
            _brightDebounceTimer?.Start();
        }

        private void SliderCompactBright_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi || _protocol == null) return;
            int val = (int)e.NewValue;
            if (TxtCompactBrightVal != null) TxtCompactBrightVal.Text = $"{val}%";
            if (SliderBright != null && (int)SliderBright.Value != val)
            {
                SliderBright.Value = val;
            }
        }

        private void SliderTemp_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi || _protocol == null) return;
            int kVal = (int)e.NewValue;
            TxtTempVal.Text = $"{kVal}K";
            if (TxtCompactTempVal != null) TxtCompactTempVal.Text = $"{kVal}K";
            if (SliderCompactTemp != null && (int)SliderCompactTemp.Value != kVal)
            {
                SliderCompactTemp.Value = kVal;
            }

            _currentMode = "white";
            UpdateBulbGlow(TxtHexCode.Text, _isPowerOn);

            if (_tempDebounceTimer == null) InitializeDebouncers();
            _tempDebounceTimer?.Stop();
            _tempDebounceTimer?.Start();
        }

        private void SliderCompactTemp_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi || _protocol == null) return;
            int kVal = (int)e.NewValue;
            if (TxtCompactTempVal != null) TxtCompactTempVal.Text = $"{kVal}K";
            if (SliderTemp != null && (int)SliderTemp.Value != kVal)
            {
                SliderTemp.Value = kVal;
            }
        }

        private void BtnB1_Click(object sender, RoutedEventArgs e) => SliderBright.Value = 1;
        private void BtnB25_Click(object sender, RoutedEventArgs e) => SliderBright.Value = 25;
        private void BtnB50_Click(object sender, RoutedEventArgs e) => SliderBright.Value = 50;
        private void BtnB75_Click(object sender, RoutedEventArgs e) => SliderBright.Value = 75;
        private void BtnB100_Click(object sender, RoutedEventArgs e) => SliderBright.Value = 100;

        private void BtnT2700_Click(object sender, RoutedEventArgs e)
        {
            _currentMode = "white";
            SliderTemp.Value = 2700;
            UpdateBulbGlow(TxtHexCode.Text, _isPowerOn);
        }

        private void BtnT4000_Click(object sender, RoutedEventArgs e)
        {
            _currentMode = "white";
            SliderTemp.Value = 4000;
            UpdateBulbGlow(TxtHexCode.Text, _isPowerOn);
        }

        private void BtnT6500_Click(object sender, RoutedEventArgs e)
        {
            _currentMode = "white";
            SliderTemp.Value = 6500;
            UpdateBulbGlow(TxtHexCode.Text, _isPowerOn);
        }
    }
}
