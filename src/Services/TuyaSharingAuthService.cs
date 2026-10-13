// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SmartLampApp.Services
{
    public class TuyaSharingAuthService
    {
        private const string GatewayHost = "https://apigw.iotbing.com";
        public const string DefaultClientId = "HA_3y9q4ak7g4ephrvke";

        private readonly HttpClient _http;

        public TuyaSharingAuthService(HttpClient? httpClient = null)
        {
            _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        }

        public async Task<TuyaQrCodeToken> RequestQrCodeAsync(string userCode, string schema = "smartlife", string clientId = DefaultClientId, CancellationToken ct = default)
        {
            var result = new TuyaQrCodeToken();
            try
            {
                string url = $"{GatewayHost}/v1.0/m/life/home-assistant/qrcode/tokens?clientid={clientId}&usercode={userCode}&schema={schema}";
                using var response = await _http.PostAsync(url, null, ct).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                using var doc = JsonDocument.Parse(json);
                bool success = doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();
                result.Success = success;

                if (success && doc.RootElement.TryGetProperty("result", out var resElem))
                {
                    string rawToken = "";
                    if (resElem.TryGetProperty("qrcode", out var qrProp)) rawToken = qrProp.GetString() ?? "";
                    else if (resElem.TryGetProperty("token", out var tokProp)) rawToken = tokProp.GetString() ?? "";
                    else if (resElem.TryGetProperty("code", out var codeProp)) rawToken = codeProp.GetString() ?? "";

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
                        result.QrCodeContent = $"{schema}--qrLogin?token={rawToken}";
                    }
                }
                else
                {
                    if (doc.RootElement.TryGetProperty("code", out var c)) result.ErrorCode = c.GetString() ?? "";
                    if (doc.RootElement.TryGetProperty("msg", out var m)) result.ErrorMsg = m.GetString() ?? "";
                    if (string.IsNullOrEmpty(result.ErrorMsg)) result.ErrorMsg = result.ErrorCode;
                }
            }
            catch (Exception ex)
            {
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
                    var session = new TuyaSharingSession
                    {
                        ClientId = clientId,
                        UserCode = userCode
                    };

                    if (resElem.TryGetProperty("accessToken", out var at)) session.AccessToken = at.GetString() ?? "";
                    if (resElem.TryGetProperty("refreshToken", out var rt)) session.RefreshToken = rt.GetString() ?? "";
                    if (resElem.TryGetProperty("expireTime", out var exp)) session.ExpireTime = exp.GetInt64();
                    if (resElem.TryGetProperty("uid", out var uid)) session.Uid = uid.GetString() ?? "";
                    if (resElem.TryGetProperty("endpoint", out var ep)) session.Endpoint = ep.GetString() ?? "https://openapi.tuyaeu.com";

                    return (true, session, null);
                }

                string errCode = doc.RootElement.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "";
                string errMsg = doc.RootElement.TryGetProperty("msg", out var m) ? m.GetString() ?? "" : "";
                return (false, null, $"{errCode}: {errMsg}");
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        public async Task<List<TuyaSharingDiscoveredDevice>> QueryUserDevicesAsync(
            TuyaSharingSession session, IEnumerable<string>? knownDeviceIds = null, CancellationToken ct = default)
        {
            var devices = new List<TuyaSharingDiscoveredDevice>();

            // 1. Get Homes
            var homes = await QueryHomesAsync(session, ct).ConfigureAwait(false);

            // 2. Query devices per home
            foreach (var home in homes)
            {
                var homeDevices = await QueryDevicesByHomeAsync(session, home.Key, home.Value, ct).ConfigureAwait(false);
                foreach (var d in homeDevices)
                {
                    if (!devices.Any(x => x.Id == d.Id))
                    {
                        devices.Add(d);
                    }
                }
            }

            // 3. Fallback: query without homeId if homes returned nothing
            if (devices.Count == 0)
            {
                var directDevices = await QueryDevicesByHomeAsync(session, "", "My Home", ct).ConfigureAwait(false);
                foreach (var d in directDevices)
                {
                    if (!devices.Any(x => x.Id == d.Id))
                    {
                        devices.Add(d);
                    }
                }
            }

            // 4. Fallback: if knownDeviceIds specified, query /v1.0/m/life/ha/devices/detail
            if (knownDeviceIds != null)
            {
                var missingIds = knownDeviceIds.Where(id => !string.IsNullOrWhiteSpace(id) && !devices.Any(d => d.Id == id)).ToList();
                if (missingIds.Count > 0)
                {
                    var idDevices = await QueryDevicesByIdsAsync(session, missingIds, "Default", ct).ConfigureAwait(false);
                    foreach (var d in idDevices)
                    {
                        if (!devices.Any(x => x.Id == d.Id))
                        {
                            devices.Add(d);
                        }
                    }
                }
            }

            return devices;
        }

        public async Task<List<KeyValuePair<string, string>>> QueryHomesAsync(TuyaSharingSession session, CancellationToken ct = default)
        {
            try
            {
                string jsonResp = await ExecuteSignedRequestAsync(session, HttpMethod.Get, "/v1.0/m/life/users/homes", null, null, ct).ConfigureAwait(false);
                return ParseHomesArray(jsonResp);
            }
            catch
            {
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
                return ParseDevicesArray(jsonResp, homeName);
            }
            catch
            {
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
                return ParseDevicesArray(jsonResp, homeName);
            }
            catch
            {
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
                        string ownerId = "";
                        if (h.TryGetProperty("ownerId", out var oid)) ownerId = oid.ValueKind == JsonValueKind.String ? oid.GetString() ?? "" : oid.ToString();
                        if (string.IsNullOrEmpty(ownerId) && h.TryGetProperty("homeId", out var hid)) ownerId = hid.ValueKind == JsonValueKind.String ? hid.GetString() ?? "" : hid.ToString();
                        if (string.IsNullOrEmpty(ownerId) && h.TryGetProperty("id", out var id)) ownerId = id.ValueKind == JsonValueKind.String ? id.GetString() ?? "" : id.ToString();

                        string name = h.TryGetProperty("name", out var n) ? n.GetString() ?? "Home" : "Home";
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
                        string id = "";
                        if (d.TryGetProperty("id", out var idProp)) id = idProp.GetString() ?? "";
                        else if (d.TryGetProperty("devId", out var idProp2)) id = idProp2.GetString() ?? "";
                        else if (d.TryGetProperty("dev_id", out var idProp3)) id = idProp3.GetString() ?? "";

                        string name = "";
                        if (d.TryGetProperty("name", out var nameProp)) name = nameProp.GetString() ?? "";
                        else if (d.TryGetProperty("dev_name", out var nameProp2)) name = nameProp2.GetString() ?? "";

                        string localKey = "";
                        if (d.TryGetProperty("local_key", out var keyProp)) localKey = keyProp.GetString() ?? "";
                        else if (d.TryGetProperty("localKey", out var keyProp2)) localKey = keyProp2.GetString() ?? "";
                        else if (d.TryGetProperty("key", out var keyProp3)) localKey = keyProp3.GetString() ?? "";

                        string cat = d.TryGetProperty("category", out var catProp) ? catProp.GetString() ?? "" : "";
                        string pid = d.TryGetProperty("product_id", out var pidProp) ? pidProp.GetString() ?? "" : "";
                        string pname = d.TryGetProperty("product_name", out var pnProp) ? pnProp.GetString() ?? "" : "";
                        string ip = d.TryGetProperty("ip", out var ipProp) ? ipProp.GetString() ?? "" : "";
                        bool online = d.TryGetProperty("online", out var onProp) && onProp.GetBoolean();

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

        private async Task<string> ExecuteSignedRequestAsync(
            TuyaSharingSession session,
            HttpMethod method,
            string path,
            Dictionary<string, string>? queryParams,
            string? jsonBody,
            CancellationToken ct)
        {
            string rid = Guid.NewGuid().ToString();
            string sid = "";

            using var md5 = MD5.Create();
            byte[] hashKeyBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(rid + session.RefreshToken));
            string hashKey = Convert.ToHexStringLower(hashKeyBytes);
            string secret = TuyaSharingCrypto.ComputeSecret(rid, sid, hashKey);

            string queryEncData = "";
            string requestUri = session.Endpoint.TrimEnd('/') + path;

            if (queryParams != null && queryParams.Count > 0)
            {
                string rawQueryJson = JsonSerializer.Serialize(queryParams);
                queryEncData = TuyaSharingCrypto.EncryptAesGcm(rawQueryJson, secret);
                requestUri += $"?encdata={Uri.EscapeDataString(queryEncData)}";
            }

            string bodyEncData = "";
            string? requestContent = null;
            if (!string.IsNullOrEmpty(jsonBody))
            {
                bodyEncData = TuyaSharingCrypto.EncryptAesGcm(jsonBody, secret);
                var bodyObj = new Dictionary<string, string> { { "encdata", bodyEncData } };
                requestContent = JsonSerializer.Serialize(bodyObj);
            }

            long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var headers = new Dictionary<string, string>
            {
                { "X-appKey", session.ClientId },
                { "X-requestId", rid },
                { "X-sid", sid },
                { "X-time", t.ToString() }
            };
            if (!string.IsNullOrEmpty(session.AccessToken))
            {
                headers["X-token"] = session.AccessToken;
            }

            string sign = TuyaSharingCrypto.ComputeRestfulSign(hashKey, queryEncData, bodyEncData, headers);
            headers["X-sign"] = sign;

            using var req = new HttpRequestMessage(method, requestUri);
            foreach (var kv in headers) req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);

            if (requestContent != null)
            {
                req.Content = new StringContent(requestContent, Encoding.UTF8, "application/json");
            }

            using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
            string rawContent = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            try
            {
                using var doc = JsonDocument.Parse(rawContent);
                if (doc.RootElement.TryGetProperty("result", out var resElem) && resElem.ValueKind == JsonValueKind.String)
                {
                    string cipherBase64 = resElem.GetString() ?? "";
                    if (!string.IsNullOrEmpty(cipherBase64))
                    {
                        string decrypted = TuyaSharingCrypto.DecryptAesGcm(cipherBase64, secret);
                        if (!string.IsNullOrWhiteSpace(decrypted))
                        {
                            return $"{{\"success\":true,\"result\":{decrypted}}}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TuyaSharing] Error processing decrypted response: {ex.Message}");
            }

            return rawContent;
        }
    }
}
