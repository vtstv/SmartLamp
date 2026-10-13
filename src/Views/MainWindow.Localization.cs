// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SmartLampApp.Models;
using SmartLampApp.Services;

namespace SmartLampApp
{
    public partial class MainWindow : Window
    {
        private bool _isCompactMode = false;

        private void BtnToggleModeFull_Click(object sender, RoutedEventArgs e)
        {
            SetCompactMode(true);
        }

        private void BtnCompactExpand_Click(object sender, RoutedEventArgs e)
        {
            SetCompactMode(false);
        }

        private void InitializeLocalization()
        {
            CmbLanguage.SelectionChanged += (s, e) =>
            {
                if (_isUpdatingUi) return;
                if (CmbLanguage.SelectedItem is ComboBoxItem item && item.Tag is string tag)
                {
                    SetLanguage(tag);
                }
            };

            LocalizationService.LanguageChanged += ApplyLocalization;
        }

        public void SetLanguage(string lang)
        {
            LocalizationService.CurrentLanguage = lang;
            _config.language = lang;
            ConfigManager.Save(_config);

            UpdateLanguageDropdown(lang);
            ApplyLocalization();
        }

        private void UpdateLanguageDropdown(string lang)
        {
            _isUpdatingUi = true;
            for (int i = 0; i < CmbLanguage.Items.Count; i++)
            {
                if (CmbLanguage.Items[i] is ComboBoxItem item && (string?)item.Tag == lang)
                {
                    CmbLanguage.SelectedIndex = i;
                    break;
                }
            }
            _isUpdatingUi = false;
        }

        public void SetCompactMode(bool compact)
        {
            _isCompactMode = compact;
            _config.compact_mode = compact;
            ConfigManager.Save(_config);

            if (compact)
            {
                Width = 430;
                Height = 440;
                ViewFullRoot.Visibility = Visibility.Collapsed;
                ViewCompactRoot.Visibility = Visibility.Visible;
            }
            else
            {
                Width = 960;
                Height = 770;
                ViewFullRoot.Visibility = Visibility.Visible;
                ViewCompactRoot.Visibility = Visibility.Collapsed;
            }
        }

        private void ApplyLocalization()
        {
            TxtAppTitle.Text = LocalizationService.T("AppTitle");
            LblActiveDevice.Text = LocalizationService.T("ActiveDevice");
            NavDash.Content = LocalizationService.T("NavDash");
            NavSettings.Content = LocalizationService.T("NavSettings");
            LblSleepTimer.Text = LocalizationService.T("SleepTimer");
            BtnSetCustomTimer.Content = LocalizationService.T("SetCustomMins");
            LblBrightness.Text = LocalizationService.T("BrightnessLevel");
            LblColorTemp.Text = LocalizationService.T("ColorTemperature");
            LblRgbPalette.Text = LocalizationService.T("RgbPalette");
            BtnColorPicker.Content = LocalizationService.T("ColorStudio");
            BtnApplyHex.Content = LocalizationService.T("Apply");
            LblCustomPresets.Text = LocalizationService.T("CustomPresets");
            BtnSavePreset.Content = LocalizationService.T("SaveCurrent");
            LblLightingScenes.Text = LocalizationService.T("LightingScenes");

            SceneNight.Content = LocalizationService.T("SceneNight");
            SceneRead.Content = LocalizationService.T("SceneRead");
            SceneFocus.Content = LocalizationService.T("SceneFocus");
            SceneRelax.Content = LocalizationService.T("SceneRelax");
            SceneParty.Content = LocalizationService.T("SceneParty");
            SceneCycle.Content = LocalizationService.T("SceneCycle");

            BtnT2700.Content = LocalizationService.T("Warm");
            BtnT4000.Content = LocalizationService.T("Neutral");
            BtnT6500.Content = LocalizationService.T("Cool");

            BtnRefresh.Content = LocalizationService.T("Refresh");
            BtnSwitchToCompactFooter.Content = LocalizationService.T("CompactMode");

            // Settings tab
            LblLanguageSettings.Text = LocalizationService.T("LanguageSettings");
            LblDevCreds.Text = LocalizationService.T("DeviceCredentials");
            LblDevName.Text = LocalizationService.T("DeviceName");
            LblDevIp.Text = LocalizationService.T("IpAddress");
            LblDevId.Text = LocalizationService.T("DeviceId");
            LblLocalKey.Text = LocalizationService.T("LocalKey");
            LblGroupName.Text = LocalizationService.T("GroupName");
            BtnSaveDevice.Content = LocalizationService.T("SaveDevice");
            BtnDeleteDevice.Content = LocalizationService.T("Delete");
            LblWifiDiscovery.Text = LocalizationService.T("WifiDiscovery");
            BtnAutoDiscover.Content = LocalizationService.T("ScanWifi");
            LblSystemUpdates.Text = LocalizationService.T("SystemUpdates");
            BtnCheckUpdates.Content = LocalizationService.T("CheckUpdatesBtn");
            BtnCreateShortcut.Content = LocalizationService.T("CreateShortcut");

            // Compact mode labels
            TxtCompactTitle.Text = LocalizationService.T("AppTitle");
            LblCompactBrightness.Text = LocalizationService.T("BrightnessLevel");
            LblCompactTemp.Text = LocalizationService.T("ColorTemperature");
            BtnCompactSwitchFull.Content = LocalizationService.T("FullMode");

            UpdatePowerButtonUi();
            RenderCustomPresets();
        }
    }
}
