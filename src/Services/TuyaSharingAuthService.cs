// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SmartLampApp.Services
{
    public partial class TuyaSharingAuthService
    {
        private const string GatewayHost = "https://apigw.iotbing.com";
        public const string DefaultClientId = "HA_3y9q4ak7g4ephrvke";
        public const string DefaultAppSchema = "haauthorize";

        private readonly HttpClient _http;

        public TuyaSharingAuthService(HttpClient? httpClient = null)
        {
            _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        }

        public static void LogDiagnostic(string message)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartLampApp");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "tuya_sharing_debug.log");
                string line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                File.AppendAllText(path, line);
            }
            catch { }
        }

        public static string GetDiagnosticLogPath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartLampApp");
            return Path.Combine(dir, "tuya_sharing_debug.log");
        }

        public async Task<TuyaQrCodeToken> RequestQrCodeAsync(string userCode, string schema = "smartlife", string clientId = DefaultClientId, CancellationToken ct = default)
        {
            var result = new TuyaQrCodeToken();
            try
            {
                string url = $"{GatewayHost}/v1.0/m/life/home-assistant/qrcode/tokens?clientid={clientId}&usercode={userCode}&schema={DefaultAppSchema}";
                LogDiagnostic($"RequestQrCodeAsync -> {url}");

                using var response = await _http.PostAsync(url, null, ct).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                LogDiagnostic($"RequestQrCodeAsync (status {(int)response.StatusCode}) <- {json}");

                using var doc = JsonDocument.Parse(json);
                bool success = doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();
                result.Success = success;

                if (success && doc.RootElement.TryGetProperty("result", out var resElem))
                {
                    string rawToken = GetPropString(resElem, "qrcode", "token", "code");
                    result.Token = rawToken;

                    if (string.IsNullOrEmpty(rawToken))
                    {
                        result.Success = false;
                        result.ErrorMsg = "No token returned from Tuya gateway.";
                    }
                    else if (rawToken.Contains("--qrLogin") || rawToken.StartsWith("http"))
                    {
                        result.QrCodeContent = rawToken;
                    }
                    else
                    {
                        string appPrefix = string.IsNullOrWhiteSpace(schema) ? "smartlife" : schema;
                        result.QrCodeContent = $"{appPrefix}--qrLogin?token={rawToken}";
                    }
                }
                else
                {
                    result.ErrorCode = GetPropString(doc.RootElement, "code");
                    result.ErrorMsg = GetPropString(doc.RootElement, "msg");
                    if (string.IsNullOrEmpty(result.ErrorMsg)) result.ErrorMsg = result.ErrorCode;
                }
            }
            catch (Exception ex)
            {
                LogDiagnostic($"RequestQrCodeAsync exception: {ex}");
                result.Success = false;
                result.ErrorMsg = ex.Message;
            }
            return result;
        }

        public async Task<(bool Scanned, TuyaSharingSession? Session, string? Error)> PollLoginResultAsync(
            string token, string userCode, string clientId = DefaultClientId, CancellationToken ct = default)
        {
            try
            {
                string url = $"{GatewayHost}/v1.0/m/life/home-assistant/qrcode/tokens/{token}?clientid={clientId}&usercode={userCode}";
                using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                using var doc = JsonDocument.Parse(json);
                bool success = doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();

                if (success && doc.RootElement.TryGetProperty("result", out var resElem))
                {
                    LogDiagnostic($"PollLoginResult confirmed: {json}");
                    var session = new TuyaSharingSession
                    {
                        ClientId = clientId,
                        UserCode = userCode,
                        AccessToken = GetPropString(resElem, "access_token", "accessToken"),
                        RefreshToken = GetPropString(resElem, "refresh_token", "refreshToken"),
                        ExpireTime = GetPropLong(resElem, "expire_time", "expireTime"),
                        Uid = GetPropString(resElem, "uid")
                    };

                    string ep = GetPropString(resElem, "endpoint", "end_point");
                    session.Endpoint = NormalizeEndpoint(ep);

                    LogDiagnostic($"Session created: uid={session.Uid}, ep={session.Endpoint}, tokenLen={session.AccessToken.Length}, refreshLen={session.RefreshToken.Length}");
                    return (true, session, null);
                }

                string errCode = GetPropString(doc.RootElement, "code");
                string errMsg = GetPropString(doc.RootElement, "msg");
                return (false, null, $"{errCode}: {errMsg}");
            }
            catch (Exception ex)
            {
                LogDiagnostic($"PollLoginResult exception: {ex}");
                return (false, null, ex.Message);
            }
        }

        public static string NormalizeEndpoint(string? ep)
        {
            if (string.IsNullOrWhiteSpace(ep)) return "https://apigw.tuyaeu.com";
            string res = ep.Trim();
            if (!res.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !res.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                res = "https://" + res;
            }
            if (res.Contains("openapi."))
            {
                res = res.Replace("openapi.", "apigw.");
            }
            return res.TrimEnd('/');
        }

        public async Task<List<TuyaSharingDiscoveredDevice>> QueryUserDevicesAsync(
            TuyaSharingSession session, IEnumerable<string>? knownDeviceIds = null, CancellationToken ct = default)
        {
            var devices = new List<TuyaSharingDiscoveredDevice>();
            LogDiagnostic($"Starting QueryUserDevicesAsync for uid={session.Uid}...");

            // 1. Get Homes
            var homes = await QueryHomesAsync(session, ct).ConfigureAwait(false);

            // 2. Query devices per home
            foreach (var home in homes)
            {
                var homeDevices = await QueryDevicesByHomeAsync(session, home.Key, home.Value, ct).ConfigureAwait(false);
                foreach (var d in homeDevices)
                {
                    if (!devices.Any(x => x.Id == d.Id)) devices.Add(d);
                }
            }

            // 3. Fallback: query without homeId if homes returned nothing
            if (devices.Count == 0)
            {
                LogDiagnostic("No devices found in homes, querying default home...");
                var directDevices = await QueryDevicesByHomeAsync(session, "", "My Home", ct).ConfigureAwait(false);
                foreach (var d in directDevices)
                {
                    if (!devices.Any(x => x.Id == d.Id)) devices.Add(d);
                }
            }

            // 4. Fallback: if knownDeviceIds specified, query /v1.0/m/life/ha/devices/detail
            if (knownDeviceIds != null)
            {
                var missingIds = knownDeviceIds.Where(id => !string.IsNullOrWhiteSpace(id) && !devices.Any(d => d.Id == id)).Distinct().ToList();
                if (missingIds.Count > 0)
                {
                    LogDiagnostic($"Querying devices by known IDs: {string.Join(", ", missingIds)}");
                    var idDevices = await QueryDevicesByIdsAsync(session, missingIds, "Default", ct).ConfigureAwait(false);
                    foreach (var d in idDevices)
                    {
                        if (!devices.Any(x => x.Id == d.Id)) devices.Add(d);
                    }
                }
            }

            LogDiagnostic($"QueryUserDevicesAsync finished: {devices.Count} total devices.");
            return devices;
        }

        public async Task<List<KeyValuePair<string, string>>> QueryHomesAsync(TuyaSharingSession session, CancellationToken ct = default)
        {
            try
            {
                string jsonResp = await ExecuteSignedRequestAsync(session, HttpMethod.Get, "/v1.0/m/life/users/homes", null, null, ct).ConfigureAwait(false);
                var homes = ParseHomesArray(jsonResp);
                LogDiagnostic($"QueryHomesAsync found {homes.Count} homes.");
                return homes;
            }
            catch (Exception ex)
            {
                LogDiagnostic($"QueryHomesAsync exception: {ex}");
                return new List<KeyValuePair<string, string>>();
            }
        }

        public async Task<List<TuyaSharingDiscoveredDevice>> QueryDevicesByHomeAsync(
            TuyaSharingSession session, string homeId, string homeName, CancellationToken ct = default)
        {
            try
            {
                var queryParams = !string.IsNullOrEmpty(homeId) ? new Dictionary<string, string> { { "homeId", homeId } } : null;
                string jsonResp = await ExecuteSignedRequestAsync(session, HttpMethod.Get, "/v1.0/m/life/ha/home/devices", queryParams, null, ct).ConfigureAwait(false);
                var devs = ParseDevicesArray(jsonResp, homeName);
                LogDiagnostic($"QueryDevicesByHomeAsync (homeId={homeId}) found {devs.Count} devices.");
                return devs;
            }
            catch (Exception ex)
            {
                LogDiagnostic($"QueryDevicesByHomeAsync exception: {ex}");
                return new List<TuyaSharingDiscoveredDevice>();
            }
        }

        public async Task<List<TuyaSharingDiscoveredDevice>> QueryDevicesByIdsAsync(
            TuyaSharingSession session, IEnumerable<string> deviceIds, string homeName = "Imported", CancellationToken ct = default)
        {
            var list = new List<TuyaSharingDiscoveredDevice>();
            var ids = deviceIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            if (ids.Count == 0) return list;

            try
            {
                var queryParams = new Dictionary<string, string> { { "devIds", string.Join(",", ids) } };
                string jsonResp = await ExecuteSignedRequestAsync(session, HttpMethod.Get, "/v1.0/m/life/ha/devices/detail", queryParams, null, ct).ConfigureAwait(false);
                var devs = ParseDevicesArray(jsonResp, homeName);
                LogDiagnostic($"QueryDevicesByIdsAsync found {devs.Count} devices.");
                return devs;
            }
            catch (Exception ex)
            {
                LogDiagnostic($"QueryDevicesByIdsAsync exception: {ex}");
                return list;
            }
        }

        public static List<KeyValuePair<string, string>> ParseHomesArray(string jsonResp)
        {
            var homes = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrWhiteSpace(jsonResp)) return homes;

            try
            {
                using var doc = JsonDocument.Parse(jsonResp);
                JsonElement arrayElem = default;

                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    arrayElem = doc.RootElement;
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("result", out var r))
                {
                    if (r.ValueKind == JsonValueKind.Array) arrayElem = r;
                }

                if (arrayElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var h in arrayElem.EnumerateArray())
                    {
                        string ownerId = GetPropString(h, "ownerId", "owner_id", "homeId", "home_id", "id");
                        string name = GetPropString(h, "name", "home_name");
                        if (string.IsNullOrEmpty(name)) name = "Home";

                        if (!string.IsNullOrEmpty(ownerId))
                        {
                            homes.Add(new KeyValuePair<string, string>(ownerId, name));
                        }
                    }
                }
            }
            catch { }

            return homes;
        }

        public static List<TuyaSharingDiscoveredDevice> ParseDevicesArray(string jsonResp, string homeName = "Imported")
        {
            var list = new List<TuyaSharingDiscoveredDevice>();
            if (string.IsNullOrWhiteSpace(jsonResp)) return list;

            try
            {
                using var doc = JsonDocument.Parse(jsonResp);
                JsonElement arrayElem = default;

                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    arrayElem = doc.RootElement;
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("result", out var r))
                {
                    if (r.ValueKind == JsonValueKind.Array)
                    {
                        arrayElem = r;
                    }
                    else if (r.ValueKind == JsonValueKind.Object)
                    {
                        if (r.TryGetProperty("list", out var l) && l.ValueKind == JsonValueKind.Array) arrayElem = l;
                        else if (r.TryGetProperty("devices", out var d) && d.ValueKind == JsonValueKind.Array) arrayElem = d;
                    }
                }

                if (arrayElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var d in arrayElem.EnumerateArray())
                    {
                        string id = GetPropString(d, "id", "devId", "dev_id");
                        string name = GetPropString(d, "name", "dev_name", "custom_name");
                        string localKey = GetPropString(d, "local_key", "localKey", "key");
                        string cat = GetPropString(d, "category", "product_category");
                        string pid = GetPropString(d, "product_id", "productId");
                        string pname = GetPropString(d, "product_name", "productName");
                        string ip = GetPropString(d, "ip");
                        bool online = d.TryGetProperty("online", out var onProp) && (onProp.ValueKind == JsonValueKind.True || (onProp.ValueKind == JsonValueKind.String && onProp.GetString() == "true"));

                        if (!string.IsNullOrEmpty(id))
                        {
                            list.Add(new TuyaSharingDiscoveredDevice
                            {
                                HomeName = homeName,
                                Id = id,
                                Name = string.IsNullOrWhiteSpace(name) ? "Smart Lamp" : name,
                                LocalKey = localKey,
                                Category = cat,
                                ProductId = pid,
                                ProductName = pname,
                                Ip = ip,
                                Online = online
                            });
                        }
                    }
                }
            }
            catch { }

            return list;
        }
    }
}
