// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

using Button = System.Windows.Controls.Button;

namespace SmartLampApp
{
    public partial class MainWindow : Window
    {
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
    }
}
