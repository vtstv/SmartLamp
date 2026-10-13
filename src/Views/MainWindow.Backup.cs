// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using SmartLampApp.Models;
using SmartLampApp.Services;

using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace SmartLampApp
{
    public partial class MainWindow : Window
    {
        private void BtnExportBackup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sfd = new SaveFileDialog
                {
                    Title = LocalizationService.T("ExportBackup"),
                    Filter = "SmartLamp Backup (*.json)|*.json|All Files (*.*)|*.*",
                    FileName = $"smartlamp_backup_{DateTime.Now:yyyy-MM-dd}.json",
                    DefaultExt = ".json"
                };

                if (sfd.ShowDialog(this) == true)
                {
                    BackupService.ExportToFile(sfd.FileName, _config);
                    string msg = LocalizationService.T("BackupExportSuccess", Path.GetFileName(sfd.FileName));
                    MessageBox.Show(this, msg, LocalizationService.T("BackupTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnImportBackup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ofd = new OpenFileDialog
                {
                    Title = LocalizationService.T("ImportBackup"),
                    Filter = "SmartLamp Backup or Config (*.json)|*.json|All Files (*.*)|*.*",
                    Multiselect = false
                };

                if (ofd.ShowDialog(this) == true)
                {
                    string json = File.ReadAllText(ofd.FileName);
                    var backup = BackupService.ParseBackup(json);

                    if (backup == null)
                    {
                        MessageBox.Show(this, LocalizationService.T("BackupImportError"), 
                                        LocalizationService.T("BackupTitle"), 
                                        MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    int devCount = backup.Devices?.Count ?? 0;
                    int grpCount = backup.Groups?.Count ?? 0;
                    int prsCount = backup.CustomPresets?.Count ?? 0;

                    string prompt = LocalizationService.T("BackupImportConfirm", devCount, grpCount, prsCount);
                    var result = MessageBox.Show(this, prompt, LocalizationService.T("BackupTitle"), 
                                                 MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Cancel) return;

                    bool mergeMode = (result == MessageBoxResult.No); // Yes = Overwrite, No = Merge

                    _config = BackupService.RestoreBackup(_config, backup, mergeMode);
                    ConfigManager.Save(_config);
                    _protocol.UpdateConfig(_config);

                    // Refresh UI
                    PopulateDeviceSelector();
                    PopulateMasterDevicesList();
                    PopulateDeviceForm();
                    RenderCustomPresets();
                    if (ViewGroups != null && ViewGroups.Visibility == Visibility.Visible)
                    {
                        RenderGroupsTab();
                    }

                    if (!string.IsNullOrEmpty(_config.language))
                    {
                        SetLanguage(_config.language);
                    }

                    _ = RefreshStatusAsync();

                    string modeName = mergeMode ? "Merge" : "Overwrite";
                    string successMsg = LocalizationService.T("BackupImportSuccess", modeName);
                    MessageBox.Show(this, successMsg, LocalizationService.T("BackupTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
