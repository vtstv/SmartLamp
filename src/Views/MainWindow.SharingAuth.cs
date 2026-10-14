// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;
using SmartLampApp.Models;
using SmartLampApp.Services;

using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;

namespace SmartLampApp
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource? _qrPollCts;
        private readonly TuyaSharingAuthService _sharingAuthService = new();
        private List<TuyaSharingDiscoveredDevice> _sharingDevices = new();

        public void CancelQrPolling()
        {
            try
            {
                _qrPollCts?.Cancel();
                _qrPollCts?.Dispose();
            }
            catch { }
            finally
            {
                _qrPollCts = null;
            }
        }

        private void BtnDeviceTabQrLogin_Click(object sender, RoutedEventArgs e)
        {
            _wizardMethod = WizardMethod.QrLogin;
            _wizardCurrentStep = 2;
            _wizardCandidateDevice = null;
            OverlayDeviceWizard.Visibility = Visibility.Visible;
            UpdateWizardStepUi();
        }

        private void BtnWizardChooseQr_Click(object sender, RoutedEventArgs e)
        {
            _wizardMethod = WizardMethod.QrLogin;
            _wizardCurrentStep = 2;
            UpdateWizardStepUi();
        }

        private async void BtnGenerateQrCode_Click(object sender, RoutedEventArgs e)
        {
            string userCode = TxtWizUserCode.Text.Trim();
            if (string.IsNullOrWhiteSpace(userCode))
            {
                MessageBox.Show(this, LocalizationService.T("QrUserCodeRequired"), "SmartLamp", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string schema = "smartlife";
            if (CmbWizSchema.SelectedItem is ComboBoxItem item && item.Tag is string tag && !string.IsNullOrEmpty(tag))
            {
                schema = tag;
            }

            CancelQrPolling();
            BtnGenerateQrCode.IsEnabled = false;
            BtnGenerateQrCode.Content = "⏳ Generating...";
            TxtWizQrStatus.Text = "Connecting to Tuya gateway...";
            TxtWizQrStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xF0, 0xFF));
            BorderQrCode.Visibility = Visibility.Collapsed;
            LstWizQrDevices.Visibility = Visibility.Collapsed;

            try
            {
                var tokenRes = await _sharingAuthService.RequestQrCodeAsync(userCode, schema);
                if (!tokenRes.Success || string.IsNullOrEmpty(tokenRes.Token))
                {
                    string errMsg = !string.IsNullOrEmpty(tokenRes.ErrorMsg) ? tokenRes.ErrorMsg :
                                    !string.IsNullOrEmpty(tokenRes.ErrorCode) ? tokenRes.ErrorCode :
                                    "Failed to obtain login QR code from Tuya gateway.";
                    TxtWizQrStatus.Text = $"🔴 {errMsg}";
                    TxtWizQrStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                    BtnGenerateQrCode.IsEnabled = true;
                    BtnGenerateQrCode.Content = LocalizationService.T("GenerateQrBtn");
                    return;
                }

                // Render QR code
                using var qrGen = new QRCodeGenerator();
                using var qrData = qrGen.CreateQrCode(tokenRes.QrCodeContent, QRCodeGenerator.ECCLevel.M);
                using var pngQr = new PngByteQRCode(qrData);
                byte[] pngBytes = pngQr.GetGraphic(8, new byte[] { 0, 0, 0 }, new byte[] { 255, 255, 255 });

                var bmp = new BitmapImage();
                using (var ms = new MemoryStream(pngBytes))
                {
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                }

                ImgWizQrCode.Source = bmp;
                BorderQrCode.Visibility = Visibility.Visible;
                TxtWizQrStatus.Text = LocalizationService.T("QrWaitingScan");
                TxtWizQrStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xF0, 0xFF));
                BtnGenerateQrCode.Content = "🔄 Refresh QR Code";
                BtnGenerateQrCode.IsEnabled = true;

                // Start polling
                _qrPollCts = new CancellationTokenSource();
                var ct = _qrPollCts.Token;

                _ = Task.Run(async () =>
                {
                    for (int i = 0; i < 90; i++)
                    {
                        if (ct.IsCancellationRequested) break;
                        await Task.Delay(2000, ct);
                        if (ct.IsCancellationRequested) break;

                        var (scanned, session, err) = await _sharingAuthService.PollLoginResultAsync(tokenRes.Token, userCode, ct: ct);
                        if (scanned && session != null)
                        {
                            await Dispatcher.InvokeAsync(async () =>
                            {
                                TxtWizQrStatus.Text = LocalizationService.T("QrAuthorized");
                                TxtWizQrStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));

                                var knownIds = new List<string>();
                                if (_config?.devices != null) knownIds.AddRange(_config.devices.Select(x => x.DevId));
                                if (_discoveredList != null) knownIds.AddRange(_discoveredList.Select(x => x.DevId));
                                if (!string.IsNullOrEmpty(_config?.dev_id)) knownIds.Add(_config.dev_id);

                                _sharingDevices = await _sharingAuthService.QueryUserDevicesAsync(session, knownIds);
                                if (_sharingDevices != null && _sharingDevices.Count > 0)
                                {
                                    LstWizQrDevices.Items.Clear();
                                    int added = 0;
                                    foreach (var d in _sharingDevices)
                                    {
                                        string cloudIp = d.Ip ?? "";
                                        string localIp = "";
                                        string externalIp = "";

                                        if (TuyaSharingCrypto.IsPrivateIp(cloudIp))
                                        {
                                            localIp = cloudIp;
                                        }
                                        else
                                        {
                                            externalIp = cloudIp;
                                        }

                                        // 1. Check cached UDP broadcast discovered list for private LAN IP
                                        var disc = _discoveredList?.Find(x => x.DevId == d.Id && TuyaSharingCrypto.IsPrivateIp(x.Ip));
                                        if (disc != null && !string.IsNullOrEmpty(disc.Ip))
                                        {
                                            localIp = disc.Ip;
                                        }
                                        else
                                        {
                                            // 2. Check existing config for known local IP
                                            var existingDev = _config?.devices?.Find(x => x.DevId == d.Id);
                                            if (existingDev != null && !string.IsNullOrEmpty(existingDev.LocalIp))
                                            {
                                                localIp = existingDev.LocalIp;
                                            }
                                            else if (existingDev != null && TuyaSharingCrypto.IsPrivateIp(existingDev.Ip))
                                            {
                                                localIp = existingDev.Ip;
                                            }
                                            else if (_config?.dev_id == d.Id && TuyaSharingCrypto.IsPrivateIp(_config.ip))
                                            {
                                                localIp = _config.ip;
                                            }
                                        }

                                        // Prefer Local IP over External IP
                                        string devIp = !string.IsNullOrEmpty(localIp) ? localIp : cloudIp;
                                        bool useLocal = !string.IsNullOrEmpty(localIp);

                                        string keyState = !string.IsNullOrEmpty(d.LocalKey) ? "🔑 Key OK" : "No Key";
                                        string ipState = !string.IsNullOrEmpty(localIp) ? $" • LAN: {localIp}" : (!string.IsNullOrEmpty(externalIp) ? $" • WAN: {externalIp}" : "");
                                        LstWizQrDevices.Items.Add($"💡 {d.Name} ({d.Id}){ipState} • {keyState}");

                                        if (_config != null)
                                        {
                                            _config.devices ??= new();
                                            var existing = _config.devices.Find(x => x.DevId == d.Id);
                                            if (existing == null)
                                            {
                                                _config.devices.Add(new DeviceInfo
                                                {
                                                    Name = string.IsNullOrWhiteSpace(d.Name) ? "Smart Lamp" : d.Name,
                                                    DevId = d.Id,
                                                    LocalKey = d.LocalKey,
                                                    Ip = devIp,
                                                    LocalIp = localIp,
                                                    ExternalIp = externalIp,
                                                    UseLocalIp = useLocal,
                                                    Version = "3.5",
                                                    GroupName = string.IsNullOrWhiteSpace(d.HomeName) ? "Living Room" : d.HomeName
                                                });
                                                added++;
                                            }
                                            else
                                            {
                                                if (!string.IsNullOrEmpty(d.LocalKey)) existing.LocalKey = d.LocalKey;
                                                if (!string.IsNullOrEmpty(localIp)) existing.LocalIp = localIp;
                                                if (!string.IsNullOrEmpty(externalIp)) existing.ExternalIp = externalIp;
                                                existing.Ip = devIp;
                                                existing.UseLocalIp = useLocal;
                                            }

                                            if (_config.dev_id == d.Id || string.IsNullOrEmpty(_config.local_key))
                                            {
                                                if (!string.IsNullOrEmpty(d.LocalKey)) _config.local_key = d.LocalKey;
                                                _config.ip = devIp;
                                                if (string.IsNullOrEmpty(_config.dev_id)) _config.dev_id = d.Id;
                                            }
                                        }
                                    }

                                    LstWizQrDevices.Visibility = Visibility.Visible;
                                    if (_config != null)
                                    {
                                        ConfigManager.Save(_config);
                                        _protocol.UpdateConfig(_config);
                                    }
                                    PopulateDeviceSelector();
                                    PopulateMasterDevicesList();

                                    string successMsg = LocalizationService.T("QrDevicesImported", _sharingDevices.Count);
                                    TxtWizQrStatus.Text = $"✅ {successMsg}";

                                    if (_sharingDevices.Count > 0)
                                    {
                                        LstWizQrDevices.SelectedIndex = 0;
                                    }

                                    // Automatically resolve local LAN IPs for all imported devices!
                                    try
                                    {
                                        var (resolved, total) = await _protocol.AutoResolveAllDeviceIpsAsync(3000);
                                        if (resolved > 0 && _config?.devices != null)
                                        {
                                            LstWizQrDevices.Items.Clear();
                                            foreach (var dev in _config.devices)
                                            {
                                                string ipState = !string.IsNullOrEmpty(dev.LocalIp) ? $" • LAN: {dev.LocalIp}" : (!string.IsNullOrEmpty(dev.ExternalIp) ? $" • WAN: {dev.ExternalIp}" : "");
                                                string keyState = !string.IsNullOrEmpty(dev.LocalKey) ? "🔑 Key OK" : "No Key";
                                                LstWizQrDevices.Items.Add($"💡 {dev.Name} ({dev.DevId}){ipState} • {keyState}");
                                            }
                                            TxtWizQrStatus.Text = $"✅ {successMsg} • Local LAN IP(s) resolved ({resolved}/{total})!";
                                            PopulateDeviceSelector();
                                            PopulateMasterDevicesList();
                                            if (_sharingDevices.Count > 0)
                                            {
                                                LstWizQrDevices.SelectedIndex = 0;
                                            }
                                        }
                                    }
                                    catch { }
                                }
                                else
                                {
                                    TxtWizQrStatus.Text = "⚠️ Login OK, but 0 devices found in cloud. Click here to open tuya_sharing_debug.log";
                                    TxtWizQrStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24));
                                }
                            });
                            break;
                        }
                    }
                }, ct);
            }
            catch (Exception ex)
            {
                TxtWizQrStatus.Text = $"🔴 {ex.Message}";
                TxtWizQrStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                BtnGenerateQrCode.IsEnabled = true;
                BtnGenerateQrCode.Content = LocalizationService.T("GenerateQrBtn");
            }
        }

        private void TxtWizQrStatus_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                string logPath = TuyaSharingAuthService.GetDiagnosticLogPath();
                if (File.Exists(logPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = logPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show(this, "Diagnostic log file is not yet created.", "SmartLamp", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not open log file: {ex.Message}", "SmartLamp", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LstWizQrDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstWizQrDevices.SelectedIndex < 0 || LstWizQrDevices.SelectedIndex >= _sharingDevices.Count) return;
            var sel = _sharingDevices[LstWizQrDevices.SelectedIndex];

            _wizardCandidateDevice = new DeviceInfo
            {
                Name = sel.Name,
                DevId = sel.Id,
                LocalKey = sel.LocalKey,
                Ip = sel.Ip ?? "",
                Version = "3.5",
                GroupName = string.IsNullOrWhiteSpace(sel.HomeName) ? "Living Room" : sel.HomeName
            };

            TxtWizFinalName.Text = sel.Name;
            TxtWizFinalGroup.Text = string.IsNullOrWhiteSpace(sel.HomeName) ? "Living Room" : sel.HomeName;
        }
    }
}
