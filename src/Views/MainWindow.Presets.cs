// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        private void RenderCustomPresets()
        {
            PnlCustomPresets.Children.Clear();

            if (_config.custom_presets == null)
            {
                _config.custom_presets = new List<UserPreset>();
                ConfigManager.Save(_config);
            }

            if (_config.custom_presets.Count == 0)
            {
                var txtPlaceholder = new TextBlock
                {
                    Text = "No custom presets saved yet. Click '+ Save Current' to add one!",
                    FontSize = 11,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666688")),
                    Margin = new Thickness(4, 6, 4, 6),
                    FontStyle = FontStyles.Italic
                };
                PnlCustomPresets.Children.Add(txtPlaceholder);
                return;
            }

            foreach (var preset in _config.custom_presets)
            {
                var chip = CreatePresetChip(preset);
                PnlCustomPresets.Children.Add(chip);
            }
        }

        private UIElement CreatePresetChip(UserPreset preset)
        {
            var container = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222236")),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(3),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = $"Click to apply:\nMode: {preset.Mode}\nBrightness: {preset.Brightness}%\nTemp: {preset.ColorTempK}K\nHex: {preset.ColorHex}"
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Color Indicator
            string dotColor = preset.Mode == "colour" ? preset.ColorHex : "#FFB347";
            Color brushColor;
            try { brushColor = (Color)ColorConverter.ConvertFromString(dotColor); } catch { brushColor = Colors.Cyan; }

            var dot = new Border
            {
                Width = 10,
                Height = 10,
                CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush(brushColor),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            Grid.SetColumn(dot, 0);
            grid.Children.Add(dot);

            // Preset Name
            var txt = new TextBlock
            {
                Text = preset.Name,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            Grid.SetColumn(txt, 1);
            grid.Children.Add(txt);

            // Delete Button (✕)
            var btnDelete = new Button
            {
                Content = "✕",
                Width = 16,
                Height = 16,
                FontSize = 9,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5252")),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Delete this preset"
            };
            btnDelete.Click += (s, e) =>
            {
                e.Handled = true;
                _config.custom_presets.Remove(preset);
                ConfigManager.Save(_config);
                RenderCustomPresets();
            };
            Grid.SetColumn(btnDelete, 2);
            grid.Children.Add(btnDelete);

            container.Child = grid;

            // Apply preset on chip click
            container.MouseLeftButtonDown += async (s, e) =>
            {
                await ApplyUserPresetAsync(preset);
            };

            return container;
        }

        private async Task ApplyUserPresetAsync(UserPreset preset)
        {
            if (_isUpdatingUi) return;

            _isUpdatingUi = true;
            SliderBright.Value = preset.Brightness;
            TxtBrightVal.Text = $"{preset.Brightness}%";
            SliderTemp.Value = preset.ColorTempK;
            TxtTempVal.Text = $"{preset.ColorTempK}K";
            TxtHexCode.Text = preset.ColorHex;
            _isUpdatingUi = false;

            _isPowerOn = true;
            _lastUserCommandTime = DateTime.UtcNow;
            UpdatePowerButtonUi();

            await _protocol.SetPresetAsync(preset.Mode, preset.Brightness, preset.ColorTempK, preset.ColorHex);

            await Task.Delay(250);
            _ = RefreshStatusAsync();
        }

        private void BtnSavePreset_Click(object sender, RoutedEventArgs e)
        {
            string mode = !string.IsNullOrWhiteSpace(TxtHexCode.Text) && TxtHexCode.Text != "#00E5FF" ? "colour" : "white";
            int bright = (int)SliderBright.Value;
            int temp = (int)SliderTemp.Value;
            string hex = TxtHexCode.Text;

            TxtPresetSummary.Text = $"Mode: {mode.ToUpper()} | Brightness: {bright}% | Temp: {temp}K | Hex: {hex}";
            TxtPresetName.Text = $"My Preset {(_config.custom_presets.Count + 1)}";
            OverlaySavePreset.Visibility = Visibility.Visible;
        }

        private void BtnConfirmSavePreset_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtPresetName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name)) name = "Custom Preset";

            string mode = !string.IsNullOrWhiteSpace(TxtHexCode.Text) && TxtHexCode.Text != "#00E5FF" ? "colour" : "white";
            var preset = new UserPreset
            {
                Name = name,
                Mode = mode,
                Brightness = (int)SliderBright.Value,
                ColorTempK = (int)SliderTemp.Value,
                ColorHex = TxtHexCode.Text
            };

            _config.custom_presets.Add(preset);
            ConfigManager.Save(_config);
            OverlaySavePreset.Visibility = Visibility.Collapsed;
            RenderCustomPresets();
        }

        private void BtnCancelSavePreset_Click(object sender, RoutedEventArgs e)
        {
            OverlaySavePreset.Visibility = Visibility.Collapsed;
        }
    }
}
