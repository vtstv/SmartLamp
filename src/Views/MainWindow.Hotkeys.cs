// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

using SmartLampApp.Models;

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
    }
}
