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

        private async Task RefreshStatusAsync()
        {
            if (!_protocol.IsConfigured)
            {
                PillStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9100"));
                TxtStatus.Text = "⚠️ KEY REQUIRED";
                BtnPower.Content = "⚡ POWERED ON";
                BtnPower.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                return;
            }

            TxtStatus.Text = "Connecting...";
            var status = await Task.Run(() => _protocol.GetStatusAsync());

            if (!status.IsOnline)
            {
                PillStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1744"));
                TxtStatus.Text = "❌ OFFLINE";
                UpdateBulbGlow("#333344", false);
                return;
            }

            PillStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            TxtStatus.Text = "🟢 ONLINE";

            bool isUserActive = (DateTime.UtcNow - _lastUserCommandTime).TotalSeconds < 4.0;
            if (!isUserActive)
            {
                _isPowerOn = status.IsPowerOn;
                if (!string.IsNullOrWhiteSpace(status.Mode))
                {
                    _currentMode = status.Mode;
                }

                var active = _protocol.GetActiveDevice();
                if (status.Brightness > 0)
                {
                    _isUpdatingUi = true;
                    SliderBright.Value = status.Brightness;
                    TxtBrightVal.Text = $"{status.Brightness}%";
                    if (active != null) active.last_brightness = status.Brightness;
                    _config.last_brightness = status.Brightness;
                    _isUpdatingUi = false;
                }
                if (status.ColorTempK >= 2700)
                {
                    _isUpdatingUi = true;
                    SliderTemp.Value = status.ColorTempK;
                    TxtTempVal.Text = $"{status.ColorTempK}K";
                    if (active != null) active.last_temp = status.ColorTempK;
                    _config.last_temp = status.ColorTempK;
                    _isUpdatingUi = false;
                }
                if (!string.IsNullOrWhiteSpace(status.ColorHex) && status.Mode == "colour")
                {
                    TxtHexCode.Text = status.ColorHex;
                }
                ConfigManager.Save(_config);
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
                BtnPower.Content = "⚡ POWERED ON";
                BtnPower.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                UpdateBulbGlow(TxtHexCode.Text, true);
            }
            else
            {
                BtnPower.Content = "⚡ POWERED OFF";
                BtnPower.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1744"));
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
            }
            catch { }
        }

        private async void BtnPower_Click(object sender, RoutedEventArgs e)
        {
            _isPowerOn = !_isPowerOn;
            _lastUserCommandTime = DateTime.UtcNow;
            UpdatePowerButtonUi();

            if (CmbDeviceSelector.SelectedItem != null && CmbDeviceSelector.SelectedItem.ToString()!.Contains("Group"))
            {
                await _protocol.SetGroupPowerAsync(_config.devices, _isPowerOn);
                return;
            }

            bool success = await _protocol.SetPowerAsync(_isPowerOn);
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

                var active = _protocol.GetActiveDevice();
                if (active != null) active.last_brightness = val;
                _config.last_brightness = val;
                ConfigManager.Save(_config);

                if (CmbDeviceSelector.SelectedItem != null && CmbDeviceSelector.SelectedItem.ToString()!.Contains("Group"))
                {
                    await _protocol.SetGroupBrightnessAsync(_config.devices, val);
                }
                else
                {
                    await _protocol.SetBrightnessAsync(val);
                }
            };

            _tempDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _tempDebounceTimer.Tick += async (s, e) =>
            {
                _tempDebounceTimer.Stop();
                if (_isUpdatingUi || _protocol == null) return;
                int kVal = (int)SliderTemp.Value;

                _currentMode = "white";
                UpdateBulbGlow(TxtHexCode.Text, _isPowerOn);

                var active = _protocol.GetActiveDevice();
                if (active != null) active.last_temp = kVal;
                _config.last_temp = kVal;
                ConfigManager.Save(_config);

                await _protocol.SetColorTempAsync(kVal);
            };
        }

        private void SliderBright_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi || _protocol == null) return;
            int val = (int)e.NewValue;
            TxtBrightVal.Text = $"{val}%";

            if (_brightDebounceTimer == null) InitializeDebouncers();
            _brightDebounceTimer?.Stop();
            _brightDebounceTimer?.Start();
        }

        private void SliderTemp_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi || _protocol == null) return;
            int kVal = (int)e.NewValue;
            TxtTempVal.Text = $"{kVal}K";

            _currentMode = "white";
            UpdateBulbGlow(TxtHexCode.Text, _isPowerOn);

            if (_tempDebounceTimer == null) InitializeDebouncers();
            _tempDebounceTimer?.Stop();
            _tempDebounceTimer?.Start();
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

        private async void BtnColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                _currentMode = "colour";
                TxtHexCode.Text = hex;
                UpdateBulbGlow(hex, _isPowerOn);

                if (CmbDeviceSelector.SelectedItem != null && CmbDeviceSelector.SelectedItem.ToString()!.Contains("Group"))
                {
                    await _protocol.SetGroupColorHexAsync(_config.devices, hex);
                    return;
                }
                await _protocol.SetColorHexAsync(hex);
            }
        }

        private async void BtnColorPicker_Click(object sender, RoutedEventArgs e)
        {
            var pickerWin = new ColorPickerWindow(TxtHexCode.Text)
            {
                Owner = this
            };

            if (pickerWin.ShowDialog() == true)
            {
                string hex = pickerWin.SelectedHex;
                _currentMode = "colour";
                TxtHexCode.Text = hex;
                UpdateBulbGlow(hex, _isPowerOn);

                if (CmbDeviceSelector.SelectedItem != null && CmbDeviceSelector.SelectedItem.ToString()!.Contains("Group"))
                {
                    await _protocol.SetGroupColorHexAsync(_config.devices, hex);
                    return;
                }
                await _protocol.SetColorHexAsync(hex);
            }
        }

        private async void BtnApplyHex_Click(object sender, RoutedEventArgs e)
        {
            string hex = TxtHexCode.Text.Trim();
            _currentMode = "colour";
            UpdateBulbGlow(hex, _isPowerOn);

            if (CmbDeviceSelector.SelectedItem != null && CmbDeviceSelector.SelectedItem.ToString()!.Contains("Group"))
            {
                await _protocol.SetGroupColorHexAsync(_config.devices, hex);
                return;
            }
            await _protocol.SetColorHexAsync(hex);
        }

        private async void BtnScene_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string sceneKey)
            {
                await _protocol.SetSceneAsync(sceneKey);
            }
        }
    }
}
