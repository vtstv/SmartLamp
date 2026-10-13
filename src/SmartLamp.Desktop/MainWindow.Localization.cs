// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using Avalonia.Controls;
using Avalonia.Media;
using SmartLamp.Desktop.Models;
using SmartLamp.Desktop.Services;

namespace SmartLamp.Desktop
{
    public partial class MainWindow
    {
        private void InitializeLocalization()
        {
            BtnLangEn.Click += (s, e) => SetLanguage("en");
            BtnLangDe.Click += (s, e) => SetLanguage("de");
            BtnLangRu.Click += (s, e) => SetLanguage("ru");

            LocalizationService.LanguageChanged += ApplyLocalization;
        }

        private void SetLanguage(string lang)
        {
            LocalizationService.CurrentLanguage = lang;
            _config.language = lang;
            ConfigManager.Save(_config);

            UpdateLanguageButtons(lang);
            ApplyLocalization();
        }

        private void UpdateLanguageButtons(string lang)
        {
            UpdateBtnLang(BtnLangEn, lang == "en");
            UpdateBtnLang(BtnLangDe, lang == "de");
            UpdateBtnLang(BtnLangRu, lang == "ru");
        }

        private static void UpdateBtnLang(Button btn, bool isActive)
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
                btn.Foreground = Brush.Parse("#8888AA");
                btn.FontWeight = FontWeight.Normal;
            }
        }

        private void ApplyLocalization()
        {
            TxtAppTitle.Text = LocalizationService.T("AppTitle");
            LblActiveDevice.Text = LocalizationService.T("ActiveDevice");
            NavDash.Content = LocalizationService.T("NavDash");
            NavSettings.Content = LocalizationService.T("NavSettings");
            NavGoogleHome.Content = LocalizationService.T("NavGoogleHome");
            NavMqtt.Content = LocalizationService.T("NavMqtt");

            LblSleepTimer.Text = LocalizationService.T("SleepTimer");
            BtnSetCustomTimer.Content = LocalizationService.T("SetCustomMins");
            LblBrightness.Text = LocalizationService.T("BrightnessLevel");
            LblColorTemp.Text = LocalizationService.T("ColorTemperature");
            LblRgbPalette.Text = LocalizationService.T("RgbPalette");
            BtnColorStudio.Content = LocalizationService.T("ColorStudio");
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

            // Settings tab - Language Card
            LblLanguageSettings.Text = LocalizationService.T("LanguageSettings");
            BtnLangEn.Content = "🇬🇧 " + LocalizationService.T("LangEn");
            BtnLangDe.Content = "🇩🇪 " + LocalizationService.T("LangDe");
            BtnLangRu.Content = "🇷🇺 " + LocalizationService.T("LangRu");

            // Settings tab - Device Credentials Card
            LblDevCreds.Text = LocalizationService.T("DeviceCredentials");
            LblDevName.Text = LocalizationService.T("DeviceName");
            LblDevIp.Text = LocalizationService.T("IpAddress");
            LblDevId.Text = LocalizationService.T("DeviceId");
            LblLocalKey.Text = LocalizationService.T("LocalKey");
            LblGroupName.Text = LocalizationService.T("GroupName");
            BtnSaveDevice.Content = LocalizationService.T("SaveDevice");
            BtnDeleteDevice.Content = LocalizationService.T("Delete");

            // Settings tab - App Behavior Card
            LblAppBehavior.Text = LocalizationService.T("AppBehavior");
            ChkCloseToTray.Content = LocalizationService.T("CloseToTray");
            ChkTimerNotifications.Content = LocalizationService.T("TimerNotifications");
            ChkEnableGoogle.Content = LocalizationService.T("EnableGoogle");
            ChkEnableMqtt.Content = LocalizationService.T("EnableMqtt");
            ChkCheckUpdates.Content = LocalizationService.T("CheckUpdates");
            BtnCheckUpdates.Content = LocalizationService.T("CheckUpdatesBtn");
            BtnCreateShortcut.Content = LocalizationService.T("CreateShortcut");

            // Settings tab - Wi-Fi Discovery Card
            LblWifiDiscovery.Text = LocalizationService.T("WifiDiscovery");
            BtnAutoDiscover.Content = LocalizationService.T("ScanWifi");

            // Settings tab - Tuya Cloud Sync Card
            LblCloudSync.Text = LocalizationService.T("CloudSync");
            LblAccId.Text = LocalizationService.T("AccessId");
            LblAccKey.Text = LocalizationService.T("AccessKey");
            BtnCloudFetch.Content = LocalizationService.T("FetchKeyCloud");
            BtnTuyaGuide.Content = LocalizationService.T("Instructions");

            // Settings tab - Control Mode Card
            LblControlModeTitle.Text = LocalizationService.T("ControlModeTitle");
            LblControlMode.Text = LocalizationService.T("ControlMode");
            LblAutoPriority.Text = LocalizationService.T("AutoPriority");

            // Compact mode labels
            TxtCompactTitle.Text = LocalizationService.T("AppTitle");
            LblCompactBrightness.Text = LocalizationService.T("BrightnessLevel");
            LblCompactTemp.Text = LocalizationService.T("ColorTemperature");
            BtnCompactSwitchFull.Content = LocalizationService.T("FullMode");

            // Modal dialog
            LblSavePresetTitle.Text = LocalizationService.T("SavePresetTitle");
            LblPresetName.Text = LocalizationService.T("PresetName");
            BtnCancelSavePreset.Content = LocalizationService.T("Cancel");
            BtnConfirmSavePreset.Content = LocalizationService.T("SavePreset");

            UpdatePowerButtonUi();
            RenderCustomPresets();
        }
    }
}
