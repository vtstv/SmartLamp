// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using SmartLampApp.Models;

namespace SmartLampApp.Services
{
    public partial class TuyaProtocol
    {
        private static string? _cachedCloudToken = null;
        private static DateTime _cloudTokenExpiry = DateTime.MinValue;
        private static readonly SemaphoreSlim _tokenLock = new SemaphoreSlim(1, 1);

        private static string GetTuyaBaseUrl(string region)
        {
            return (region != null ? region.ToLowerInvariant() : "") switch
            {
                "us" => "https://openapi.tuyaus.com",
                "cn" => "https://openapi.tuyacn.com",
                "in" => "https://openapi.tuyain.com",
                _ => "https://openapi.tuyaeu.com"
            };
        }

        private static string CalculateSha256Hex(string input)
        {
            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static string CalculateHmacSha256Upper(string secret, string message)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) sb.Append(b.ToString("X2"));
            return sb.ToString();
        }

        private async Task<string?> GetCloudTokenAsync(string accessId, string accessSecret, string region)
        {
            if (!string.IsNullOrEmpty(_cachedCloudToken) && DateTime.UtcNow < _cloudTokenExpiry)
            {
                return _cachedCloudToken;
            }

            await _tokenLock.WaitAsync();
            try
            {
                if (!string.IsNullOrEmpty(_cachedCloudToken) && DateTime.UtcNow < _cloudTokenExpiry)
                {
                    return _cachedCloudToken;
                }

                string baseUrl = GetTuyaBaseUrl(region);
                long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string uri = "/v1.0/token?grant_type=1";
                string emptyHash = CalculateSha256Hex("");
                string stringToSign = accessId + t + "GET\n" + emptyHash + "\n\n" + uri;
                string sign = CalculateHmacSha256Upper(accessSecret, stringToSign);

                using var req = new HttpRequestMessage(HttpMethod.Get, baseUrl + uri);
                req.Headers.Add("client_id", accessId);
                req.Headers.Add("sign", sign);
                req.Headers.Add("t", t.ToString());
                req.Headers.Add("sign_method", "HMAC-SHA256");

                var res = await _httpClient.SendAsync(req);
                if (!res.IsSuccessStatusCode) return null;

                string jsonStr = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonStr);
                var root = doc.RootElement;
                if (root.TryGetProperty("success", out var succ) && succ.GetBoolean())
                {
                    var result = root.GetProperty("result");
                    _cachedCloudToken = result.GetProperty("access_token").GetString();
                    int expireSeconds = result.TryGetProperty("expire_time", out var exp) ? exp.GetInt32() : 7200;
                    _cloudTokenExpiry = DateTime.UtcNow.AddSeconds(expireSeconds - 60);
                    return _cachedCloudToken;
                }
                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        private async Task<bool> SendDirectCloudCommandAsync(string accessId, string accessSecret, string region, string devId, List<object> commands)
        {
            try
            {
                string? token = await GetCloudTokenAsync(accessId, accessSecret, region);
                if (string.IsNullOrEmpty(token)) return false;

                string baseUrl = GetTuyaBaseUrl(region);
                long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string uri = $"/v1.0/iot-03/devices/{devId}/commands";

                var bodyObj = new { commands = commands };
                string bodyJson = JsonSerializer.Serialize(bodyObj);
                string bodyHash = CalculateSha256Hex(bodyJson);

                string stringToSign = accessId + token + t + "POST\n" + bodyHash + "\n\n" + uri;
                string sign = CalculateHmacSha256Upper(accessSecret, stringToSign);

                using var req = new HttpRequestMessage(HttpMethod.Post, baseUrl + uri)
                {
                    Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
                };
                req.Headers.Add("client_id", accessId);
                req.Headers.Add("access_token", token);
                req.Headers.Add("sign", sign);
                req.Headers.Add("t", t.ToString());
                req.Headers.Add("sign_method", "HMAC-SHA256");

                var res = await _httpClient.SendAsync(req);
                if (!res.IsSuccessStatusCode) return false;

                string jsonStr = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonStr);
                return doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> SendCloudCommandAsync(string cmdType, object? extra = null, DeviceInfo? target = null)
        {
            var dev = target ?? _activeDevice;
            if (dev == null || string.IsNullOrWhiteSpace(_config.access_id) || string.IsNullOrWhiteSpace(_config.access_key)) return false;

            string plainKey = ConfigManager.DecryptSecret(_config.access_key);
            string region = string.IsNullOrWhiteSpace(_config.region) ? "eu" : _config.region;

            // Direct C# REST call to Tuya OpenAPI for high-speed instant execution (<100ms)
            var tuyaCmds = new List<object>();

            if (cmdType == "set_power")
            {
                bool pwr = extra != null && extra.GetType().GetProperty("power")?.GetValue(extra) is bool b && b;
                tuyaCmds.Add(new { code = "switch_led", value = pwr });
            }
            else if (cmdType == "toggle")
            {
                var curStatus = await GetCloudStatusAsync(target);
                bool newPwr = !curStatus.IsPowerOn;
                tuyaCmds.Add(new { code = "switch_led", value = newPwr });
            }
            else if (cmdType == "set_brightness")
            {
                int bright = extra != null && extra.GetType().GetProperty("brightness")?.GetValue(extra) is int i ? i : 50;
                tuyaCmds.Add(new { code = "bright_value_v2", value = bright * 10 });
            }
            else if (cmdType == "set_temp")
            {
                int temp = extra != null && extra.GetType().GetProperty("temp")?.GetValue(extra) is int i ? i : 500;
                tuyaCmds.Add(new { code = "work_mode", value = "white" });
                tuyaCmds.Add(new { code = "temp_value_v2", value = temp });
            }
            else if (cmdType == "set_color")
            {
                string hex = extra != null && extra.GetType().GetProperty("hex")?.GetValue(extra) is string s ? s : "000003e803e8";
                tuyaCmds.Add(new { code = "work_mode", value = "colour" });
                int hVal = 0, sVal = 1000, vVal = 1000;
                if (hex.Length >= 12)
                {
                    try
                    {
                        hVal = Convert.ToInt32(hex.Substring(0, 4), 16);
                        sVal = Convert.ToInt32(hex.Substring(4, 4), 16);
                        vVal = Convert.ToInt32(hex.Substring(8, 4), 16);
                    }
                    catch { }
                }
                tuyaCmds.Add(new { code = "colour_data_v2", value = new { h = hVal, s = sVal, v = vVal } });
            }
            else if (cmdType == "set_preset")
            {
                string mode = extra != null && extra.GetType().GetProperty("mode")?.GetValue(extra) is string m ? m : "white";
                int brightness = extra != null && extra.GetType().GetProperty("brightness")?.GetValue(extra) is int b ? b : 50;
                int temp = extra != null && extra.GetType().GetProperty("temp")?.GetValue(extra) is int t ? t : 500;
                string hex = extra != null && extra.GetType().GetProperty("hex")?.GetValue(extra) is string s ? s : "000003e803e8";

                tuyaCmds.Add(new { code = "switch_led", value = true });
                tuyaCmds.Add(new { code = "bright_value_v2", value = brightness * 10 });

                if (mode == "colour")
                {
                    tuyaCmds.Add(new { code = "work_mode", value = "colour" });
                    int hVal = 0, sVal = 1000, vVal = 1000;
                    if (hex.Length >= 12)
                    {
                        try
                        {
                            hVal = Convert.ToInt32(hex.Substring(0, 4), 16);
                            sVal = Convert.ToInt32(hex.Substring(4, 4), 16);
                            vVal = Convert.ToInt32(hex.Substring(8, 4), 16);
                        }
                        catch { }
                    }
                    tuyaCmds.Add(new { code = "colour_data_v2", value = new { h = hVal, s = sVal, v = vVal } });
                }
                else
                {
                    tuyaCmds.Add(new { code = "work_mode", value = "white" });
                    tuyaCmds.Add(new { code = "temp_value_v2", value = temp });
                }
            }

            if (tuyaCmds.Count > 0)
            {
                bool ok = await SendDirectCloudCommandAsync(_config.access_id, plainKey, region, dev.DevId, tuyaCmds);
                if (ok) return true;
            }

            // Fallback to Python Bridge request if direct C# call didn't handle custom command
            var payloadDict = new Dictionary<string, object>
            {
                ["action"] = "cloud_command",
                ["cmd_type"] = cmdType,
                ["dev_id"] = dev.DevId,
                ["region"] = region,
                ["access_id"] = _config.access_id,
                ["access_key"] = plainKey
            };

            if (extra != null)
            {
                foreach (var prop in extra.GetType().GetProperties())
                {
                    payloadDict[prop.Name] = prop.GetValue(extra) ?? "";
                }
            }

            string output = await SendBridgeRequestAsync(payloadDict, "");
            return output.Contains("\"success\": true") || (!output.Contains("Error") && !string.IsNullOrWhiteSpace(output));
        }

        private async Task<LampStatus> GetCloudStatusAsync(DeviceInfo? target = null)
        {
            var dev = target ?? _activeDevice;
            var status = new LampStatus();
            if (dev == null || string.IsNullOrWhiteSpace(_config.access_id) || string.IsNullOrWhiteSpace(_config.access_key))
            {
                status.ErrorMessage = "Tuya Cloud credentials not set";
                status.IsOnline = false;
                return status;
            }

            string plainKey = ConfigManager.DecryptSecret(_config.access_key);
            string region = string.IsNullOrWhiteSpace(_config.region) ? "eu" : _config.region;

            try
            {
                string? token = await GetCloudTokenAsync(_config.access_id, plainKey, region);
                if (!string.IsNullOrEmpty(token))
                {
                    string baseUrl = GetTuyaBaseUrl(region);
                    long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    string uri = $"/v1.0/iot-03/devices/{dev.DevId}/status";
                    string emptyHash = CalculateSha256Hex("");

                    string stringToSign = _config.access_id + token + t + "GET\n" + emptyHash + "\n\n" + uri;
                    string sign = CalculateHmacSha256Upper(plainKey, stringToSign);

                    using var req = new HttpRequestMessage(HttpMethod.Get, baseUrl + uri);
                    req.Headers.Add("client_id", _config.access_id);
                    req.Headers.Add("access_token", token);
                    req.Headers.Add("sign", sign);
                    req.Headers.Add("t", t.ToString());
                    req.Headers.Add("sign_method", "HMAC-SHA256");

                    var res = await _httpClient.SendAsync(req);
                    if (res.IsSuccessStatusCode)
                    {
                        string jsonStr = await res.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(jsonStr);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("success", out var s) && s.GetBoolean())
                        {
                            status.IsOnline = true;
                            if (root.TryGetProperty("result", out var resultArr) && resultArr.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in resultArr.EnumerateArray())
                                {
                                    string code = item.TryGetProperty("code", out var cProp) ? cProp.GetString() ?? "" : "";
                                    var val = item.GetProperty("value");
                                    if (code == "switch_led" || code == "switch_1" || code == "20" || code == "1")
                                    {
                                        status.IsPowerOn = val.ValueKind == JsonValueKind.True || (val.ValueKind == JsonValueKind.Number && val.GetInt32() == 1);
                                    }
                                    else if (code == "work_mode" || code == "21")
                                    {
                                        status.Mode = val.GetString() ?? "white";
                                    }
                                    else if (code == "bright_value_v2" || code == "22")
                                    {
                                        status.Brightness = val.GetInt32() / 10;
                                    }
                                    else if (code == "temp_value_v2" || code == "23")
                                    {
                                        int tRaw = Math.Clamp(val.GetInt32(), 0, 1000);
                                        int kelvin = (int)Math.Round(2700.0 + (tRaw / 1000.0) * 3800.0);
                                        status.ColorTempK = Math.Clamp(kelvin, 2700, 6500);
                                    }
                                    else if (code == "colour_data_v2" || code == "24")
                                    {
                                        if (val.ValueKind == JsonValueKind.Object)
                                        {
                                            int hVal = val.TryGetProperty("h", out var hProp) ? hProp.GetInt32() : 0;
                                            int sVal = val.TryGetProperty("s", out var sProp) ? sProp.GetInt32() : 1000;
                                            int vVal = val.TryGetProperty("v", out var vProp) ? vProp.GetInt32() : 1000;
                                            status.ColorHex = TuyaHsvToRgbHex(hVal, sVal, vVal);
                                        }
                                        else if (val.ValueKind == JsonValueKind.String)
                                        {
                                            string tuyaHex = val.GetString() ?? "";
                                            if (tuyaHex.Length >= 12)
                                            {
                                                try
                                                {
                                                    int hVal = Convert.ToInt32(tuyaHex.Substring(0, 4), 16);
                                                    int sVal = Convert.ToInt32(tuyaHex.Substring(4, 4), 16);
                                                    int vVal = Convert.ToInt32(tuyaHex.Substring(8, 4), 16);
                                                    status.ColorHex = TuyaHsvToRgbHex(hVal, sVal, vVal);
                                                }
                                                catch { }
                                            }
                                        }
                                    }
                                }
                            }
                            return status;
                        }
                    }
                }
            }
            catch { }

            // Fallback to Python bridge request if direct C# call fails
            var payload = new
            {
                action = "cloud_command",
                cmd_type = "status",
                dev_id = dev.DevId,
                region = region,
                access_id = _config.access_id,
                access_key = plainKey
            };

            string output = await SendBridgeRequestAsync(payload, "");
            if (string.IsNullOrWhiteSpace(output) || output.Contains("\"error\""))
            {
                status.ErrorMessage = "Cloud status request failed";
                status.IsOnline = false;
                return status;
            }

            return ParseStatusJson(output);
        }

        public async Task<(string localKey, string error)> AutoFetchKeyFromCloudAsync(string accessId, string accessKey, string region, string devId = "")
        {
            return await FetchLocalKeyFromCloudAsync(accessId, accessKey, region, devId);
        }

        public async Task<(string localKey, string error)> FetchLocalKeyFromCloudAsync(string accessId, string accessKey, string region, string devId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string pythonCmd = $"import tinytuya, json; c=tinytuya.Cloud('{region}', '{accessId}', '{accessKey}'); print(json.dumps(c.getdevices()))";
                    string output = RunPythonCommandCLI(pythonCmd);
                    if (string.IsNullOrWhiteSpace(output) || output.Contains("error"))
                    {
                        return ("", "Failed to connect to Tuya Cloud. Verify credentials.");
                    }

                    using var doc = JsonDocument.Parse(output);
                    var root = doc.RootElement;

                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in root.EnumerateArray())
                        {
                            string id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                            if (id == devId || string.IsNullOrEmpty(devId))
                            {
                                string key = item.TryGetProperty("key", out var kProp) ? kProp.GetString() ?? "" : "";
                                if (!string.IsNullOrEmpty(key)) return (key, "");
                            }
                        }
                    }
                    return ("", "Device not found in cloud account.");
                }
                catch (Exception ex)
                {
                    return ("", ex.Message);
                }
            });
        }
    }
}
