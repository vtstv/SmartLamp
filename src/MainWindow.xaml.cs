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

        // Window-Focused Session Timers
        private System.Windows.Threading.DispatcherTimer? _statusPollingTimer;
        private System.Windows.Threading.DispatcherTimer? _heartbeatTimer;
        private System.Windows.Threading.DispatcherTimer? _trayGraceTimer;
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
            _protocol.OnStatusPushed += OnIncomingPushStatus;

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

            InitializeSessionLifecycle();
        }

        private void InitializeSessionLifecycle()
        {
            this.StateChanged += MainWindow_StateChanged;
            this.Activated += MainWindow_Activated;
            this.IsVisibleChanged += MainWindow_IsVisibleChanged;

            _heartbeatTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(15)
            };
            _heartbeatTimer.Tick += HeartbeatTimer_Tick;

            _trayGraceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(15)
            };
            _trayGraceTimer.Tick += TrayGraceTimer_Tick;

            _statusPollingTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _statusPollingTimer.Tick += StatusPollingTimer_Tick;

            if (this.IsVisible && this.WindowState != WindowState.Minimized)
            {
                OnWindowActivatedOrRestored();
            }
        }

        private async void OnWindowActivatedOrRestored()
        {
            if (!this.IsVisible || this.WindowState == WindowState.Minimized) return;

            _trayGraceTimer?.Stop();
            _statusPollingTimer?.Stop();

            bool ok = await _protocol.EnsureLocalSessionAsync();
            if (ok)
            {
                if (_heartbeatTimer != null && !_heartbeatTimer.IsEnabled)
                {
                    _heartbeatTimer.Start();
                }
            }
            else
            {
                if (_statusPollingTimer != null && !_statusPollingTimer.IsEnabled)
                {
                    _statusPollingTimer.Start();
                }
            }

            _ = RefreshStatusAsync(showConnecting: false);
        }

        private async void OnWindowMinimizedOrHidden()
        {
            _heartbeatTimer?.Stop();
            _statusPollingTimer?.Stop();
            _trayGraceTimer?.Stop();

            await _protocol.DisconnectLocalAsync();
        }

        private async void HeartbeatTimer_Tick(object? sender, EventArgs e)
        {
            if (!this.IsVisible || this.WindowState == WindowState.Minimized)
            {
                _heartbeatTimer?.Stop();
                return;
            }

            bool ok = await _protocol.SendHeartbeatAsync();
            if (!ok)
            {
                await _protocol.EnsureLocalSessionAsync();
            }
        }

        public void StartTrayGraceTimer()
        {
            _trayGraceTimer?.Stop();
            _trayGraceTimer?.Start();
        }

        private async void TrayGraceTimer_Tick(object? sender, EventArgs e)
        {
            _trayGraceTimer?.Stop();
            if (!this.IsVisible || this.WindowState == WindowState.Minimized)
            {
                await _protocol.DisconnectLocalAsync();
            }
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                OnWindowMinimizedOrHidden();
            }
            else if (this.IsVisible)
            {
                OnWindowActivatedOrRestored();
            }
        }

        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            if (this.WindowState != WindowState.Minimized && this.IsVisible)
            {
                OnWindowActivatedOrRestored();
            }
        }

        private void MainWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this.IsVisible && this.WindowState != WindowState.Minimized)
            {
                OnWindowActivatedOrRestored();
            }
            else if (!this.IsVisible)
            {
                OnWindowMinimizedOrHidden();
            }
        }

        private async void StatusPollingTimer_Tick(object? sender, EventArgs e)
        {
            if (!this.IsVisible || this.WindowState == WindowState.Minimized)
            {
                _statusPollingTimer?.Stop();
                return;
            }

            if ((DateTime.UtcNow - _lastUserCommandTime).TotalSeconds < 5.0)
            {
                return;
            }

            await RefreshStatusAsync();
        }

        protected override void OnClosed(EventArgs e)
        {
            _heartbeatTimer?.Stop();
            _trayGraceTimer?.Stop();
            _statusPollingTimer?.Stop();
            _ = _protocol.DisconnectLocalAsync();
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
