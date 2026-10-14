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
using System.Threading;
using System.Threading.Tasks;

namespace SmartLampApp.Services
{
    public partial class TuyaSharingAuthService
    {
        private async Task<string> ExecuteSignedRequestAsync(
            TuyaSharingSession session,
            HttpMethod method,
            string path,
            Dictionary<string, string>? queryParams,
            string? jsonBody,
            CancellationToken ct)
        {
            var endpointsToTry = new List<string> { session.Endpoint };
            if (!endpointsToTry.Contains("https://apigw.tuyaeu.com", StringComparer.OrdinalIgnoreCase))
                endpointsToTry.Add("https://apigw.tuyaeu.com");
            if (!endpointsToTry.Contains("https://apigw.iotbing.com", StringComparer.OrdinalIgnoreCase))
                endpointsToTry.Add("https://apigw.iotbing.com");

            string lastResponse = "";
            foreach (var ep in endpointsToTry)
            {
                try
                {
                    string resp = await SendSignedOnceAsync(session, ep, method, path, queryParams, jsonBody, ct).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(resp))
                    {
                        using var doc = JsonDocument.Parse(resp);
                        if (doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean())
                        {
                            return resp;
                        }
                    }
                    lastResponse = resp;
                }
                catch (Exception ex)
                {
                    LogDiagnostic($"SendSignedOnce to {ep} failed: {ex.Message}");
                }
            }
            return lastResponse;
        }

        private async Task<string> SendSignedOnceAsync(
            TuyaSharingSession session,
            string baseEndpoint,
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
            string requestUri = baseEndpoint.TrimEnd('/') + path;

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

            LogDiagnostic($"SignedReq -> {method} {requestUri}");
            using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
            string rawContent = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            LogDiagnostic($"SignedResp ({(int)res.StatusCode}) <- {rawContent}");

            try
            {
                using var doc = JsonDocument.Parse(rawContent);
                if (doc.RootElement.TryGetProperty("result", out var resElem) && resElem.ValueKind == JsonValueKind.String)
                {
                    string cipherBase64 = resElem.GetString() ?? "";
                    if (!string.IsNullOrEmpty(cipherBase64))
                    {
                        string decrypted = TuyaSharingCrypto.DecryptAesGcm(cipherBase64, secret);
                        LogDiagnostic($"SignedDecrypted <- {decrypted}");
                        if (!string.IsNullOrWhiteSpace(decrypted))
                        {
                            return $"{{\"success\":true,\"result\":{decrypted}}}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogDiagnostic($"Decrypt error: {ex.Message}");
            }

            return rawContent;
        }

        public static string GetPropString(JsonElement elem, params string[] names)
        {
            foreach (var n in names)
            {
                if (elem.TryGetProperty(n, out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.String) return prop.GetString() ?? "";
                    return prop.ToString();
                }
            }
            return "";
        }

        public static long GetPropLong(JsonElement elem, params string[] names)
        {
            foreach (var n in names)
            {
                if (elem.TryGetProperty(n, out var prop))
                {
                    if (prop.TryGetInt64(out var val)) return val;
                    if (long.TryParse(prop.ToString(), out var parsed)) return parsed;
                }
            }
            return 0;
        }
    }
}
