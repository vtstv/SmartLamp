// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

using SmartLampApp.Models;
using SmartLampApp.Services;

using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using MessageBox = System.Windows.MessageBox;

namespace SmartLampApp
{
    public partial class MainWindow : Window
    {
        private UpdateInfo? _pendingUpdate = null;
        private bool _isDownloadingUpdate = false;

        private void InitializeUpdateService()
        {
            ChkCheckUpdates.IsChecked = _config.check_updates_on_startup;
            TxtUpdateCheckStatus.Text = $"Version v{AppVersion.Version}";

            if (_config.check_updates_on_startup)
            {
                Task.Run(async () =>
                {
                    // Delay initial check by 3.5 seconds so it doesn't block window rendering or LAN discovery
                    await Task.Delay(3500);
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        await CheckForUpdatesSilentAsync();
                    });
                });
            }
        }

        private async Task CheckForUpdatesSilentAsync()
        {
            try
            {
                var info = await UpdateService.CheckForUpdatesAsync(AppVersion.Version);
                if (info.IsUpdateAvailable && !string.IsNullOrWhiteSpace(info.LatestVersion))
                {
                    TxtUpdateCheckStatus.Text = $"v{info.LatestVersion} available!";
                    TxtUpdateCheckStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800"));

                    if (info.LatestVersion != _config.ignored_update_version)
                    {
                        ShowUpdateModal(info);
                    }
                }
                else
                {
                    TxtUpdateCheckStatus.Text = $"Up to date (v{AppVersion.Version}) ✓";
                    TxtUpdateCheckStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                }
            }
            catch { }
        }

        private void ShowUpdateModal(UpdateInfo info)
        {
            _pendingUpdate = info;
            TxtUpdateNewVersion.Text = $"v{info.LatestVersion}";
            TxtUpdateSubtitle.Text = string.IsNullOrWhiteSpace(info.ReleaseTitle)
                ? $"SmartLamp Studio v{info.LatestVersion} is available to install."
                : $"{info.ReleaseTitle} is available to install.";

            TxtUpdateChangelog.Text = string.IsNullOrWhiteSpace(info.ReleaseNotes)
                ? "✨ Performance improvements and bug fixes."
                : info.ReleaseNotes;

            PnlUpdateProgress.Visibility = Visibility.Collapsed;
            PnlUpdateActions.Visibility = Visibility.Visible;
            BtnInstallUpdate.IsEnabled = true;

            OverlayUpdateAvailable.Visibility = Visibility.Visible;
        }

        private async void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            TxtUpdateCheckStatus.Text = "Checking for updates...";
            TxtUpdateCheckStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00F0FF"));
            BtnCheckUpdates.IsEnabled = false;

            try
            {
                var info = await UpdateService.CheckForUpdatesAsync(AppVersion.Version);
                if (info.IsUpdateAvailable)
                {
                    TxtUpdateCheckStatus.Text = $"v{info.LatestVersion} available!";
                    TxtUpdateCheckStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800"));
                    ShowUpdateModal(info);
                }
                else
                {
                    TxtUpdateCheckStatus.Text = $"Up to date (v{AppVersion.Version}) ✓";
                    TxtUpdateCheckStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                }
            }
            catch
            {
                TxtUpdateCheckStatus.Text = "Check failed (Offline)";
                TxtUpdateCheckStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5252"));
            }
            finally
            {
                BtnCheckUpdates.IsEnabled = true;
            }
        }

        private void ChkCheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            _config.check_updates_on_startup = ChkCheckUpdates.IsChecked == true;
            ConfigManager.Save(_config);
        }

        private void BtnCloseUpdateModal_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloadingUpdate) return;
            OverlayUpdateAvailable.Visibility = Visibility.Collapsed;
        }

        private void BtnSkipUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloadingUpdate) return;

            if (_pendingUpdate != null && !string.IsNullOrWhiteSpace(_pendingUpdate.LatestVersion))
            {
                _config.ignored_update_version = _pendingUpdate.LatestVersion;
                ConfigManager.Save(_config);
            }

            OverlayUpdateAvailable.Visibility = Visibility.Collapsed;
        }

        private async void BtnInstallUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingUpdate == null) return;

            // If no binary asset is directly found, open release web page in browser
            if (string.IsNullOrWhiteSpace(_pendingUpdate.DownloadUrl))
            {
                string url = !string.IsNullOrWhiteSpace(_pendingUpdate.HtmlUrl)
                    ? _pendingUpdate.HtmlUrl
                    : "https://github.com/vtstv/SmartLamp/releases";
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
                catch { }
                OverlayUpdateAvailable.Visibility = Visibility.Collapsed;
                return;
            }

            _isDownloadingUpdate = true;
            PnlUpdateActions.Visibility = Visibility.Collapsed;
            PnlUpdateProgress.Visibility = Visibility.Visible;
            TxtUpdateProgressStatus.Text = "Downloading update package...";
            PbUpdateProgress.Value = 0;
            TxtUpdateProgressPercent.Text = "0%";

            var progress = new Progress<int>(percent =>
            {
                PbUpdateProgress.Value = percent;
                TxtUpdateProgressPercent.Text = $"{percent}%";
            });

            try
            {
                string downloadedPath = await UpdateService.DownloadUpdateAsync(
                    _pendingUpdate.DownloadUrl, 
                    _pendingUpdate.FileName, 
                    progress);

                TxtUpdateProgressStatus.Text = "Applying update & restarting...";
                await Task.Delay(600);

                UpdateService.ApplyUpdateAndRestart(downloadedPath);
            }
            catch (Exception ex)
            {
                _isDownloadingUpdate = false;
                PnlUpdateProgress.Visibility = Visibility.Collapsed;
                PnlUpdateActions.Visibility = Visibility.Visible;
                MessageBox.Show($"Failed to download update: {ex.Message}\n\nYou can manually download it from GitHub.", 
                    "Update Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
