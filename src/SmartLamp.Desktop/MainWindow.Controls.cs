// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using SmartLamp.Desktop.Services;
using TuyaNet.Native;

namespace SmartLamp.Desktop
{
    public partial class MainWindow
    {
        private DispatcherTimer? _brightDebouncer;
        private DispatcherTimer? _tempDebouncer;
        private DispatcherTimer? _sleepTimer;
        private int _sleepSecondsRemaining = 0;

        private void InitializeControls()
        {
            BtnPower.Click += async (s, e) => await TogglePowerAsync();
            BtnCompactPower.Click += async (s, e) => await TogglePowerAsync();

            // Brightness Debouncers
            _brightDebouncer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _brightDebouncer.Tick += async (s, e) =>
            {
                _brightDebouncer.Stop();
                if (_isUpdatingUi || _device == null || !_isPowerOn) return;
                int val = (int)SliderBright.Value;
                SyncBrightnessUi(val);
                await SetBrightnessAsync(val);
            };

            SliderBright.PropertyChanged += (s, e) =>
            {
                if (e.Property.Name == nameof(Slider.Value) && !_isUpdatingUi)
                {
                    int val = (int)SliderBright.Value;
                    SyncBrightnessUi(val);
                    _brightDebouncer.Stop();
                    _brightDebouncer.Start();
                }
            };

            SliderCompactBright.PropertyChanged += (s, e) =>
            {
                if (e.Property.Name == nameof(Slider.Value) && !_isUpdatingUi)
                {
                    int val = (int)SliderCompactBright.Value;
                    SyncBrightnessUi(val);
                    _brightDebouncer.Stop();
                    _brightDebouncer.Start();
                }
            };

            // Temperature Debouncers
            _tempDebouncer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _tempDebouncer.Tick += async (s, e) =>
            {
                _tempDebouncer.Stop();
                if (_isUpdatingUi || _device == null || !_isPowerOn) return;
                int val = (int)SliderTemp.Value;
                SyncTempUi(val);
                await SetColorTempAsync(val);
            };

            SliderTemp.PropertyChanged += (s, e) =>
            {
                if (e.Property.Name == nameof(Slider.Value) && !_isUpdatingUi)
                {
                    int val = (int)SliderTemp.Value;
                    SyncTempUi(val);
                    _tempDebouncer.Stop();
                    _tempDebouncer.Start();
                }
            };

            SliderCompactTemp.PropertyChanged += (s, e) =>
            {
                if (e.Property.Name == nameof(Slider.Value) && !_isUpdatingUi)
                {
                    int val = (int)SliderCompactTemp.Value;
                    SyncTempUi(val);
                    _tempDebouncer.Stop();
                    _tempDebouncer.Start();
                }
            };

            // Quick Brightness Jumps
            BtnB1.Click += async (s, e) => await QuickSetBrightnessAsync(1);
            BtnB25.Click += async (s, e) => await QuickSetBrightnessAsync(25);
            BtnB50.Click += async (s, e) => await QuickSetBrightnessAsync(50);
            BtnB75.Click += async (s, e) => await QuickSetBrightnessAsync(75);
            BtnB100.Click += async (s, e) => await QuickSetBrightnessAsync(100);

            BtnCompactB1.Click += async (s, e) => await QuickSetBrightnessAsync(1);
            BtnCompactB50.Click += async (s, e) => await QuickSetBrightnessAsync(50);
            BtnCompactB100.Click += async (s, e) => await QuickSetBrightnessAsync(100);

            // Quick Color Temp Jumps
            BtnT2700.Click += async (s, e) => await QuickSetTempAsync(2700);
            BtnT4000.Click += async (s, e) => await QuickSetTempAsync(4000);
            BtnT6500.Click += async (s, e) => await QuickSetTempAsync(6500);

            BtnCompactT2700.Click += async (s, e) => await QuickSetTempAsync(2700);
            BtnCompactT4000.Click += async (s, e) => await QuickSetTempAsync(4000);
            BtnCompactT6500.Click += async (s, e) => await QuickSetTempAsync(6500);

            // Color Swatches
            foreach (var child in PnlColorSwatches.Children)
            {
                if (child is Button btn && btn.Tag is string hex)
                {
                    btn.Click += async (s, e) => await SetColorHexAsync(hex);
                }
            }

            BtnApplyHex.Click += async (s, e) => await SetColorHexAsync(TxtHexCode.Text ?? "#00E5FF");
            BtnColorStudio.Click += async (s, e) =>
            {
                var rand = new Random();
                string[] palette = { "#FF1744", "#FF5252", "#FF9100", "#FFEA00", "#00E676", "#00E5FF", "#2979FF", "#651FFF", "#D500F9" };
                string chosen = palette[rand.Next(palette.Length)];
                TxtHexCode.Text = chosen;
                await SetColorHexAsync(chosen);
            };

            // Lighting Scenes
            SceneNight.Click += async (s, e) => await SetSceneAsync("night", 5, 2700);
            SceneRead.Click += async (s, e) => await SetSceneAsync("reading", 100, 4000);
            SceneFocus.Click += async (s, e) => await SetSceneAsync("working", 100, 6500);
            SceneRelax.Click += async (s, e) => await SetSceneAsync("relax", 50, 3000);
            SceneParty.Click += async (s, e) => await SetSceneAsync("party", 100, 4000, "#FF0080");
            SceneCycle.Click += async (s, e) => await SetSceneAsync("rainbow", 100, 4000, "#00E5FF");

            // Sleep Timers
            BtnTimer15.Click += (s, e) => StartSleepTimer(15);
            BtnTimer30.Click += (s, e) => StartSleepTimer(30);
            BtnTimer45.Click += (s, e) => StartSleepTimer(45);
            BtnTimer60.Click += (s, e) => StartSleepTimer(60);
            BtnTimerOff.Click += (s, e) => CancelSleepTimer();
            BtnSetCustomTimer.Click += (s, e) =>
            {
                if (int.TryParse(TxtCustomTimerMins.Text, out int mins) && mins > 0)
                {
                    StartSleepTimer(mins);
                }
            };
        }

        private void SyncBrightnessUi(int val)
        {
            _isUpdatingUi = true;
            SliderBright.Value = val;
            SliderCompactBright.Value = val;
            TxtBrightVal.Text = $"{val}%";
            TxtCompactBrightVal.Text = $"{val}%";
            _isUpdatingUi = false;
        }

        private void SyncTempUi(int val)
        {
            _isUpdatingUi = true;
            SliderTemp.Value = val;
            SliderCompactTemp.Value = val;
            TxtTempVal.Text = $"{val}K";
            TxtCompactTempVal.Text = $"{val}K";
            _isUpdatingUi = false;
        }

        private void ParseAndUpdateUi(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("dps", out var dps))
                {
                    if (dps.TryGetProperty("20", out var p20)) _isPowerOn = p20.GetBoolean();
                    if (dps.TryGetProperty("21", out var p21)) _currentMode = p21.GetString() ?? "white";

                    if (dps.TryGetProperty("22", out var p22) && p22.TryGetInt32(out int rawB))
                    {
                        int pct = Math.Clamp((int)Math.Round(rawB / 10.0), 1, 100);
                        SyncBrightnessUi(pct);
                    }

                    if (dps.TryGetProperty("23", out var p23) && p23.TryGetInt32(out int rawT))
                    {
                        int k = 2700 + (int)Math.Round(rawT / 1000.0 * 3800.0);
                        k = Math.Clamp(k, 2700, 6500);
                        SyncTempUi(k);
                    }

                    UpdatePowerButtonUi();
                }
            }
            catch { }
        }

        private void UpdatePowerButtonUi()
        {
            string txt = _isPowerOn ? LocalizationService.T("PoweredOn") : LocalizationService.T("PoweredOff");
            IBrush bg = _isPowerOn ? Brush.Parse("#10B981") : Brush.Parse("#FF1744");

            BtnPower.Content = txt;
            BtnPower.Background = bg;

            BtnCompactPower.Content = txt;
            BtnCompactPower.Background = bg;

            UpdateBulbGlow(TxtHexCode.Text ?? "#00E5FF", _isPowerOn);
        }

        private void UpdateBulbGlow(string hexCode, bool isPowerOn)
        {
            try
            {
                if (!isPowerOn)
                {
                    BulbGlowFrame.Background = Brush.Parse("#2A2A3E");
                    CompactBulbGlow.Background = Brush.Parse("#2A2A3E");
                    return;
                }

                IBrush brush;
                if (_currentMode == "white")
                {
                    int k = (int)SliderTemp.Value;
                    brush = new SolidColorBrush(KelvinToColor(k));
                }
                else
                {
                    string h = hexCode.StartsWith("#") ? hexCode : "#" + hexCode;
                    brush = Brush.Parse(h);
                }

                BulbGlowFrame.Background = brush;
                CompactBulbGlow.Background = brush;
            }
            catch { }
        }

        public static Color KelvinToColor(int kelvin)
        {
            kelvin = Math.Clamp(kelvin, 2700, 6500);
            float t = (kelvin - 2700f) / (6500f - 2700f);
            byte r, g, b;
            if (t <= 0.5f)
            {
                float localT = t / 0.5f;
                r = 255;
                g = (byte)(179 + (242 - 179) * localT);
                b = (byte)(71 + (214 - 71) * localT);
            }
            else
            {
                float localT = (t - 0.5f) / 0.5f;
                r = (byte)(255 - (255 - 212) * localT);
                g = (byte)(242 - (242 - 236) * localT);
                b = (byte)(214 + (255 - 214) * localT);
            }
            return Color.FromRgb(r, g, b);
        }

        private async Task TogglePowerAsync()
        {
            if (_device == null) return;
            var sw = Stopwatch.StartNew();

            bool nextState = !_isPowerOn;
            _isPowerOn = nextState;
            UpdatePowerButtonUi();

            bool ok = await _device.SetPowerAsync(nextState);
            sw.Stop();
            string lat = $"⚡ {sw.ElapsedMilliseconds} ms (Power Toggle)";
            TxtLatency.Text = lat;
            TxtCompactLatency.Text = lat;

            if (!ok)
            {
                _isPowerOn = !nextState;
                UpdatePowerButtonUi();
            }
        }

        private async Task SetBrightnessAsync(int percent)
        {
            if (_device == null) return;
            var sw = Stopwatch.StartNew();
            int valV2 = (int)(percent * 10.0);
            await _device.SetDpsAsync(new Dictionary<string, object>
            {
                ["20"] = true,
                ["22"] = valV2
            });
            sw.Stop();
            string lat = $"⚡ {sw.ElapsedMilliseconds} ms";
            TxtLatency.Text = lat;
            TxtCompactLatency.Text = lat;
            _isPowerOn = true;
            UpdatePowerButtonUi();
        }

        private async Task QuickSetBrightnessAsync(int percent)
        {
            SyncBrightnessUi(percent);
            await SetBrightnessAsync(percent);
        }

        private async Task SetColorTempAsync(int kelvin)
        {
            if (_device == null) return;
            var sw = Stopwatch.StartNew();
            int valV2 = (int)Math.Round((kelvin - 2700.0) / 3800.0 * 1000.0);
            valV2 = Math.Clamp(valV2, 0, 1000);
            _currentMode = "white";
            await _device.SetDpsAsync(new Dictionary<string, object>
            {
                ["20"] = true,
                ["21"] = "white",
                ["23"] = valV2
            });
            sw.Stop();
            string lat = $"⚡ {sw.ElapsedMilliseconds} ms";
            TxtLatency.Text = lat;
            TxtCompactLatency.Text = lat;
            _isPowerOn = true;
            UpdatePowerButtonUi();
        }

        private async Task QuickSetTempAsync(int kelvin)
        {
            SyncTempUi(kelvin);
            await SetColorTempAsync(kelvin);
        }

        private async Task SetColorHexAsync(string hex)
        {
            if (_device == null) return;
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            TxtHexCode.Text = "#" + hex;
            _currentMode = "colour";

            var sw = Stopwatch.StartNew();
            string tuyaHex = HexToTuyaColor(hex);
            await _device.SetDpsAsync(new Dictionary<string, object>
            {
                ["20"] = true,
                ["21"] = "colour",
                ["24"] = tuyaHex
            });
            sw.Stop();
            string lat = $"⚡ {sw.ElapsedMilliseconds} ms (Color)";
            TxtLatency.Text = lat;
            TxtCompactLatency.Text = lat;
            _isPowerOn = true;
            UpdatePowerButtonUi();
        }

        private static string HexToTuyaColor(string hex)
        {
            if (hex.Length != 6) return "000003e803e8";
            int r = Convert.ToInt32(hex[..2], 16), g = Convert.ToInt32(hex[2..4], 16), b = Convert.ToInt32(hex[4..6], 16);
            float rF = r / 255f, gF = g / 255f, bF = b / 255f;
            float max = Math.Max(rF, Math.Max(gF, bF)), min = Math.Min(rF, Math.Min(gF, bF));
            float d = max - min, h = 0f;
            if (d > 0.0001f)
            {
                if (max == rF) h = (gF - bF) / d % 6f;
                else if (max == gF) h = (bF - rF) / d + 2f;
                else h = (rF - gF) / d + 4f;
                h *= 60f;
                if (h < 0f) h += 360f;
            }
            float s = max == 0f ? 0f : d / max;
            return $"{(int)Math.Round(h):x4}{(int)Math.Round(s * 1000f):x4}{(int)Math.Round(max * 1000f):x4}";
        }

        private async Task SetSceneAsync(string sceneName, int bright, int temp, string? hex = null)
        {
            if (hex != null)
            {
                await SetColorHexAsync(hex);
            }
            else
            {
                SyncBrightnessUi(bright);
                SyncTempUi(temp);

                int valB = (int)(bright * 10.0);
                int valT = (int)Math.Round((temp - 2700.0) / 3800.0 * 1000.0);
                _currentMode = "white";
                await _device!.SetDpsAsync(new Dictionary<string, object>
                {
                    ["20"] = true,
                    ["21"] = "white",
                    ["22"] = valB,
                    ["23"] = valT
                });
                _isPowerOn = true;
                UpdatePowerButtonUi();
            }
        }

        private void StartSleepTimer(int minutes)
        {
            _sleepSecondsRemaining = minutes * 60;
            _sleepTimer?.Stop();
            _sleepTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _sleepTimer.Tick += async (s, e) =>
            {
                _sleepSecondsRemaining--;
                if (_sleepSecondsRemaining <= 0)
                {
                    _sleepTimer.Stop();
                    TxtTimerStatus.Text = LocalizationService.T("Off");
                    if (_isPowerOn) await TogglePowerAsync();
                }
                else
                {
                    int m = _sleepSecondsRemaining / 60;
                    int sec = _sleepSecondsRemaining % 60;
                    TxtTimerStatus.Text = $"{m:D2}:{sec:D2}";
                }
            };
            _sleepTimer.Start();
            TxtTimerStatus.Text = $"{minutes}m";
        }

        private void CancelSleepTimer()
        {
            _sleepTimer?.Stop();
            _sleepSecondsRemaining = 0;
            TxtTimerStatus.Text = LocalizationService.T("Off");
        }
    }
}
