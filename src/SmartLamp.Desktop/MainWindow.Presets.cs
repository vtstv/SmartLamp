// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using SmartLamp.Desktop.Models;

namespace SmartLamp.Desktop
{
    public partial class MainWindow
    {
        private void InitializePresets()
        {
            BtnSavePreset.Click += (s, e) =>
            {
                int bright = (int)SliderBright.Value;
                int temp = (int)SliderTemp.Value;
                string hex = TxtHexCode.Text ?? "#00E5FF";

                TxtPresetSummary.Text = _currentMode == "colour"
                    ? $"Mode: COLOUR | Brightness: {bright}% | Color: {hex}"
                    : $"Mode: WHITE | Brightness: {bright}% | Temp: {temp}K";

                TxtPresetName.Text = $"My Preset {(_config.custom_presets.Count + 1)}";
                OverlaySavePreset.IsVisible = true;
            };

            BtnCancelSavePreset.Click += (s, e) => OverlaySavePreset.IsVisible = false;

            BtnConfirmSavePreset.Click += (s, e) =>
            {
                string name = TxtPresetName.Text?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(name)) name = "Custom Preset";

                var preset = new UserPreset
                {
                    Name = name,
                    Mode = _currentMode,
                    Brightness = (int)SliderBright.Value,
                    ColorTempK = (int)SliderTemp.Value,
                    ColorHex = TxtHexCode.Text ?? "#00E5FF"
                };

                _config.custom_presets.Add(preset);
                ConfigManager.Save(_config);
                OverlaySavePreset.IsVisible = false;
                RenderCustomPresets();
            };
        }

        private void RenderCustomPresets()
        {
            PnlCustomPresets.Children.Clear();

            if (_config.custom_presets.Count == 0)
            {
                var txt = new TextBlock
                {
                    Text = "No custom presets saved yet. Click '+ Save Current' to add one!",
                    FontSize = 11,
                    Foreground = Brush.Parse("#666688"),
                    Margin = new Thickness(4, 6, 4, 6),
                    FontStyle = FontStyle.Italic
                };
                PnlCustomPresets.Children.Add(txt);
                return;
            }

            foreach (var preset in _config.custom_presets)
            {
                var chip = CreatePresetChip(preset);
                PnlCustomPresets.Children.Add(chip);
            }
        }

        private Control CreatePresetChip(UserPreset preset)
        {
            var container = new Border
            {
                Background = Brush.Parse("#222236"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(3),
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto, Auto, Auto")
            };

            // Color Indicator Dot
            IBrush dotBrush = preset.Mode == "colour"
                ? Brush.Parse(preset.ColorHex.StartsWith("#") ? preset.ColorHex : "#" + preset.ColorHex)
                : new SolidColorBrush(KelvinToColor(preset.ColorTempK));

            var dot = new Border
            {
                Width = 10,
                Height = 10,
                CornerRadius = new CornerRadius(5),
                Background = dotBrush,
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
                FontWeight = FontWeight.Bold,
                Foreground = Brush.Parse("#FFFFFF"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            Grid.SetColumn(txt, 1);
            grid.Children.Add(txt);

            // Delete Button (✕)
            var btnDelete = new Button
            {
                Content = "✕",
                Width = 18,
                Height = 18,
                FontSize = 9,
                Foreground = Brush.Parse("#FF5252"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = new Cursor(StandardCursorType.Hand),
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0)
            };
            btnDelete.Click += (s, e) =>
            {
                _config.custom_presets.Remove(preset);
                ConfigManager.Save(_config);
                RenderCustomPresets();
            };
            Grid.SetColumn(btnDelete, 2);
            grid.Children.Add(btnDelete);

            container.Child = grid;

            container.PointerPressed += async (s, e) =>
            {
                if (e.GetCurrentPoint(container).Properties.IsLeftButtonPressed)
                {
                    await ApplyUserPresetAsync(preset);
                }
            };

            return container;
        }

        private async Task ApplyUserPresetAsync(UserPreset preset)
        {
            if (_isUpdatingUi) return;

            _isUpdatingUi = true;
            _currentMode = preset.Mode;
            SliderBright.Value = preset.Brightness;
            TxtBrightVal.Text = $"{preset.Brightness}%";
            SliderTemp.Value = preset.ColorTempK;
            TxtTempVal.Text = $"{preset.ColorTempK}K";
            if (!string.IsNullOrWhiteSpace(preset.ColorHex))
            {
                TxtHexCode.Text = preset.ColorHex;
            }
            _isUpdatingUi = false;

            if (preset.Mode == "colour")
            {
                await SetColorHexAsync(preset.ColorHex);
            }
            else
            {
                await SetSceneAsync("preset", preset.Brightness, preset.ColorTempK);
            }
        }
    }
}
