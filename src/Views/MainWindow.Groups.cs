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
using System.Windows.Threading;

using SmartLampApp.Models;

using ColorConverter = System.Windows.Media.ColorConverter;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Orientation = System.Windows.Controls.Orientation;
using Brushes = System.Windows.Media.Brushes;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Color = System.Windows.Media.Color;

namespace SmartLampApp
{
    public partial class MainWindow : Window
    {
        private string _selectedEditingGroupId = "";
        private readonly Dictionary<string, DispatcherTimer> _groupSliderDebouncers = new();

        public void RenderGroupsTab()
        {
            PnlGroupsList.Children.Clear();

            if (_config.groups == null)
            {
                _config.groups = new List<DeviceGroup>();
            }

            if (_config.groups.Count == 0)
            {
                var emptyCard = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#161626")),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(16),
                    Margin = new Thickness(0, 0, 0, 8),
                    Child = new StackPanel
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Children =
                        {
                            new TextBlock { Text = "👥 No groups or rooms created yet", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA")), HorizontalAlignment = HorizontalAlignment.Center },
                            new TextBlock { Text = "Click '+ Add Room' above to group your lamps for simultaneous control.", FontSize = 11, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666688")), Margin = new Thickness(0, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Center }
                        }
                    }
                };
                PnlGroupsList.Children.Add(emptyCard);
                ResetGroupEditor();
                return;
            }

            // Sort by SortOrder
            _config.groups = _config.groups.OrderBy(g => g.SortOrder).ToList();

            for (int i = 0; i < _config.groups.Count; i++)
            {
                var grp = _config.groups[i];
                var card = CreateGroupCard(grp, i, _config.groups.Count);
                PnlGroupsList.Children.Add(card);
            }

            // Select active or first group in editor
            var toEdit = _config.groups.Find(g => g.Id == _selectedEditingGroupId) ?? _config.groups[0];
            PopulateGroupEditor(toEdit);
        }

        private UIElement CreateGroupCard(DeviceGroup grp, int index, int totalCount)
        {
            var card = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#161626")),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 8),
                BorderThickness = new Thickness(_selectedEditingGroupId == grp.Id ? 1.5 : 0),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00F0FF"))
            };

            var mainPanel = new StackPanel();

            // Row 1: Icon, Title, Lamp count pill, Quick Power Toggle
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock { Text = grp.Icon, FontSize = 16, Margin = new Thickness(0, 0, 6, 0) });
            titleStack.Children.Add(new TextBlock
            {
                Text = grp.Name,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            });

            int lampCount = grp.DeviceIds?.Count ?? 0;
            var countPill = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222236")),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            countPill.Child = new TextBlock
            {
                Text = $"{lampCount} {(lampCount == 1 ? "lamp" : "lamps")}",
                FontSize = 10,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00F0FF"))
            };
            titleStack.Children.Add(countPill);
            Grid.SetColumn(titleStack, 0);
            headerGrid.Children.Add(titleStack);

            // Quick Power Toggle Button
            var btnPower = new Button
            {
                Content = grp.MasterPowerOn ? "⚡ ON" : "⚡ OFF",
                Width = 75,
                Height = 26,
                Style = (Style)FindResource("GlassBtn"),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(grp.MasterPowerOn ? "#10B981" : "#222236")),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 11
            };
            btnPower.Click += async (s, e) =>
            {
                grp.MasterPowerOn = !grp.MasterPowerOn;
                btnPower.Content = grp.MasterPowerOn ? "⚡ ON" : "⚡ OFF";
                btnPower.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(grp.MasterPowerOn ? "#10B981" : "#222236"));

                var devices = _protocol.GetDevicesForGroup(grp);
                await _protocol.SetGroupPowerAsync(devices, grp.MasterPowerOn);
                ConfigManager.Save(_config);
            };
            Grid.SetColumn(btnPower, 1);
            headerGrid.Children.Add(btnPower);

            mainPanel.Children.Add(headerGrid);

            // Row 2: Quick Brightness Slider
            var sliderGrid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            sliderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sliderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var slider = new Slider
            {
                Minimum = 1,
                Maximum = 100,
                Value = grp.MasterBrightness > 0 ? grp.MasterBrightness : 80,
                VerticalAlignment = VerticalAlignment.Center
            };

            var txtBrightVal = new TextBlock
            {
                Text = $"{(int)slider.Value}%",
                FontSize = 10,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA")),
                Margin = new Thickness(8, 0, 0, 0),
                Width = 32,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            slider.ValueChanged += (s, e) =>
            {
                int val = (int)e.NewValue;
                txtBrightVal.Text = $"{val}%";
                grp.MasterBrightness = val;

                if (!_groupSliderDebouncers.TryGetValue(grp.Id, out var timer))
                {
                    timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                    timer.Tick += async (ts, te) =>
                    {
                        timer.Stop();
                        var currentGrp = _config.groups?.Find(g => g.Id == grp.Id);
                        if (currentGrp != null)
                        {
                            var devices = _protocol.GetDevicesForGroup(currentGrp);
                            await _protocol.SetGroupBrightnessAsync(devices, currentGrp.MasterBrightness, currentGrp.SyncMode, currentGrp.MasterBrightness);
                            ConfigManager.Save(_config);
                        }
                    };
                    _groupSliderDebouncers[grp.Id] = timer;
                }

                timer.Stop();
                timer.Start();
            };

            Grid.SetColumn(slider, 0);
            sliderGrid.Children.Add(slider);
            Grid.SetColumn(txtBrightVal, 1);
            sliderGrid.Children.Add(txtBrightVal);

            mainPanel.Children.Add(sliderGrid);

            // Row 3: Action Buttons (Edit, Move Up, Move Down)
            var actionGrid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btnEdit = new Button
            {
                Content = "✏️ Edit Room & Rules",
                Height = 22,
                Width = 140,
                HorizontalAlignment = HorizontalAlignment.Left,
                Style = (Style)FindResource("GlassBtn"),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222236")),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00F0FF")),
                FontSize = 10
            };
            btnEdit.Click += (s, e) =>
            {
                _selectedEditingGroupId = grp.Id;
                RenderGroupsTab();
            };
            Grid.SetColumn(btnEdit, 0);
            actionGrid.Children.Add(btnEdit);

            var sortStack = new StackPanel { Orientation = Orientation.Horizontal };
            Button CreateSortBtn(string text, RoutedEventHandler handler, Thickness margin = default)
            {
                var b = new Button { Content = text, Width = 24, Height = 22, Margin = margin, FontSize = 9, Style = (Style)FindResource("GlassBtn"), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222236")), Foreground = Brushes.White };
                b.Click += handler;
                return b;
            }

            if (index > 0)
            {
                sortStack.Children.Add(CreateSortBtn("▲", (s, e) =>
                {
                    var prev = _config.groups[index - 1];
                    (grp.SortOrder, prev.SortOrder) = (prev.SortOrder, grp.SortOrder);
                    ConfigManager.Save(_config);
                    RenderGroupsTab();
                }, new Thickness(0, 0, 4, 0)));
            }

            if (index < totalCount - 1)
            {
                sortStack.Children.Add(CreateSortBtn("▼", (s, e) =>
                {
                    var next = _config.groups[index + 1];
                    (grp.SortOrder, next.SortOrder) = (next.SortOrder, grp.SortOrder);
                    ConfigManager.Save(_config);
                    RenderGroupsTab();
                }));
            }

            Grid.SetColumn(sortStack, 1);
            actionGrid.Children.Add(sortStack);

            mainPanel.Children.Add(actionGrid);
            card.Child = mainPanel;

            // Clicking card body also selects it for editing
            card.MouseLeftButtonDown += (s, e) =>
            {
                if (_selectedEditingGroupId != grp.Id)
                {
                    _selectedEditingGroupId = grp.Id;
                    RenderGroupsTab();
                }
            };

            return card;
        }

        private void PopulateGroupEditor(DeviceGroup grp)
        {
            _selectedEditingGroupId = grp.Id;
            TxtGroupName.Text = grp.Name;

            // Set Icon
            foreach (ComboBoxItem item in CmbGroupIcon.Items)
            {
                if (item.Content.ToString() == grp.Icon)
                {
                    item.IsSelected = true;
                    break;
                }
            }

            // Set Sync Mode
            CmbGroupSyncMode.SelectedIndex = grp.SyncMode == GroupSyncMode.Proportional ? 1 : 0;
            ChkGroupTurnOffAll.IsChecked = grp.TurnOffAllOnPowerOff;
            ChkGroupTurnOnAll.IsChecked = grp.TurnOnAllOnPowerOn;

            // Render Devices Checklist
            PnlGroupDevicesList.Children.Clear();

            if (_config.devices == null || _config.devices.Count == 0)
            {
                PnlGroupDevicesList.Children.Add(new TextBlock
                {
                    Text = "No devices configured yet. Add lamps in Settings tab first.",
                    FontSize = 11,
                    FontStyle = FontStyles.Italic,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8888AA"))
                });
                return;
            }

            foreach (var dev in _config.devices)
            {
                bool isAssigned = grp.DeviceIds != null && grp.DeviceIds.Contains(dev.DevId);

                var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var chk = new CheckBox
                {
                    Content = $"💡 {dev.Name} ({dev.Ip})",
                    Foreground = Brushes.White,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    IsChecked = isAssigned,
                    Tag = dev.DevId
                };
                Grid.SetColumn(chk, 0);
                row.Children.Add(chk);

                var badge = new TextBlock
                {
                    Text = dev.last_power_on ? "🟢 ON" : "⚪ OFF",
                    FontSize = 10,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dev.last_power_on ? "#10B981" : "#8888AA")),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(badge, 1);
                row.Children.Add(badge);

                PnlGroupDevicesList.Children.Add(row);
            }
        }

        private void ResetGroupEditor()
        {
            _selectedEditingGroupId = "";
            TxtGroupName.Text = "New Room";
            CmbGroupIcon.SelectedIndex = 0;
            CmbGroupSyncMode.SelectedIndex = 0;
            ChkGroupTurnOffAll.IsChecked = true;
            ChkGroupTurnOnAll.IsChecked = true;
            PnlGroupDevicesList.Children.Clear();
        }

        private void BtnNewGroup_Click(object sender, RoutedEventArgs e)
        {
            var newGrp = new DeviceGroup
            {
                Name = $"Room {_config.groups.Count + 1}",
                Icon = "🛋️",
                SortOrder = _config.groups.Count,
                DeviceIds = _config.devices.Select(d => d.DevId).ToList()
            };
            _config.groups.Add(newGrp);
            _selectedEditingGroupId = newGrp.Id;
            ConfigManager.Save(_config);
            PopulateDeviceSelector();
            RenderGroupsTab();
            TxtGroupName.Focus();
            TxtGroupName.SelectAll();
        }

        private void BtnSaveGroup_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtGroupName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                System.Windows.MessageBox.Show("Please enter a valid Room / Group name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var grp = _config.groups.Find(g => g.Id == _selectedEditingGroupId);
            if (grp == null)
            {
                grp = new DeviceGroup
                {
                    Id = Guid.NewGuid().ToString(),
                    SortOrder = _config.groups.Count
                };
                _config.groups.Add(grp);
                _selectedEditingGroupId = grp.Id;
            }

            grp.Name = name;
            grp.Icon = (CmbGroupIcon.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "🛋️";
            grp.SyncMode = CmbGroupSyncMode.SelectedIndex == 1 ? GroupSyncMode.Proportional : GroupSyncMode.Synchronized;
            grp.TurnOffAllOnPowerOff = ChkGroupTurnOffAll.IsChecked == true;
            grp.TurnOnAllOnPowerOn = ChkGroupTurnOnAll.IsChecked == true;

            // Collect checked device IDs
            grp.DeviceIds.Clear();
            foreach (UIElement child in PnlGroupDevicesList.Children)
            {
                if (child is Grid row)
                {
                    var chk = row.Children.OfType<CheckBox>().FirstOrDefault();
                    if (chk != null && chk.IsChecked == true && chk.Tag is string devId)
                    {
                        grp.DeviceIds.Add(devId);
                    }
                }
            }

            ConfigManager.Save(_config);
            PopulateDeviceSelector();
            RenderGroupsTab();
            System.Windows.MessageBox.Show($"Room '{grp.Name}' saved successfully with {grp.DeviceIds.Count} lamp(s)!", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnDeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            var grp = _config.groups.Find(g => g.Id == _selectedEditingGroupId);
            if (grp == null) return;

            var res = System.Windows.MessageBox.Show($"Are you sure you want to delete room '{grp.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                _config.groups.Remove(grp);
                _selectedEditingGroupId = _config.groups.Count > 0 ? _config.groups[0].Id : "";
                ConfigManager.Save(_config);
                PopulateDeviceSelector();
                RenderGroupsTab();
            }
        }

        private async void BtnToggleGroupTest_Click(object sender, RoutedEventArgs e)
        {
            var grp = _config.groups.Find(g => g.Id == _selectedEditingGroupId);
            if (grp == null) return;

            grp.MasterPowerOn = !grp.MasterPowerOn;
            var devices = _protocol.GetDevicesForGroup(grp);
            await _protocol.SetGroupPowerAsync(devices, grp.MasterPowerOn);
            ConfigManager.Save(_config);
            RenderGroupsTab();
        }
    }
}
