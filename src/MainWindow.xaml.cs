using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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
        // Win32 Global Hotkey Imports
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9001;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const int WM_HOTKEY = 0x0312;

        private LampConfig _config;
        private TuyaProtocol _protocol;
        private GoogleHomeService _googleHomeService;
        private MqttService _mqttService;

        private bool _isPowerOn = false;
        private bool _isUpdatingUi = false;
        private bool _isKeyVisible = false;
        private bool _isAccKeyVisible = false;

        // Feature 1: Sleep Timer
        private DispatcherTimer? _sleepTimer;
        private int _sleepCountdownSeconds = 0;

        // Feature 4: System Tray Icon
        private System.Windows.Forms.NotifyIcon? _notifyIcon;

        private List<DiscoveredDevice> _discoveredList = new List<DiscoveredDevice>();

        public MainWindow()
        {
            InitializeComponent();
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

            PopulateDeviceSelector();
            _ = RefreshStatusAsync();
        }

        private void MqttService_OnLogMessage(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                TxtMqttLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
                TxtMqttLog.ScrollToEnd();
            });
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            RegisterGlobalHotkey();
        }

        private void PopulateHotkeySelector()
        {
            CmbHotkeyKey.Items.Clear();
            for (char c = 'A'; c <= 'Z'; c++)
            {
                CmbHotkeyKey.Items.Add(c.ToString());
            }
            for (int f = 1; f <= 12; f++)
            {
                CmbHotkeyKey.Items.Add($"F{f}");
            }
            CmbHotkeyKey.Items.Add("Space");

            ChkCtrl.IsChecked = _config.hotkey_mod_ctrl;
            ChkAlt.IsChecked = _config.hotkey_mod_alt;
            ChkShift.IsChecked = _config.hotkey_mod_shift;

            string keyStr = string.IsNullOrWhiteSpace(_config.hotkey_key) ? "L" : _config.hotkey_key;
            CmbHotkeyKey.SelectedItem = keyStr;
        }

        private void RegisterGlobalHotkey()
        {
            try
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                if (handle == IntPtr.Zero) return;

                UnregisterHotKey(handle, HOTKEY_ID);

                uint modifiers = 0;
                if (_config.hotkey_mod_ctrl) modifiers |= MOD_CONTROL;
                if (_config.hotkey_mod_alt) modifiers |= MOD_ALT;
                if (_config.hotkey_mod_shift) modifiers |= MOD_SHIFT;

                uint vk = GetVirtualKeyFromString(_config.hotkey_key);
                bool success = RegisterHotKey(handle, HOTKEY_ID, modifiers, vk);

                HwndSource source = HwndSource.FromHwnd(handle);
                source?.RemoveHook(HwndHook);
                source?.AddHook(HwndHook);

                string hotkeyDisplay = GetHotkeyDisplayText();
                TxtFooterHotkey.Text = $"⌨️ Hotkey: {hotkeyDisplay}";
            }
            catch { }
        }

        private uint GetVirtualKeyFromString(string keyStr)
        {
            if (string.IsNullOrEmpty(keyStr)) return 0x4C; // 'L'
            if (keyStr.Length == 1 && char.IsLetter(keyStr[0])) return (uint)char.ToUpper(keyStr[0]);
            if (keyStr.StartsWith("F") && int.TryParse(keyStr.Substring(1), out int fNum)) return (uint)(0x6F + fNum);
            if (keyStr == "Space") return 0x20;
            return 0x4C;
        }

        private string GetHotkeyDisplayText()
        {
            var parts = new List<string>();
            if (_config.hotkey_mod_ctrl) parts.Add("Ctrl");
            if (_config.hotkey_mod_alt) parts.Add("Alt");
            if (_config.hotkey_mod_shift) parts.Add("Shift");
            parts.Add(_config.hotkey_key);
            return string.Join("+", parts);
        }

        private void BtnSaveHotkey_Click(object sender, RoutedEventArgs e)
        {
            _config.hotkey_mod_ctrl = ChkCtrl.IsChecked == true;
            _config.hotkey_mod_alt = ChkAlt.IsChecked == true;
            _config.hotkey_mod_shift = ChkShift.IsChecked == true;
            _config.hotkey_key = CmbHotkeyKey.SelectedItem?.ToString() ?? "L";

            ConfigManager.Save(_config);
            RegisterGlobalHotkey();

            System.Windows.MessageBox.Show($"Custom Hotkey saved!\n\nHotkey: {GetHotkeyDisplayText()}", "Hotkey Updated", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                _ = TogglePowerFromHotkeyAsync();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private async Task TogglePowerFromHotkeyAsync()
        {
            _isPowerOn = !_isPowerOn;
            UpdatePowerButtonUi();
            await _protocol.SetPowerAsync(_isPowerOn);
            ShowTrayNotification($"SmartLamp ({GetHotkeyDisplayText()})", _isPowerOn ? "⚡ Lamp Powered ON" : "⚡ Lamp Powered OFF");
        }

        private void InitializeSystemTray()
        {
            try
            {
                System.Drawing.Icon trayIcon = System.Drawing.SystemIcons.Application;
                try
                {
                    Uri iconUri = new Uri("pack://application:,,,/Assets/app_icon.ico", UriKind.RelativeOrAbsolute);
                    var resourceStream = System.Windows.Application.GetResourceStream(iconUri);
                    if (resourceStream != null && resourceStream.Stream != null)
                    {
                        trayIcon = new System.Drawing.Icon(resourceStream.Stream);
                    }
                    else
                    {
                        string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
                        if (File.Exists(localPath))
                        {
                            trayIcon = new System.Drawing.Icon(localPath);
                        }
                    }
                }
                catch { }

                _notifyIcon = new System.Windows.Forms.NotifyIcon
                {
                    Icon = trayIcon,
                    Text = "SmartLamp Studio by Murr",
                    Visible = true
                };

                var contextMenu = new System.Windows.Forms.ContextMenuStrip();
                contextMenu.Items.Add("⚡ Toggle Power", null, async (s, e) => await TogglePowerFromHotkeyAsync());
                contextMenu.Items.Add("🌙 Night Mode (2700K)", null, async (s, e) => await _protocol.SetColorTempAsync(2700));
                contextMenu.Items.Add("📖 Read Mode (4000K)", null, async (s, e) => await _protocol.SetColorTempAsync(4000));
                contextMenu.Items.Add("-");
                contextMenu.Items.Add("💡 Show Dashboard", null, (s, e) => RestoreFromTray());
                contextMenu.Items.Add("✕ Exit SmartLamp", null, (s, e) => ExitApplication());

                _notifyIcon.ContextMenuStrip = contextMenu;
                _notifyIcon.DoubleClick += (s, e) => RestoreFromTray();
            }
            catch { }
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ExitApplication()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            try
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                UnregisterHotKey(handle, HOTKEY_ID);
            }
            catch { }
            _googleHomeService?.StopServer();
            _mqttService?.StopBridge();
            System.Windows.Application.Current.Shutdown();
        }

        private void ShowTrayNotification(string title, string msg)
        {
            try
            {
                _notifyIcon?.ShowBalloonTip(2000, title, msg, System.Windows.Forms.ToolTipIcon.Info);
            }
            catch { }
        }

        private void InitializeSleepTimer()
        {
            _sleepTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _sleepTimer.Tick += SleepTimer_Tick;
        }

        private void BtnSetTimer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string minsStr && int.TryParse(minsStr, out int mins))
            {
                StartSleepTimer(mins);
            }
        }

        private void BtnSetCustomTimer_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtCustomTimerMins.Text.Trim(), out int mins) && mins > 0)
            {
                StartSleepTimer(mins);
            }
            else
            {
                System.Windows.MessageBox.Show("Please enter a valid number of minutes.", "Timer Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void StartSleepTimer(int mins)
        {
            _sleepCountdownSeconds = mins * 60;
            _sleepTimer?.Start();
            UpdateSleepTimerText();
            ShowTrayNotification("Sleep Timer Set", $"Auto-turn off set for {mins} minutes.");
        }

        private void BtnCancelTimer_Click(object sender, RoutedEventArgs e)
        {
            _sleepTimer?.Stop();
            _sleepCountdownSeconds = 0;
            TxtTimerStatus.Text = "Off";
        }

        private async void SleepTimer_Tick(object? sender, EventArgs e)
        {
            if (_sleepCountdownSeconds > 0)
            {
                _sleepCountdownSeconds--;
                UpdateSleepTimerText();
            }
            else
            {
                _sleepTimer?.Stop();
                TxtTimerStatus.Text = "Off";
                _isPowerOn = false;
                UpdatePowerButtonUi();
                await _protocol.SetPowerAsync(false);
                ShowTrayNotification("Sleep Timer Finished", "💤 Smart Lamp powered off automatically.");
            }
        }

        private void UpdateSleepTimerText()
        {
            int m = _sleepCountdownSeconds / 60;
            int s = _sleepCountdownSeconds % 60;
            TxtTimerStatus.Text = $"{m:D2}m {s:D2}s";
        }

        private void GoogleHomeService_OnLogMessage(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                TxtGoogleLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
                TxtGoogleLog.ScrollToEnd();
            });
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

        private void ChkConfigToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;

            _config.close_to_tray = ChkCloseToTray.IsChecked == true;
            
            bool prevGoogle = _config.enable_google_home;
            _config.enable_google_home = ChkEnableGoogle.IsChecked == true;
            
            bool prevMqtt = _config.enable_mqtt;
            _config.enable_mqtt = ChkEnableMqtt.IsChecked == true;

            ConfigManager.Save(_config);

            NavGoogleHome.Visibility = _config.enable_google_home ? Visibility.Visible : Visibility.Collapsed;
            NavMqtt.Visibility = _config.enable_mqtt ? Visibility.Visible : Visibility.Collapsed;

            // Handle MQTT Bridge start/stop
            if (_config.enable_mqtt && !prevMqtt)
            {
                _mqttService.StartBridge(1883);
                System.Windows.MessageBox.Show("Home Assistant MQTT Bridge Enabled & Started on Port 1883.", "Integration Enabled", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (!_config.enable_mqtt && prevMqtt)
            {
                _mqttService.StopBridge();
                System.Windows.MessageBox.Show("Home Assistant MQTT Bridge Disabled & Stopped.", "Integration Disabled", MessageBoxButton.OK, MessageBoxImage.Information);
                if (ViewMqtt.Visibility == Visibility.Visible) NavDash_Click(null, null);
            }

            // Handle Google Home start/stop
            if (_config.enable_google_home && !prevGoogle)
            {
                _googleHomeService.StartServer(8088);
                System.Windows.MessageBox.Show("Google Smart Home Bridge Enabled & Started on Port 8088.", "Integration Enabled", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (!_config.enable_google_home && prevGoogle)
            {
                _googleHomeService.StopServer();
                System.Windows.MessageBox.Show("Google Smart Home Bridge Disabled & Stopped.", "Integration Disabled", MessageBoxButton.OK, MessageBoxImage.Information);
                if (ViewGoogleHome.Visibility == Visibility.Visible) NavDash_Click(null, null);
            }
        }

        private void NavDash_Click(object sender, RoutedEventArgs e)
        {
            ViewDashboard.Visibility = Visibility.Visible;
            ViewSettings.Visibility = Visibility.Collapsed;
            ViewGoogleHome.Visibility = Visibility.Collapsed;
            ViewMqtt.Visibility = Visibility.Collapsed;

            SetNavActive(NavDash);
        }

        private void NavSettings_Click(object sender, RoutedEventArgs e)
        {
            ViewDashboard.Visibility = Visibility.Collapsed;
            ViewSettings.Visibility = Visibility.Visible;
            ViewGoogleHome.Visibility = Visibility.Collapsed;
            ViewMqtt.Visibility = Visibility.Collapsed;

            SetNavActive(NavSettings);
        }

        private void NavGoogleHome_Click(object sender, RoutedEventArgs e)
        {
            ViewDashboard.Visibility = Visibility.Collapsed;
            ViewSettings.Visibility = Visibility.Collapsed;
            ViewGoogleHome.Visibility = Visibility.Visible;
            ViewMqtt.Visibility = Visibility.Collapsed;

            SetNavActive(NavGoogleHome);
        }

        private void NavMqtt_Click(object sender, RoutedEventArgs e)
        {
            ViewDashboard.Visibility = Visibility.Collapsed;
            ViewSettings.Visibility = Visibility.Collapsed;
            ViewGoogleHome.Visibility = Visibility.Collapsed;
            ViewMqtt.Visibility = Visibility.Visible;

            SetNavActive(NavMqtt);
        }

        private void SetNavActive(Button activeBtn)
        {
            NavDash.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F1F30"));
            NavDash.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA"));

            NavSettings.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F1F30"));
            NavSettings.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA"));

            NavGoogleHome.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F1F30"));
            NavGoogleHome.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA"));

            NavMqtt.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F1F30"));
            NavMqtt.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA"));

            activeBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00F0FF"));
            activeBtn.Foreground = new SolidColorBrush(Colors.Black);
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

            if (_config.devices.Count > 1)
            {
                CmbDeviceSelector.Items.Add("👥 All Devices (Group Control)");
            }

            int selectedIndex = _config.devices.FindIndex(d => d.DevId == _config.selected_dev_id);
            CmbDeviceSelector.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;

            PopulateDeviceForm();
            _isUpdatingUi = false;
        }

        private void PopulateDeviceForm()
        {
            var active = _protocol.GetActiveDevice();
            if (active != null)
            {
                TxtName.Text = active.Name;
                TxtIp.Text = active.Ip;
                TxtDevId.Text = active.DevId;

                PassLocalKey.Password = active.LocalKey;
                TxtLocalKeyVisible.Text = active.LocalKey;

                TxtGroup.Text = active.GroupName;
                TxtDeviceInfo.Text = $"IP: {active.Ip}  |  ID: {active.DevId}";

                // Restore saved brightness, temperature, and color hex
                int bVal = active.last_brightness > 0 ? active.last_brightness : (_config.last_brightness > 0 ? _config.last_brightness : 50);
                SliderBright.Value = bVal;
                TxtBrightVal.Text = $"{bVal}%";

                int tVal = active.last_temp >= 2700 ? active.last_temp : (_config.last_temp >= 2700 ? _config.last_temp : 4000);
                SliderTemp.Value = tVal;
                TxtTempVal.Text = $"{tVal}K";

                string hexVal = !string.IsNullOrWhiteSpace(active.last_color_hex) ? active.last_color_hex : (_config.last_color_hex ?? "#00E5FF");
                TxtHexCode.Text = hexVal;
            }

            TxtAccId.Text = _config.access_id;
            PassAccKey.Password = _config.access_key;
            TxtAccKeyVisible.Text = _config.access_key;

            ChkCloseToTray.IsChecked = _config.close_to_tray;
            ChkEnableGoogle.IsChecked = _config.enable_google_home;
            ChkEnableMqtt.IsChecked = _config.enable_mqtt;

            NavGoogleHome.Visibility = _config.enable_google_home ? Visibility.Visible : Visibility.Collapsed;
            NavMqtt.Visibility = _config.enable_mqtt ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnToggleKeyVisibility_Click(object sender, RoutedEventArgs e)
        {
            _isKeyVisible = !_isKeyVisible;
            if (_isKeyVisible)
            {
                TxtLocalKeyVisible.Text = PassLocalKey.Password;
                PassLocalKey.Visibility = Visibility.Collapsed;
                TxtLocalKeyVisible.Visibility = Visibility.Visible;
                BtnToggleKeyVisibility.Content = "🙈";
            }
            else
            {
                PassLocalKey.Password = TxtLocalKeyVisible.Text;
                TxtLocalKeyVisible.Visibility = Visibility.Collapsed;
                PassLocalKey.Visibility = Visibility.Visible;
                BtnToggleKeyVisibility.Content = "👁️";
            }
        }

        private void BtnToggleAccKeyVisibility_Click(object sender, RoutedEventArgs e)
        {
            _isAccKeyVisible = !_isAccKeyVisible;
            if (_isAccKeyVisible)
            {
                TxtAccKeyVisible.Text = PassAccKey.Password;
                PassAccKey.Visibility = Visibility.Collapsed;
                TxtAccKeyVisible.Visibility = Visibility.Visible;
                BtnToggleAccKeyVisibility.Content = "🙈";
            }
            else
            {
                PassAccKey.Password = TxtAccKeyVisible.Text;
                TxtAccKeyVisible.Visibility = Visibility.Collapsed;
                PassAccKey.Visibility = Visibility.Visible;
                BtnToggleAccKeyVisibility.Content = "👁️";
            }
        }

        private string GetEnteredLocalKey()
        {
            return _isKeyVisible ? TxtLocalKeyVisible.Text.Trim() : PassLocalKey.Password.Trim();
        }

        private string GetEnteredAccessKey()
        {
            return _isAccKeyVisible ? TxtAccKeyVisible.Text.Trim() : PassAccKey.Password.Trim();
        }

        private void CmbDeviceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || CmbDeviceSelector.SelectedIndex < 0) return;

            int idx = CmbDeviceSelector.SelectedIndex;
            if (idx < _config.devices.Count)
            {
                _config.selected_dev_id = _config.devices[idx].DevId;
                ConfigManager.Save(_config);
                _protocol.UpdateConfig(_config);
                PopulateDeviceForm();
                _ = RefreshStatusAsync();
            }
        }

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
            var status = await _protocol.GetStatusAsync();

            if (!status.IsOnline)
            {
                PillStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1744"));
                TxtStatus.Text = "❌ OFFLINE";
                UpdateBulbGlow("#333344", false);
                return;
            }

            PillStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            TxtStatus.Text = "🟢 ONLINE";

            _isPowerOn = status.IsPowerOn;
            UpdatePowerButtonUi();

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
            ConfigManager.Save(_config);
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
                var c = (Color)ColorConverter.ConvertFromString(hexCode);
                BulbGlowFrame.Background = new SolidColorBrush(c);
            }
            catch { }
        }

        private async void BtnPower_Click(object sender, RoutedEventArgs e)
        {
            _isPowerOn = !_isPowerOn;
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

        private DispatcherTimer? _brightDebounceTimer;
        private DispatcherTimer? _tempDebounceTimer;

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

            if (_tempDebounceTimer == null) InitializeDebouncers();
            _tempDebounceTimer?.Stop();
            _tempDebounceTimer?.Start();
        }

        private void BtnB25_Click(object sender, RoutedEventArgs e) => SliderBright.Value = 25;
        private void BtnB50_Click(object sender, RoutedEventArgs e) => SliderBright.Value = 50;
        private void BtnB75_Click(object sender, RoutedEventArgs e) => SliderBright.Value = 75;
        private void BtnB100_Click(object sender, RoutedEventArgs e) => SliderBright.Value = 100;

        private void BtnT2700_Click(object sender, RoutedEventArgs e) => SliderTemp.Value = 2700;
        private void BtnT4000_Click(object sender, RoutedEventArgs e) => SliderTemp.Value = 4000;
        private void BtnT6500_Click(object sender, RoutedEventArgs e) => SliderTemp.Value = 6500;

        private async void BtnColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
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

        private void BtnSaveDevice_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtName.Text.Trim();
            string ip = TxtIp.Text.Trim();
            string devId = TxtDevId.Text.Trim();
            string key = GetEnteredLocalKey();
            string group = TxtGroup.Text.Trim();

            if (string.IsNullOrWhiteSpace(devId))
            {
                System.Windows.MessageBox.Show("Please enter a valid Device ID.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var existing = _config.devices.Find(d => d.DevId == devId);
            if (existing != null)
            {
                existing.Name = string.IsNullOrWhiteSpace(name) ? "Smart Lamp" : name;
                existing.Ip = ip;
                existing.LocalKey = key;
                existing.GroupName = string.IsNullOrWhiteSpace(group) ? "Default Group" : group;
            }
            else
            {
                _config.devices.Add(new DeviceInfo
                {
                    Name = string.IsNullOrWhiteSpace(name) ? "Smart Lamp" : name,
                    Ip = ip,
                    DevId = devId,
                    LocalKey = key,
                    GroupName = string.IsNullOrWhiteSpace(group) ? "Default Group" : group
                });
            }

            _config.selected_dev_id = devId;
            ConfigManager.Save(_config);
            _protocol.UpdateConfig(_config);

            PopulateDeviceSelector();
            _ = RefreshStatusAsync();
            System.Windows.MessageBox.Show("Device saved successfully!", "Device Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnDeleteDevice_Click(object sender, RoutedEventArgs e)
        {
            var active = _protocol.GetActiveDevice();
            if (active == null || string.IsNullOrWhiteSpace(active.DevId))
            {
                System.Windows.MessageBox.Show("No active device selected to delete.", "Delete Device", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = System.Windows.MessageBox.Show($"Are you sure you want to delete '{active.Name}' ({active.Ip})?", "Delete Device", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _config.devices.RemoveAll(d => d.DevId == active.DevId);
                _config.selected_dev_id = _config.devices.Count > 0 ? _config.devices[0].DevId : "";
                ConfigManager.Save(_config);
                _protocol.UpdateConfig(_config);

                PopulateDeviceSelector();
                _ = RefreshStatusAsync();
                System.Windows.MessageBox.Show("Device deleted successfully.", "Device Removed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void BtnAutoDiscover_Click(object sender, RoutedEventArgs e)
        {
            BtnAutoDiscover.Content = "Scanning Wi-Fi subnet...";
            BtnAutoDiscover.IsEnabled = false;
            LstDiscovered.Items.Clear();

            _discoveredList = await _protocol.AutoDiscoverDevicesAsync();

            BtnAutoDiscover.Content = "⚡ Scan Local Wi-Fi Subnet";
            BtnAutoDiscover.IsEnabled = true;

            if (_discoveredList.Count == 0)
            {
                LstDiscovered.Items.Add("No Tuya devices found on local Wi-Fi.");
            }
            else
            {
                foreach (var dev in _discoveredList)
                {
                    LstDiscovered.Items.Add($"💡 {dev.ProductName} - IP: {dev.Ip} (ID: {dev.DevId})");
                }
            }
        }

        private void LstDiscovered_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int idx = LstDiscovered.SelectedIndex;
            if (idx >= 0 && idx < _discoveredList.Count)
            {
                var sel = _discoveredList[idx];
                TxtName.Text = sel.ProductName;
                TxtIp.Text = sel.Ip;
                TxtDevId.Text = sel.DevId;
            }
        }

        private async void BtnCloudFetch_Click(object sender, RoutedEventArgs e)
        {
            string accId = TxtAccId.Text.Trim();
            string accKey = GetEnteredAccessKey();

            if (string.IsNullOrWhiteSpace(accId) || string.IsNullOrWhiteSpace(accKey))
            {
                System.Windows.MessageBox.Show("Please enter your Tuya Access ID and Access Key (Secret).", "Missing Credentials", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnCloudFetch.Content = "Fetching keys from Tuya Cloud...";
            BtnCloudFetch.IsEnabled = false;

            var (key, err) = await _protocol.AutoFetchKeyFromCloudAsync(accId, accKey, _config.region);

            BtnCloudFetch.Content = "⚡ Fetch Key from Cloud";
            BtnCloudFetch.IsEnabled = true;

            if (!string.IsNullOrEmpty(key))
            {
                PassLocalKey.Password = key;
                TxtLocalKeyVisible.Text = key;
                _config.access_id = accId;
                _config.access_key = accKey;

                ConfigManager.Save(_config);
                System.Windows.MessageBox.Show($"Retrieved Local Key from Tuya Cloud!\n\nLocal Key: {key}", "Cloud Sync Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show($"Cloud Sync Error: {err}", "Tuya Cloud Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnStartGoogleBridge_Click(object sender, RoutedEventArgs e)
        {
            _googleHomeService.StopServer();
            _googleHomeService.StartServer(8088);
            System.Windows.MessageBox.Show("Google Smart Home Local Bridge restarted on port 8088!", "Google Home", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnGoogleTestOn_Click(object sender, RoutedEventArgs e)
        {
            _isPowerOn = true;
            UpdatePowerButtonUi();
            await _protocol.SetPowerAsync(true);
            GoogleHomeService_OnLogMessage("Simulated Google Assistant Voice Command: 'Hey Google, turn on the lamp'");
        }

        private async void BtnGoogleTestOff_Click(object sender, RoutedEventArgs e)
        {
            _isPowerOn = false;
            UpdatePowerButtonUi();
            await _protocol.SetPowerAsync(false);
            GoogleHomeService_OnLogMessage("Simulated Google Assistant Voice Command: 'Hey Google, turn off the lamp'");
        }

        private void BtnStartMqttBridge_Click(object sender, RoutedEventArgs e)
        {
            _mqttService.StopBridge();
            _mqttService.StartBridge(1883);
            System.Windows.MessageBox.Show("Home Assistant & MQTT REST Bridge restarted on port 1883!", "MQTT Bridge", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnCopyMqttDiscovery_Click(object sender, RoutedEventArgs e)
        {
            string discoveryPayload = _mqttService.GetHomeAssistantDiscoveryPayload();
            System.Windows.Clipboard.SetText(discoveryPayload);
            System.Windows.MessageBox.Show("Home Assistant Auto-Discovery JSON copied to Clipboard!", "HA Discovery", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnMqttTestOn_Click(object sender, RoutedEventArgs e)
        {
            _isPowerOn = true;
            UpdatePowerButtonUi();
            await _protocol.SetPowerAsync(true);
            MqttService_OnLogMessage("Simulated Home Assistant Command: Turn ON");
        }

        private async void BtnMqttTestOff_Click(object sender, RoutedEventArgs e)
        {
            _isPowerOn = false;
            UpdatePowerButtonUi();
            await _protocol.SetPowerAsync(false);
            MqttService_OnLogMessage("Simulated Home Assistant Command: Turn OFF");
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshStatusAsync();
        }

        private void BtnAuthor_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/vtstv",
                UseShellExecute = true
            });
        }
    }
}
