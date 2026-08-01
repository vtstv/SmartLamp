// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.IO;
using System.Windows;
using System.Windows.Interop;

namespace SmartLampApp
{
    public partial class MainWindow : Window
    {
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
                    Text = AppVersion.SystemTrayToolTip,
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
    }
}
