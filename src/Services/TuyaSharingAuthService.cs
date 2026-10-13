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
    public class TuyaQrCodeToken
    {
        public bool Success { get; set; }
        public string Token { get; set; } = "";
        public string QrCodeContent { get; set; } = "";
        public string ErrorCode { get; set; } = "";
        public string ErrorMsg { get; set; } = "";
    }

    public class TuyaSharingSession
    {
        public string ClientId { get; set; } = "HA_3y9q4ak7g4ephrvke";
        public string UserCode { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public long ExpireTime { get; set; }
        public string Uid { get; set; } = "";
        public string Endpoint { get; set; } = "https://openapi.tuyaeu.com";
    }

    public class TuyaSharingDiscoveredDevice
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string LocalKey { get; set; } = "";
        public string Category { get; set; } = "";
        public string ProductId { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string Ip { get; set; } = "";
        public bool Online { get; set; }
        public string HomeName { get; set; } = "";
    }

    public static class TuyaSharingCrypto
    {
        private const string Chars = "ABCDEFGHJKMNPQRSTWXYZabcdefhijkmnprstwxyz2345678";

        public static string GenerateRandomNonce(int length = 12)
        {
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                int idx = RandomNumberGenerator.GetInt32(Chars.Length);
                sb.Append(Chars[idx]);
            }
            return sb.ToString();
        }

        public static string ComputeSecret(string rid, string sid, string hashKey)
        {
            string message = hashKey;
            const int mod = 16;
            if (!string.IsNullOrEmpty(sid))
            {
                int len = Math.Min(sid.Length, mod);
                var ecode = new StringBuilder();
                for (int i = 0; i < len; i++)
                {
                    int idx = sid[i] % mod;
                    ecode.Append(sid[idx % sid.Length]);
                }
                message += "_" + ecode.ToString();
            }

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(rid));
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            string hex = Convert.ToHexStringLower(hash);
            return hex.Length >= 16 ? hex.Substring(0, 16) : hex;
        }

        public static string ComputeRestfulSign(string hashKey, string queryEncData, string bodyEncData, Dictionary<string, string> headers)
        {
            string[] headerKeys = { "X-appKey", "X-requestId", "X-sid", "X-time", "X-token" };
            var sb = new StringBuilder();
            foreach (var key in headerKeys)
            {
                if (headers.TryGetValue(key, out var val) && !string.IsNullOrEmpty(val))
                {
                    sb.Append(key).Append('=').Append(val).Append("||");
                }
            }

            string signStr = sb.Length >= 2 ? sb.ToString(0, sb.Length - 2) : "";
            if (!string.IsNullOrEmpty(queryEncData)) signStr += queryEncData;
            if (!string.IsNullOrEmpty(bodyEncData)) signStr += bodyEncData;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(hashKey));
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signStr));
            return Convert.ToHexStringLower(hash);
        }

        public static string DecryptAesGcm(string base64CipherData, string secret)
        {
            if (string.IsNullOrWhiteSpace(base64CipherData)) return string.Empty;
            try
            {
                byte[] raw = Convert.FromBase64String(base64CipherData);
                if (raw.Length < 28) return string.Empty; // 12 nonce + at least 16 tag

                byte[] nonce = raw[0..12];
                byte[] ciphertext = raw[12..^16];
                byte[] tag = raw[^16..];

                byte[] keyBytes = Encoding.UTF8.GetBytes(secret);
                byte[] plaintext = new byte[ciphertext.Length];

                using var aesGcm = new AesGcm(keyBytes, 16);
                aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, null);
                return Encoding.UTF8.GetString(plaintext);
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string EncryptAesGcm(string rawData, string secret)
        {
            string nonceStr = GenerateRandomNonce(12);
            byte[] nonce = Encoding.UTF8.GetBytes(nonceStr);
            byte[] plaintext = Encoding.UTF8.GetBytes(rawData);
            byte[] ciphertext = new byte[plaintext.Length];
            byte[] tag = new byte[16];
            byte[] keyBytes = Encoding.UTF8.GetBytes(secret);

            using var aesGcm = new AesGcm(keyBytes, 16);
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, null);

            byte[] combinedCipherAndTag = new byte[ciphertext.Length + 16];
            Buffer.BlockCopy(ciphertext, 0, combinedCipherAndTag, 0, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, combinedCipherAndTag, ciphertext.Length, 16);

            return Convert.ToBase64String(nonce) + Convert.ToBase64String(combinedCipherAndTag);
        }
    }

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
                    if (resElem.TryGetProperty("token", out var tok)) result.Token = tok.GetString() ?? "";
                    if (resElem.TryGetProperty("code", out var code)) result.QrCodeContent = code.GetString() ?? "";
                }
                else
                {
                    if (doc.RootElement.TryGetProperty("code", out var c)) result.ErrorCode = c.GetString() ?? "";
                    if (doc.RootElement.TryGetProperty("msg", out var m)) result.ErrorMsg = m.GetString() ?? "";
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

        public async Task<List<TuyaSharingDiscoveredDevice>> QueryUserDevicesAsync(TuyaSharingSession session, CancellationToken ct = default)
        {
            var devices = new List<TuyaSharingDiscoveredDevice>();

            // 1. Get Homes
            var homes = await QueryHomesAsync(session, ct).ConfigureAwait(false);
            if (homes.Count == 0)
            {
                // Fallback to default home query if homes list empty
                homes.Add(new KeyValuePair<string, string>("", "My Home"));
            }

            // 2. Query devices per home
            foreach (var home in homes)
            {
                var homeDevices = await QueryDevicesByHomeAsync(session, home.Key, home.Value, ct).ConfigureAwait(false);
                devices.AddRange(homeDevices);
            }

            return devices;
        }

        public async Task<List<KeyValuePair<string, string>>> QueryHomesAsync(TuyaSharingSession session, CancellationToken ct = default)
        {
            var homes = new List<KeyValuePair<string, string>>();
            try
            {
                string jsonResp = await ExecuteSignedRequestAsync(session, HttpMethod.Get, "/v1.0/m/life/users/homes", null, null, ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(jsonResp);
                if (doc.RootElement.TryGetProperty("result", out var resElem) && resElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var h in resElem.EnumerateArray())
                    {
                        string ownerId = h.TryGetProperty("ownerId", out var oid) ? oid.ToString() : "";
                        string name = h.TryGetProperty("name", out var n) ? n.GetString() ?? "Home" : "Home";
                        homes.Add(new KeyValuePair<string, string>(ownerId, name));
                    }
                }
            }
            catch { }
            return homes;
        }

        public async Task<List<TuyaSharingDiscoveredDevice>> QueryDevicesByHomeAsync(
            TuyaSharingSession session, string homeId, string homeName, CancellationToken ct = default)
        {
            var list = new List<TuyaSharingDiscoveredDevice>();
            try
            {
                var queryParams = !string.IsNullOrEmpty(homeId) ? new Dictionary<string, string> { { "homeId", homeId } } : null;
                string jsonResp = await ExecuteSignedRequestAsync(session, HttpMethod.Get, "/v1.0/m/life/ha/home/devices", queryParams, null, ct).ConfigureAwait(false);

                using var doc = JsonDocument.Parse(jsonResp);
                if (doc.RootElement.TryGetProperty("result", out var resElem) && resElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var d in resElem.EnumerateArray())
                    {
                        var dev = new TuyaSharingDiscoveredDevice
                        {
                            HomeName = homeName,
                            Id = d.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "",
                            Name = d.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "",
                            LocalKey = d.TryGetProperty("local_key", out var keyProp) ? keyProp.GetString() ?? "" : "",
                            Category = d.TryGetProperty("category", out var catProp) ? catProp.GetString() ?? "" : "",
                            ProductId = d.TryGetProperty("product_id", out var pidProp) ? pidProp.GetString() ?? "" : "",
                            ProductName = d.TryGetProperty("product_name", out var pnProp) ? pnProp.GetString() ?? "" : "",
                            Ip = d.TryGetProperty("ip", out var ipProp) ? ipProp.GetString() ?? "" : "",
                            Online = d.TryGetProperty("online", out var onProp) && onProp.GetBoolean()
                        };

                        if (!string.IsNullOrEmpty(dev.Id))
                        {
                            list.Add(dev);
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

            using var doc = JsonDocument.Parse(rawContent);
            if (doc.RootElement.TryGetProperty("result", out var resElem) && resElem.ValueKind == JsonValueKind.String)
            {
                string cipherBase64 = resElem.GetString() ?? "";
                if (!string.IsNullOrEmpty(cipherBase64))
                {
                    string decrypted = TuyaSharingCrypto.DecryptAesGcm(cipherBase64, secret);
                    return decrypted;
                }
            }

            return rawContent;
        }
    }
}
