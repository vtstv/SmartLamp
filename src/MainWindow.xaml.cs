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
        private bool _isUpdatingUi = false;
        private bool _isKeyVisible = false;
        private bool _isAccKeyVisible = false;

        // Feature 1: Sleep Timer
        private System.Windows.Threading.DispatcherTimer? _sleepTimer;
        private int _sleepCountdownSeconds = 0;

        // Feature 4: System Tray Icon
        private System.Windows.Forms.NotifyIcon? _notifyIcon;

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
            UpdateInstallButtonState();

            PopulateDeviceSelector();
            RenderCustomPresets();
            _ = RefreshStatusAsync();
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
