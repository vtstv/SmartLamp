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
            // Navigation Bar & Top Header
            TxtAppTitle.Text = LocalizationService.T("AppTitle");
            LblActiveDevice.Text = _config.is_group_control_active
                ? LocalizationService.T("ActiveRoomGroup")
                : LocalizationService.T("ActiveDevice");
            NavDash.Content = LocalizationService.T("NavDash");
            NavGroups.Content = LocalizationService.T("NavGroups");
            if (NavDevices != null) NavDevices.Content = LocalizationService.T("NavDevices");
            NavSettings.Content = LocalizationService.T("NavSettings");
            NavGoogleHome.Content = LocalizationService.T("NavGoogleHome");
            NavMqtt.Content = LocalizationService.T("NavMqtt");

            if (BtnHeaderWizard != null)
            {
                BtnHeaderWizard.Content = LocalizationService.T("AddDeviceWizardBtn");
                BtnHeaderWizard.ToolTip = LocalizationService.T("TooltipWizard");
            }

            // Dashboard Controls
            LblSleepTimer.Text = LocalizationService.T("SleepTimer");
            if (BtnCancelTimer != null) BtnCancelTimer.Content = LocalizationService.T("TimerOff");
            BtnSetCustomTimer.Content = LocalizationService.T("SetCustomMins");
            LblBrightness.Text = LocalizationService.T("BrightnessLevel");
            LblColorTemp.Text = LocalizationService.T("ColorTemperature");
            LblRgbPalette.Text = LocalizationService.T("RgbPalette");
            BtnColorPicker.Content = LocalizationService.T("ColorStudio");
            BtnApplyHex.Content = LocalizationService.T("Apply");
            LblCustomPresets.Text = LocalizationService.T("CustomPresets");
            BtnSavePreset.Content = LocalizationService.T("SaveCurrent");
            BtnSavePreset.ToolTip = LocalizationService.T("TooltipSavePreset");
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
            if (TxtFooterHotkey != null) TxtFooterHotkey.Text = LocalizationService.T("FooterHotkey", GetHotkeyDisplayText());

            // Groups & Rooms Tab
            if (LblRoomsAndGroups != null) LblRoomsAndGroups.Text = LocalizationService.T("RoomsAndGroups");
            if (BtnNewGroup != null)
            {
                BtnNewGroup.Content = LocalizationService.T("AddRoom");
                BtnNewGroup.ToolTip = LocalizationService.T("TooltipAddRoom");
            }
            if (LblGroupConfigRules != null) LblGroupConfigRules.Text = LocalizationService.T("GroupConfigRules");
            if (LblRoomIcon != null) LblRoomIcon.Text = LocalizationService.T("RoomIcon");
            if (LblGroupNameEdit != null) LblGroupNameEdit.Text = LocalizationService.T("GroupNameEdit");
            if (LblIncludedLamps != null) LblIncludedLamps.Text = LocalizationService.T("IncludedLamps");
            if (LblSyncRules != null) LblSyncRules.Text = LocalizationService.T("SyncRules");
            if (LblDimmingMode != null) LblDimmingMode.Text = LocalizationService.T("DimmingMode");
            if (CmbItemSync != null) CmbItemSync.Content = LocalizationService.T("SyncModeSynchronized");
            if (CmbItemProp != null) CmbItemProp.Content = LocalizationService.T("SyncModeProportional");
            if (ChkGroupTurnOffAll != null) ChkGroupTurnOffAll.Content = LocalizationService.T("TurnOffAllLamps");
            if (ChkGroupTurnOnAll != null) ChkGroupTurnOnAll.Content = LocalizationService.T("TurnOnAllLamps");
            if (BtnSaveGroup != null) BtnSaveGroup.Content = LocalizationService.T("SaveRoom");
            if (BtnToggleGroupTest != null)
            {
                BtnToggleGroupTest.Content = LocalizationService.T("TestToggle");
                BtnToggleGroupTest.ToolTip = LocalizationService.T("TooltipTestToggle");
            }
            if (BtnDeleteGroup != null) BtnDeleteGroup.Content = LocalizationService.T("Delete");

            // Settings Tab
            LblLanguageSettings.Text = LocalizationService.T("LanguageSettings");
            LblDevCreds.Text = LocalizationService.T("DeviceCredentials");
            if (BtnNewDevice != null)
            {
                BtnNewDevice.Content = LocalizationService.T("AddNewLamp");
                BtnNewDevice.ToolTip = LocalizationService.T("TooltipNewLamp");
            }
            LblDevName.Text = LocalizationService.T("DeviceName");
            LblDevIp.Text = LocalizationService.T("IpAddress");
            LblDevId.Text = LocalizationService.T("DeviceId");
            LblLocalKey.Text = LocalizationService.T("LocalKey");
            LblGroupName.Text = LocalizationService.T("GroupName");
            BtnSaveDevice.Content = LocalizationService.T("SaveDevice");
            BtnDeleteDevice.Content = LocalizationService.T("Delete");

            if (LblAppBehavior != null) LblAppBehavior.Text = LocalizationService.T("AppBehavior");
            ChkAutostart.Content = LocalizationService.T("Autostart");
            ChkCloseToTray.Content = LocalizationService.T("CloseToTray");
            ChkTimerNotifications.Content = LocalizationService.T("TimerNotifications");
            ChkEnableGoogle.Content = LocalizationService.T("EnableGoogle");
            ChkEnableMqtt.Content = LocalizationService.T("EnableMqtt");
            ChkCheckUpdates.Content = LocalizationService.T("CheckUpdatesStartup");

            LblSystemUpdates.Text = LocalizationService.T("SystemUpdates");
            BtnCheckUpdates.Content = LocalizationService.T("CheckUpdatesBtn");
            BtnCheckUpdates.ToolTip = LocalizationService.T("TooltipCheckUpdates");
            if (BtnInstallApp != null) BtnInstallApp.Content = LocalizationService.T("InstallApp");
            BtnCreateShortcut.Content = LocalizationService.T("DesktopShortcut");

            if (LblHotkeyTitle != null) LblHotkeyTitle.Text = LocalizationService.T("HotkeyTitle");
            BtnSaveHotkey.Content = LocalizationService.T("SaveHotkey");

            LblWifiDiscovery.Text = LocalizationService.T("WifiDiscovery");
            BtnAutoDiscover.Content = LocalizationService.T("ScanWifi");

            if (LblCloudSync != null) LblCloudSync.Text = LocalizationService.T("CloudSync");
            if (LblAccId != null) LblAccId.Text = LocalizationService.T("AccessId");
            if (LblAccKey != null) LblAccKey.Text = LocalizationService.T("AccessKey");
            BtnCloudFetch.Content = LocalizationService.T("FetchKeyCloud");
            BtnCloudSyncAll.Content = LocalizationService.T("CloudSyncAll");
            BtnCloudSyncAll.ToolTip = LocalizationService.T("TooltipSyncAll");
            BtnTuyaGuide.Content = LocalizationService.T("Instructions");
            BtnTuyaGuide.ToolTip = LocalizationService.T("TooltipGuide");

            if (LblControlModeTitle != null) LblControlModeTitle.Text = LocalizationService.T("ControlModeTitle");
            if (LblControlMode != null) LblControlMode.Text = LocalizationService.T("ControlMode");
            if (CmbItemModeAuto != null) CmbItemModeAuto.Content = LocalizationService.T("ControlModeAuto");
            if (CmbItemModeLocal != null) CmbItemModeLocal.Content = LocalizationService.T("ControlModeLocal");
            if (CmbItemModeCloud != null) CmbItemModeCloud.Content = LocalizationService.T("ControlModeCloud");
            if (LblAutoPriority != null) LblAutoPriority.Text = LocalizationService.T("AutoPriority");
            if (CmbItemPriCloud != null) CmbItemPriCloud.Content = LocalizationService.T("PriorityCloudFirst");
            if (CmbItemPriLocal != null) CmbItemPriLocal.Content = LocalizationService.T("PriorityLocalFirst");

            // Google Home & MQTT Bridges
            if (LblGoogleTitle != null) LblGoogleTitle.Text = LocalizationService.T("GoogleHomeBridgeTitle");
            if (LblGoogleDesc != null) LblGoogleDesc.Text = LocalizationService.T("GoogleHomeBridgeDesc");
            if (BtnStartGoogleBridge != null) BtnStartGoogleBridge.Content = LocalizationService.T("RestartGoogleBridge");
            if (LblGoogleSimTitle != null) LblGoogleSimTitle.Text = LocalizationService.T("GoogleSimTitle");
            if (BtnGoogleSimOn != null) BtnGoogleSimOn.Content = LocalizationService.T("GoogleSimOn");
            if (BtnGoogleSimOff != null) BtnGoogleSimOff.Content = LocalizationService.T("GoogleSimOff");
            if (LblGoogleLog != null) LblGoogleLog.Text = LocalizationService.T("GoogleEventLog");

            if (LblMqttTitle != null) LblMqttTitle.Text = LocalizationService.T("MqttBridgeTitle");
            if (LblMqttDesc != null) LblMqttDesc.Text = LocalizationService.T("MqttBridgeDesc");
            if (BtnStartMqttBridge != null) BtnStartMqttBridge.Content = LocalizationService.T("RestartMqttBridge");
            if (BtnCopyMqttDiscovery != null) BtnCopyMqttDiscovery.Content = LocalizationService.T("CopyMqttJson");
            if (LblMqttSimTitle != null) LblMqttSimTitle.Text = LocalizationService.T("MqttSimTitle");
            if (BtnMqttSimOn != null) BtnMqttSimOn.Content = LocalizationService.T("MqttSimOn");
            if (BtnMqttSimOff != null) BtnMqttSimOff.Content = LocalizationService.T("MqttSimOff");
            if (LblMqttLog != null) LblMqttLog.Text = LocalizationService.T("MqttEventLog");

            // Modals
            if (BtnCloseTuyaGuide != null) BtnCloseTuyaGuide.Content = LocalizationService.T("GotIt");
            if (LblSavePresetTitle != null) LblSavePresetTitle.Text = LocalizationService.T("SavePresetTitle");
            if (LblPresetNameTitle != null) LblPresetNameTitle.Text = LocalizationService.T("PresetName");
            if (LblCurrentSettingsTitle != null) LblCurrentSettingsTitle.Text = LocalizationService.T("CurrentSettingsToSave");
            if (BtnCancelSavePreset != null) BtnCancelSavePreset.Content = LocalizationService.T("Cancel");
            if (BtnConfirmSavePreset != null) BtnConfirmSavePreset.Content = LocalizationService.T("SavePreset");

            if (LblUpdateAvailableTitle != null) LblUpdateAvailableTitle.Text = LocalizationService.T("UpdateAvailableTitle");
            if (TxtUpdateSubtitle != null) TxtUpdateSubtitle.Text = LocalizationService.T("UpdateSubtitle");
            if (LblReleaseNotesTitle != null) LblReleaseNotesTitle.Text = LocalizationService.T("ReleaseNotes");
            if (BtnSkipUpdate != null) BtnSkipUpdate.Content = LocalizationService.T("SkipUpdate");
            if (BtnRemindLater != null) BtnRemindLater.Content = LocalizationService.T("RemindLater");
            if (BtnInstallUpdate != null) BtnInstallUpdate.Content = LocalizationService.T("UpdateNow");

            // Devices Tab & QR Login
            if (LblMyDevicesTitle != null) LblMyDevicesTitle.Text = LocalizationService.T("MyDevicesTitle");
            if (BtnDeviceTabQrLogin != null)
            {
                BtnDeviceTabQrLogin.Content = LocalizationService.T("QrLoginBtn");
                BtnDeviceTabQrLogin.ToolTip = LocalizationService.T("TooltipQrLogin");
            }
            if (BtnDeviceTabWizard != null)
            {
                BtnDeviceTabWizard.Content = LocalizationService.T("AddDeviceWizardBtn");
                BtnDeviceTabWizard.ToolTip = LocalizationService.T("TooltipWizard");
            }
            if (BtnTestDevice != null) BtnTestDevice.Content = LocalizationService.T("TestDeviceBtn");

            // Backup & Data Portability Card
            if (LblBackupTitle != null) LblBackupTitle.Text = LocalizationService.T("BackupTitle");
            if (LblBackupDesc != null) LblBackupDesc.Text = LocalizationService.T("BackupDesc");
            if (BtnExportBackup != null) BtnExportBackup.Content = LocalizationService.T("ExportBackup");
            if (BtnImportBackup != null) BtnImportBackup.Content = LocalizationService.T("ImportBackup");

            // Setup Wizard Modal
            if (LblWizardTitle != null) LblWizardTitle.Text = LocalizationService.T("WizardTitle");
            if (TxtBadgeStep1 != null) TxtBadgeStep1.Text = LocalizationService.T("WizardBadgeStep1");
            if (TxtBadgeStep2 != null) TxtBadgeStep2.Text = LocalizationService.T("WizardBadgeStep2");
            if (TxtBadgeStep3 != null) TxtBadgeStep3.Text = LocalizationService.T("WizardBadgeStep3");
            if (LblWizOptQrTitle != null) LblWizOptQrTitle.Text = LocalizationService.T("WizardOptQrTitle");
            if (LblWizOptQrDesc != null) LblWizOptQrDesc.Text = LocalizationService.T("WizardOptQrDesc");
            if (LblWizOptWifiTitle != null) LblWizOptWifiTitle.Text = LocalizationService.T("WizardOptWifiTitle");
            if (LblWizOptWifiDesc != null) LblWizOptWifiDesc.Text = LocalizationService.T("WizardOptWifiDesc");
            if (LblWizOptCloudTitle != null) LblWizOptCloudTitle.Text = LocalizationService.T("WizardOptCloudTitle");
            if (LblWizOptCloudDesc != null) LblWizOptCloudDesc.Text = LocalizationService.T("WizardOptCloudDesc");
            if (LblWizOptManualTitle != null) LblWizOptManualTitle.Text = LocalizationService.T("WizardOptManualTitle");
            if (LblWizOptManualDesc != null) LblWizOptManualDesc.Text = LocalizationService.T("WizardOptManualDesc");
            if (LblWizQrInstruction != null) LblWizQrInstruction.Text = LocalizationService.T("QrInstruction");
            if (LblWizUserCodePrompt != null) LblWizUserCodePrompt.Text = LocalizationService.T("UserCodePrompt");
            if (LblWizUserCodeHelp != null) LblWizUserCodeHelp.Text = LocalizationService.T("UserCodeHelp");
            if (BtnGenerateQrCode != null && (string)BtnGenerateQrCode.Content != "🔄 Refresh QR Code" && (string)BtnGenerateQrCode.Content != "⏳ Generating...")
            {
                BtnGenerateQrCode.Content = LocalizationService.T("GenerateQrBtn");
            }
            if (LblWizFoundDevices != null) LblWizFoundDevices.Text = LocalizationService.T("WizardFoundDevices");
            if (BtnWizRescan != null) BtnWizRescan.Content = LocalizationService.T("WizardScanNow");
            if (BtnWizLoadCloud != null) BtnWizLoadCloud.Content = LocalizationService.T("WizardCloudFetchBtn");
            if (BtnWizStep2Back != null) BtnWizStep2Back.Content = LocalizationService.T("WizardBack");
            if (BtnWizStep2Next != null) BtnWizStep2Next.Content = LocalizationService.T("WizardNext");
            if (BtnWizTestToggle != null) BtnWizTestToggle.Content = LocalizationService.T("WizardTestConnection");
            if (BtnWizStep3Back != null) BtnWizStep3Back.Content = LocalizationService.T("WizardBack");
            if (BtnWizFinish != null) BtnWizFinish.Content = LocalizationService.T("WizardFinish");

            // Compact mode labels
            TxtCompactTitle.Text = LocalizationService.T("AppTitle");
            LblCompactBrightness.Text = LocalizationService.T("BrightnessLevel");
            LblCompactTemp.Text = LocalizationService.T("ColorTemperature");
            BtnCompactSwitchFull.Content = LocalizationService.T("FullMode");

            UpdatePowerButtonUi();
            RenderCustomPresets();
            if (ViewGroups != null && ViewGroups.Visibility == Visibility.Visible)
            {
                RenderGroupsTab();
            }
        }
    }
}
