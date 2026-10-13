// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using SmartLamp.Desktop.Models;
using SmartLamp.Desktop.Services;
using TuyaNet.Native;

namespace SmartLamp.Desktop
{
    public partial class MainWindow : Window
    {
        private LampConfig _config = new();
        private TuyaDevice? _device;
        private bool _isPowerOn = false;
        private bool _isUpdatingUi = false;
        private bool _isCompactMode = false;
        private string _currentMode = "white";

        public MainWindow()
        {
            InitializeComponent();
            InitializeWindowChrome();
            InitializeLocalization();
            InitializeControls();
            InitializePresets();
            InitializeSettings();
            _ = InitializeAppAsync();
        }

        private void InitializeWindowChrome()
        {
            // Full TitleBar Drag & Controls
            TitleBar.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    BeginMoveDrag(e);
                }
            };
            BtnMinimize.Click += (s, e) => WindowState = WindowState.Minimized;
            BtnClose.Click += (s, e) => Close();

            // Compact TitleBar Drag & Controls
            CompactTitleBar.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    BeginMoveDrag(e);
                }
            };
            BtnCompactMinimize.Click += (s, e) => WindowState = WindowState.Minimized;
            BtnCompactClose.Click += (s, e) => Close();

            // Compact / Full Mode Switching
            BtnToggleModeFull.Click += (s, e) => SetCompactMode(true);
            BtnSwitchToCompactFooter.Click += (s, e) => SetCompactMode(true);
            BtnCompactExpand.Click += (s, e) => SetCompactMode(false);
            BtnCompactSwitchFull.Click += (s, e) => SetCompactMode(false);

            // Tab Navigation
            NavDash.Click += (s, e) => ShowTab(0);
            NavSettings.Click += (s, e) => ShowTab(1);
            NavGoogleHome.Click += (s, e) => ShowTab(2);
            NavMqtt.Click += (s, e) => ShowTab(3);

            // Refresh & Author
            BtnRefresh.Click += async (s, e) => await RefreshStatusAsync();
            BtnCompactRefresh.Click += async (s, e) => await RefreshStatusAsync();
            BtnAuthor.Click += (s, e) => OpenUrl("https://github.com/vtstv");
        }

        public void SetCompactMode(bool compact)
        {
            _isCompactMode = compact;
            _config.compact_mode = compact;
            ConfigManager.Save(_config);

            if (compact)
            {
                Width = 420;
                Height = 380;
                ViewFullRoot.IsVisible = false;
                ViewCompactRoot.IsVisible = true;
            }
            else
            {
                Width = 960;
                Height = 820;
                ViewFullRoot.IsVisible = true;
                ViewCompactRoot.IsVisible = false;
            }
        }

        private void ShowTab(int tabIndex)
        {
            ViewDashboard.IsVisible = tabIndex == 0;
            ViewSettings.IsVisible = tabIndex == 1;
            ViewGoogleHome.IsVisible = tabIndex == 2;
            ViewMqtt.IsVisible = tabIndex == 3;

            SetNavButton(NavDash, tabIndex == 0);
            SetNavButton(NavSettings, tabIndex == 1);
            SetNavButton(NavGoogleHome, tabIndex == 2);
            SetNavButton(NavMqtt, tabIndex == 3);

            if (tabIndex == 1)
            {
                PopulateDeviceForm();
            }
        }

        private static void SetNavButton(Button btn, bool isActive)
        {
            if (isActive)
            {
                btn.Background = Brush.Parse("#00F0FF");
                btn.Foreground = Brush.Parse("#000000");
                btn.FontWeight = FontWeight.Bold;
            }
            else
            {
                btn.Background = Brush.Parse("#1F1F30");
                btn.Foreground = Brush.Parse("#AAAAAA");
                btn.FontWeight = FontWeight.Normal;
            }
        }

        private async Task InitializeAppAsync()
        {
            _config = ConfigManager.Load();

            // Restore Language
            string lang = string.IsNullOrWhiteSpace(_config.language) ? "en" : _config.language;
            SetLanguage(lang);

            // Restore Integrations tab visibility
            NavGoogleHome.IsVisible = _config.enable_google_home;
            NavMqtt.IsVisible = _config.enable_mqtt;

            // Restore Device
            PopulateDeviceSelector();
            RenderCustomPresets();

            var active = GetActiveDevice();
            if (active != null)
            {
                TxtCompactDevice.Text = active.Name;
                CreateTuyaDevice(active);
                await RefreshStatusAsync();
            }
            else
            {
                TxtStatus.Text = LocalizationService.T("Offline");
                PillStatus.Background = Brush.Parse("#F59E0B");
                TxtDeviceInfo.Text = "Please configure device in Settings tab.";
            }

            // Restore Mode (Full or Compact)
            if (_config.compact_mode)
            {
                SetCompactMode(true);
            }

            // CLI flags override
            var args = Environment.GetCommandLineArgs();
            if (Array.Exists(args, a => a.Equals("--full", StringComparison.OrdinalIgnoreCase)))
            {
                SetCompactMode(false);
            }
            else if (Array.Exists(args, a => a.Equals("--compact", StringComparison.OrdinalIgnoreCase)))
            {
                SetCompactMode(true);
            }

            if (Array.Exists(args, a => a.Equals("--settings", StringComparison.OrdinalIgnoreCase)))
            {
                ShowTab(1);
            }
        }

        private DeviceInfo? GetActiveDevice()
        {
            if (_config.devices.Count == 0) return null;
            var dev = _config.devices.Find(d => d.DevId == _config.selected_dev_id);
            return dev ?? _config.devices[0];
        }

        private void CreateTuyaDevice(DeviceInfo dev)
        {
            TxtDeviceInfo.Text = $"IP: {dev.Ip}  |  ID: {dev.DevId}";
            TxtCompactDevice.Text = dev.Name;
            _device = new TuyaDevice(dev.Ip, dev.DevId, dev.LocalKey, dev.Version);
        }

        private void PopulateDeviceSelector()
        {
            _isUpdatingUi = true;
            CmbDeviceSelector.Items.Clear();

            if (_config.devices.Count == 0)
            {
                CmbDeviceSelector.Items.Add("➕ Add Device in Settings");
                CmbDeviceSelector.SelectedIndex = 0;
                TxtDeviceInfo.Text = "No devices configured yet. Switch to Settings tab to add one.";
                _isUpdatingUi = false;
                return;
            }

            foreach (var dev in _config.devices)
            {
                CmbDeviceSelector.Items.Add($"{dev.Name} ({dev.Ip})");
            }

            int selectedIndex = _config.devices.FindIndex(d => d.DevId == _config.selected_dev_id);
            CmbDeviceSelector.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;

            CmbDeviceSelector.SelectionChanged += async (s, e) =>
            {
                if (_isUpdatingUi) return;
                int idx = CmbDeviceSelector.SelectedIndex;
                if (idx >= 0 && idx < _config.devices.Count)
                {
                    _config.selected_dev_id = _config.devices[idx].DevId;
                    ConfigManager.Save(_config);
                    CreateTuyaDevice(_config.devices[idx]);
                    await RefreshStatusAsync();
                }
            };

            _isUpdatingUi = false;
        }

        private async Task RefreshStatusAsync()
        {
            if (_device == null) return;

            PillStatus.Background = Brush.Parse("#0284C7");
            PillCompactStatus.Background = Brush.Parse("#0284C7");
            TxtStatus.Text = LocalizationService.T("Connecting");
            TxtCompactStatus.Text = LocalizationService.T("Connecting");

            var sw = Stopwatch.StartNew();
            try
            {
                string statusJson = await _device.GetStatusJsonAsync();
                sw.Stop();
                string latencyMsg = $"⚡ {sw.ElapsedMilliseconds} ms (Local TCP)";
                TxtLatency.Text = latencyMsg;
                TxtCompactLatency.Text = latencyMsg;

                PillStatus.Background = Brush.Parse("#10B981");
                PillCompactStatus.Background = Brush.Parse("#10B981");
                TxtStatus.Text = LocalizationService.T("Online");
                TxtCompactStatus.Text = LocalizationService.T("Online");

                ParseAndUpdateUi(statusJson);
            }
            catch (Exception ex)
            {
                sw.Stop();
                PillStatus.Background = Brush.Parse("#FF1744");
                PillCompactStatus.Background = Brush.Parse("#FF1744");
                TxtStatus.Text = LocalizationService.T("Offline");
                TxtCompactStatus.Text = LocalizationService.T("Offline");
                TxtLatency.Text = $"Error: {ex.Message}";
                TxtCompactLatency.Text = $"Error: {ex.Message}";
                _isPowerOn = false;
                UpdatePowerButtonUi();
            }
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch { }
        }
    }
}
