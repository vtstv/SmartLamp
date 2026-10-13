// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System.Diagnostics;
using System.Text.Json;
using TuyaNet.Native;
using TuyaNet.Native.Discovery;
using TuyaNet.Native.Models;

namespace SmartLamp.Cli
{
    public class SimpleDeviceConfig
    {
        public string Ip { get; set; } = "";
        public string DevId { get; set; } = "";
        public string LocalKey { get; set; } = "";
        public string Version { get; set; } = "3.5";
    }

    public class AppCliConfig
    {
        public List<SimpleDeviceConfig> Devices { get; set; } = new();
        public string SelectedDevId { get; set; } = "";
    }

    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
            {
                PrintHelp();
                return 0;
            }

            if (args.Contains("--scan"))
            {
                return await HandleScanAsync();
            }

            if (args.Contains("--config-init"))
            {
                return HandleConfigInit();
            }

            var (device, err) = ResolveDevice(args);
            if (device == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Error: {err}");
                Console.ResetColor();
                return 1;
            }

            var sw = Stopwatch.StartNew();
            using var client = new TuyaDevice(device.Ip, device.DevId, device.LocalKey, device.Version);

            try
            {
                bool success = true;

                if (args.Contains("--toggle") || args.Contains("-t"))
                {
                    success = await client.TogglePowerAsync();
                    Console.WriteLine(success ? "OK: Toggled power" : "FAILED: Toggle power");
                }
                else if (args.Contains("--on"))
                {
                    success = await client.SetPowerAsync(true);
                    Console.WriteLine(success ? "OK: Lamp turned ON" : "FAILED: Turn ON");
                }
                else if (args.Contains("--off"))
                {
                    success = await client.SetPowerAsync(false);
                    Console.WriteLine(success ? "OK: Lamp turned OFF" : "FAILED: Turn OFF");
                }
                else if (TryGetArg(args, "--brightness", "-b", out string? brightStr) && int.TryParse(brightStr, out int bright))
                {
                    bright = Math.Clamp(bright, 1, 100);
                    int valV2 = (int)(bright * 10.0);
                    success = await client.SetDpsAsync(new Dictionary<string, object>
                    {
                        ["20"] = true,
                        ["22"] = valV2
                    });
                    Console.WriteLine(success ? $"OK: Brightness set to {bright}%" : "FAILED: Set brightness");
                }
                else if (TryGetArg(args, "--temp", "-k", out string? tempStr) && int.TryParse(tempStr, out int kelvin))
                {
                    kelvin = Math.Clamp(kelvin, 2700, 6500);
                    int valV2 = (int)Math.Round((kelvin - 2700.0) / 3800.0 * 1000.0);
                    valV2 = Math.Clamp(valV2, 0, 1000);
                    success = await client.SetDpsAsync(new Dictionary<string, object>
                    {
                        ["20"] = true,
                        ["21"] = "white",
                        ["23"] = valV2
                    });
                    Console.WriteLine(success ? $"OK: Color temperature set to {kelvin}K" : "FAILED: Set temperature");
                }
                else if (TryGetArg(args, "--color", "-c", out string? hexColor))
                {
                    string tuyaHex = HexToTuyaColor(hexColor.TrimStart('#'));
                    success = await client.SetDpsAsync(new Dictionary<string, object>
                    {
                        ["20"] = true,
                        ["21"] = "colour",
                        ["24"] = tuyaHex
                    });
                    Console.WriteLine(success ? $"OK: Color set to #{hexColor.TrimStart('#')}" : "FAILED: Set color");
                }
                else if (args.Contains("--status") || args.Contains("-s"))
                {
                    string statusJson = await client.GetStatusJsonAsync();
                    Console.WriteLine(statusJson);
                }
                else
                {
                    Console.Error.WriteLine("Unknown command. Run with --help for usage instructions.");
                    return 1;
                }

                if (args.Contains("--verbose") || args.Contains("-v"))
                {
                    Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds} ms");
                }

                return success ? 0 : 2;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Execution error: {ex.Message}");
                Console.ResetColor();
                return 3;
            }
        }

        private static async Task<int> HandleScanAsync()
        {
            Console.WriteLine("Scanning local network for Tuya devices (UDP 6666/6667/7000)...");
            var devices = await TuyaDiscovery.ScanAsync(TimeSpan.FromSeconds(3));
            if (devices.Count == 0)
            {
                Console.WriteLine("No Tuya devices discovered on the local network.");
                return 0;
            }

            Console.WriteLine($"Found {devices.Count} device(s):");
            foreach (var d in devices)
            {
                string name = string.IsNullOrWhiteSpace(d.ProductKey) ? "Tuya Device" : d.ProductKey;
                Console.WriteLine($" - IP: {d.Ip,-15} | Name: {name,-22} | DevID: {d.DeviceId} | Version: {d.Version}");
            }
            return 0;
        }

        private static int HandleConfigInit()
        {
            string configPath = GetDefaultConfigPath();
            string? dir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (File.Exists(configPath))
            {
                Console.WriteLine($"Config file already exists at: {configPath}");
                return 0;
            }

            var sampleConfig = new AppCliConfig
            {
                SelectedDevId = "sample_device_id",
                Devices = new List<SimpleDeviceConfig>
                {
                    new()
                    {
                        Ip = "192.168.1.100",
                        DevId = "sample_device_id",
                        LocalKey = "your_16_byte_local_key",
                        Version = "3.5"
                    }
                }
            };

            string json = JsonSerializer.Serialize(sampleConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
            Console.WriteLine($"Created initial config file: {configPath}");
            return 0;
        }

        private static (SimpleDeviceConfig? dev, string? err) ResolveDevice(string[] args)
        {
            // Direct CLI override
            if (TryGetArg(args, "--ip", null, out string? ip) &&
                TryGetArg(args, "--id", null, out string? id) &&
                TryGetArg(args, "--key", null, out string? key))
            {
                TryGetArg(args, "--ver", null, out string? ver);
                return (new SimpleDeviceConfig
                {
                    Ip = ip,
                    DevId = id,
                    LocalKey = key,
                    Version = ver ?? "3.5"
                }, null);
            }

            // Load from file
            string configPath = TryGetArg(args, "--config", null, out string? customPath) ? customPath : GetDefaultConfigPath();

            if (!File.Exists(configPath))
            {
                // Fallback to current directory
                string localFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smartlamp_config.json");
                if (File.Exists(localFile)) configPath = localFile;
                else
                {
                    return (null, $"Config file not found at '{configPath}'. Use --config <path> or run --config-init");
                }
            }

            try
            {
                string json = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Support both CLI format and Desktop App format
                if (root.TryGetProperty("devices", out var devicesEl) && devicesEl.ValueKind == JsonValueKind.Array && devicesEl.GetArrayLength() > 0)
                {
                    var firstDev = devicesEl[0];
                    string devIp = firstDev.TryGetProperty("Ip", out var pIp) ? pIp.GetString() ?? "" : (firstDev.TryGetProperty("ip", out var pIp2) ? pIp2.GetString() ?? "" : "");
                    string devId = firstDev.TryGetProperty("DevId", out var pId) ? pId.GetString() ?? "" : (firstDev.TryGetProperty("devId", out var pId2) ? pId2.GetString() ?? "" : "");
                    string devKey = firstDev.TryGetProperty("LocalKey", out var pK) ? pK.GetString() ?? "" : (firstDev.TryGetProperty("localKey", out var pK2) ? pK2.GetString() ?? "" : "");
                    string devVer = firstDev.TryGetProperty("Version", out var pV) ? pV.GetString() ?? "3.5" : (firstDev.TryGetProperty("version", out var pV2) ? pV2.GetString() ?? "3.5" : "3.5");

                    if (string.IsNullOrEmpty(devIp) || string.IsNullOrEmpty(devId) || string.IsNullOrEmpty(devKey))
                    {
                        return (null, "Invalid device entry in configuration file.");
                    }

                    return (new SimpleDeviceConfig { Ip = devIp, DevId = devId, LocalKey = devKey, Version = devVer }, null);
                }
                return (null, "No devices configured in config file.");
            }
            catch (Exception ex)
            {
                return (null, $"Failed to parse config file: {ex.Message}");
            }
        }

        private static string GetDefaultConfigPath()
        {
            if (OperatingSystem.IsWindows())
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "SmartLampApp", "smartlamp_config.json");
            }
            else
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, ".config", "smartlamp", "config.json");
            }
        }

        private static bool TryGetArg(string[] args, string flag1, string? flag2, out string value)
        {
            value = "";
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals(flag1, StringComparison.OrdinalIgnoreCase) || (flag2 != null && args[i].Equals(flag2, StringComparison.OrdinalIgnoreCase)))
                {
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        value = args[i + 1];
                        return true;
                    }
                }
            }
            return false;
        }

        private static string HexToTuyaColor(string hex)
        {
            if (hex.Length != 6) return "000003e803e8";
            int r = Convert.ToInt32(hex[..2], 16);
            int g = Convert.ToInt32(hex[2..4], 16);
            int b = Convert.ToInt32(hex[4..6], 16);

            float rF = r / 255f, gF = g / 255f, bF = b / 255f;
            float max = Math.Max(rF, Math.Max(gF, bF)), min = Math.Min(rF, Math.Min(gF, bF));
            float d = max - min;
            float h = 0f;
            if (d > 0.0001f)
            {
                if (max == rF) h = (gF - bF) / d % 6f;
                else if (max == gF) h = (bF - rF) / d + 2f;
                else h = (rF - gF) / d + 4f;
                h *= 60f;
                if (h < 0f) h += 360f;
            }
            float s = max == 0f ? 0f : d / max;
            float v = max;
            return $"{(int)Math.Round(h):x4}{(int)Math.Round(s * 1000f):x4}{(int)Math.Round(v * 1000f):x4}";
        }

        private static void PrintHelp()
        {
            Console.WriteLine($@"SmartLamp CLI v{SmartLampApp.AppVersion.Version} - Cross-Platform Tuya Controller
Usage:
  smartlamp --toggle                       Toggle lamp power
  smartlamp --on                           Turn lamp ON
  smartlamp --off                          Turn lamp OFF
  smartlamp --brightness <1-100>           Set brightness percentage
  smartlamp --temp <2700-6500>             Set color temperature in Kelvin
  smartlamp --color <#HEX>                 Set RGB color (e.g. #FF0080)
  smartlamp --status                       Print current state JSON
  smartlamp --scan                         Scan network for Tuya devices
  smartlamp --config-init                  Create sample configuration file

Options:
  --config <path>                          Custom config file path
  --ip <ip> --id <devId> --key <localKey>  Direct device override
  --verbose, -v                            Print elapsed execution time");
        }
    }
}
