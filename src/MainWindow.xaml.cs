// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

using SmartLampApp.Models;
using SmartLampApp.Services;

namespace SmartLampApp
{
    public partial class MainWindow : Window
    {
        private LampConfig _config;
        private TuyaProtocol _protocol;
        private GoogleHomeService _googleHomeService;
        private MqttService _mqttService;

        private bool _isPowerOn = false;
        private string _currentMode = "white";
        private int _uiUpdatingDepth = 0;
        private bool _isUpdatingUi
        {
            get => _uiUpdatingDepth > 0;
            set
            {
                if (value)
                    _uiUpdatingDepth++;
                else if (_uiUpdatingDepth > 0)
                    _uiUpdatingDepth--;
            }
        }
        private bool _isKeyVisible = false;
        private bool _isAccKeyVisible = false;
        private bool _isAddingNewDevice = false;

        // Feature 1: Sleep Timer
        private System.Windows.Threading.DispatcherTimer? _sleepTimer;
        private int _sleepCountdownSeconds = 0;

        // Feature 4: System Tray Icon
        private System.Windows.Forms.NotifyIcon? _notifyIcon;

        // Status Polling Timer
        private System.Windows.Threading.DispatcherTimer? _statusPollingTimer;
        private bool _isRefreshingStatus = false;

        private List<DiscoveredDevice> _discoveredList = new List<DiscoveredDevice>();
        private DateTime _lastUserCommandTime = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();
            this.Title = AppVersion.FullTitle;
            TxtAppTitle.Text = AppVersion.DisplayTitle;
            BtnAuthor.Content = AppVersion.CopyrightNotice;

            _config = ConfigManager.Load();
            _protocol = new TuyaProtocol(_config);

            _googleHomeService = new GoogleHomeService(_protocol);
            _googleHomeService.OnLogMessage += GoogleHomeService_OnLogMessage;
            if (_config.enable_google_home)
            {
                _googleHomeService.StartServer(8088);
            }

            _mqttService = new MqttService(_protocol);
            _mqttService.OnLogMessage += MqttService_OnLogMessage;
            if (_config.enable_mqtt)
            {
                _mqttService.StartBridge(1883);
            }

            InitializeSystemTray();
            InitializeSleepTimer();
            PopulateHotkeySelector();

            ChkAutostart.IsChecked = Services.InstallationService.IsAutostartEnabled() || _config.autostart;
            ChkCloseToTray.IsChecked = _config.close_to_tray;
            ChkEnableGoogle.IsChecked = _config.enable_google_home;
            ChkEnableMqtt.IsChecked = _config.enable_mqtt;
            NavGoogleHome.Visibility = _config.enable_google_home ? Visibility.Visible : Visibility.Collapsed;
            NavMqtt.Visibility = _config.enable_mqtt ? Visibility.Visible : Visibility.Collapsed;
            ChkTimerNotifications.IsChecked = _config.enable_timer_notifications;
            UpdateInstallButtonState();
            InitializeUpdateService();

            PopulateDeviceSelector();
            RenderCustomPresets();
            UpdatePowerButtonUi();

            InitializeLocalization();
            SetLanguage(_config.language ?? "en");
            if (_config.compact_mode)
            {
                SetCompactMode(true);
            }

            InitializeStatusPolling();
        }

        private void InitializeStatusPolling()
        {
            this.StateChanged += MainWindow_StateChanged;
            this.Activated += MainWindow_Activated;
            this.IsVisibleChanged += MainWindow_IsVisibleChanged;

            _statusPollingTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _statusPollingTimer.Tick += StatusPollingTimer_Tick;
            _statusPollingTimer.Start();
        }

        private void UpdateStatusPollingInterval()
        {
            if (_statusPollingTimer == null) return;
            bool isBackground = !this.IsVisible || this.WindowState == WindowState.Minimized;
            var targetInterval = isBackground ? TimeSpan.FromSeconds(15) : TimeSpan.FromSeconds(5);
            if (_statusPollingTimer.Interval != targetInterval)
            {
                _statusPollingTimer.Interval = targetInterval;
            }
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            UpdateStatusPollingInterval();
            if (this.WindowState != WindowState.Minimized && this.IsVisible)
            {
                _ = RefreshStatusAsync();
            }
        }

        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            UpdateStatusPollingInterval();
            if (this.WindowState != WindowState.Minimized && this.IsVisible)
            {
                _ = RefreshStatusAsync();
            }
        }

        private void MainWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateStatusPollingInterval();
            if (this.WindowState != WindowState.Minimized && this.IsVisible)
            {
                _ = RefreshStatusAsync();
            }
        }

        private async void StatusPollingTimer_Tick(object? sender, EventArgs e)
        {
            UpdateStatusPollingInterval();

            if ((DateTime.UtcNow - _lastUserCommandTime).TotalSeconds < 5.0)
            {
                return;
            }

            await RefreshStatusAsync();
        }

        protected override void OnClosed(EventArgs e)
        {
            _statusPollingTimer?.Stop();
            base.OnClosed(e);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            RegisterGlobalHotkey();

            if (App.IsAutostartLaunch)
            {
                this.WindowState = WindowState.Minimized;
                this.Hide();
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (_config.close_to_tray)
            {
                Hide();
            }
            else
            {
                ExitApplication();
            }
        }
    }
}
