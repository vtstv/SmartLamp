// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using SmartLampApp.Models;

namespace SmartLampApp.Services
{
    public partial class TuyaProtocol
    {
        private async Task<string> SendCloudGetAsync(string accessId, string accessSecret, string token, string baseUrl, string uri)
        {
            long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string emptyHash = CalculateSha256Hex("");
            string stringToSign = accessId + token + t + "GET\n" + emptyHash + "\n\n" + uri;
            string sign = CalculateHmacSha256Upper(accessSecret, stringToSign);

            using var req = new HttpRequestMessage(HttpMethod.Get, baseUrl + uri);
            req.Headers.Add("client_id", accessId);
            req.Headers.Add("access_token", token);
            req.Headers.Add("sign", sign);
            req.Headers.Add("t", t.ToString());
            req.Headers.Add("sign_method", "HMAC-SHA256");

            var res = await _httpClient.SendAsync(req);
            if (!res.IsSuccessStatusCode) return "";
            return await res.Content.ReadAsStringAsync();
        }

        public async Task<(string localKey, string error)> AutoFetchKeyFromCloudAsync(string accessId, string accessKey, string region, string devId = "")
        {
            return await FetchLocalKeyFromCloudAsync(accessId, accessKey, region, devId);
        }

        public async Task<(string localKey, string error)> FetchLocalKeyFromCloudAsync(string accessId, string accessKey, string region, string devId)
        {
            try
            {
                string? token = await GetCloudTokenAsync(accessId, accessKey, region);
                if (string.IsNullOrEmpty(token)) return ("", "Failed to obtain Tuya Cloud token. Check credentials.");

                string baseUrl = GetTuyaBaseUrl(region);

                // 1. Try direct device details endpoint: /v1.0/devices/{devId}
                if (!string.IsNullOrWhiteSpace(devId))
                {
                    string uri1 = $"/v1.0/devices/{devId}";
                    string resJson1 = await SendCloudGetAsync(accessId, accessKey, token, baseUrl, uri1);
                    if (!string.IsNullOrWhiteSpace(resJson1))
                    {
                        using var doc1 = JsonDocument.Parse(resJson1);
                        if (doc1.RootElement.TryGetProperty("result", out var resObj) && resObj.ValueKind == JsonValueKind.Object)
                        {
                            if (resObj.TryGetProperty("local_key", out var lkProp))
                            {
                                string lk = lkProp.GetString() ?? "";
                                if (!string.IsNullOrEmpty(lk)) return (lk, "");
                            }
                        }
                    }
                }

                // 2. Try associated users devices list: /v1.0/iot-01/associated-users/devices
                string uri2 = "/v1.0/iot-01/associated-users/devices";
                string resJson2 = await SendCloudGetAsync(accessId, accessKey, token, baseUrl, uri2);
                if (!string.IsNullOrWhiteSpace(resJson2))
                {
                    using var doc2 = JsonDocument.Parse(resJson2);
                    if (doc2.RootElement.TryGetProperty("result", out var resObj) && resObj.TryGetProperty("devices", out var devArr))
                    {
                        foreach (var d in devArr.EnumerateArray())
                        {
                            string dId = d.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                            if (dId == devId || string.IsNullOrEmpty(devId))
                            {
                                if (d.TryGetProperty("local_key", out var lkProp))
                                {
                                    string lk = lkProp.GetString() ?? "";
                                    if (!string.IsNullOrEmpty(lk)) return (lk, "");
                                }
                                if (d.TryGetProperty("uid", out var uidProp))
                                {
                                    string uid = uidProp.GetString() ?? "";
                                    if (!string.IsNullOrEmpty(uid))
                                    {
                                        string uri3 = $"/v1.3/iot-03/devices?source_type=tuyaUser&source_id={uid}";
                                        string resJson3 = await SendCloudGetAsync(accessId, accessKey, token, baseUrl, uri3);
                                        if (!string.IsNullOrWhiteSpace(resJson3))
                                        {
                                            using var doc3 = JsonDocument.Parse(resJson3);
                                            if (doc3.RootElement.TryGetProperty("result", out var r3) && r3.TryGetProperty("list", out var l3))
                                            {
                                                foreach (var item in l3.EnumerateArray())
                                                {
                                                    string itemId = item.TryGetProperty("id", out var iId) ? iId.GetString() ?? "" : "";
                                                    if (itemId == devId || string.IsNullOrEmpty(devId))
                                                    {
                                                        if (item.TryGetProperty("local_key", out var keyProp))
                                                        {
                                                            string key = keyProp.GetString() ?? "";
                                                            if (!string.IsNullOrEmpty(key)) return (key, "");
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return ("", "Device or local key not found in cloud account.");
            }
            catch (Exception ex)
            {
                return ("", ex.Message);
            }
        }

        public async Task<(List<DeviceInfo> devices, string error)> FetchAllCloudDevicesAsync(string accessId, string accessKey, string region)
        {
            var result = new List<DeviceInfo>();
            try
            {
                string? token = await GetCloudTokenAsync(accessId, accessKey, region);
                if (string.IsNullOrEmpty(token)) return (result, "Failed to obtain Tuya Cloud token. Check credentials.");

                string baseUrl = GetTuyaBaseUrl(region);

                // 1. Associated users devices list: /v1.0/iot-01/associated-users/devices
                string uri2 = "/v1.0/iot-01/associated-users/devices";
                string resJson2 = await SendCloudGetAsync(accessId, accessKey, token, baseUrl, uri2);
                string uid = "";
                if (!string.IsNullOrWhiteSpace(resJson2))
                {
                    using var doc2 = JsonDocument.Parse(resJson2);
                    if (doc2.RootElement.TryGetProperty("result", out var resObj) && resObj.TryGetProperty("devices", out var devArr))
                    {
                        foreach (var d in devArr.EnumerateArray())
                        {
                            string dId = d.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                            string name = d.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "Smart Lamp" : "Smart Lamp";
                            string lk = d.TryGetProperty("local_key", out var lkProp) ? lkProp.GetString() ?? "" : "";
                            string ip = d.TryGetProperty("ip", out var ipProp) ? ipProp.GetString() ?? "" : "";

                            if (string.IsNullOrEmpty(uid) && d.TryGetProperty("uid", out var uProp))
                            {
                                uid = uProp.GetString() ?? "";
                            }

                            if (!string.IsNullOrEmpty(dId) && !result.Any(r => r.DevId == dId))
                            {
                                result.Add(new DeviceInfo
                                {
                                    DevId = dId,
                                    Name = name,
                                    LocalKey = lk,
                                    Ip = ip,
                                    GroupName = "Default Group"
                                });
                            }
                        }
                    }
                }

                // 2. Enrich via /v1.3/iot-03/devices if user uid is known
                if (!string.IsNullOrEmpty(uid))
                {
                    string uri3 = $"/v1.3/iot-03/devices?source_type=tuyaUser&source_id={uid}";
                    string resJson3 = await SendCloudGetAsync(accessId, accessKey, token, baseUrl, uri3);
                    if (!string.IsNullOrWhiteSpace(resJson3))
                    {
                        using var doc3 = JsonDocument.Parse(resJson3);
                        if (doc3.RootElement.TryGetProperty("result", out var r3) && r3.TryGetProperty("list", out var l3))
                        {
                            foreach (var item in l3.EnumerateArray())
                            {
                                string itemId = item.TryGetProperty("id", out var iId) ? iId.GetString() ?? "" : "";
                                string itemName = item.TryGetProperty("name", out var iName) ? iName.GetString() ?? "Smart Lamp" : "Smart Lamp";
                                string itemKey = item.TryGetProperty("local_key", out var kProp) ? kProp.GetString() ?? "" : "";
                                string itemIp = item.TryGetProperty("ip", out var ipProp) ? ipProp.GetString() ?? "" : "";

                                var existing = result.FirstOrDefault(r => r.DevId == itemId);
                                if (existing != null)
                                {
                                    if (string.IsNullOrEmpty(existing.LocalKey) && !string.IsNullOrEmpty(itemKey)) existing.LocalKey = itemKey;
                                    if (string.IsNullOrEmpty(existing.Ip) && !string.IsNullOrEmpty(itemIp)) existing.Ip = itemIp;
                                    if (existing.Name == "Smart Lamp" && !string.IsNullOrEmpty(itemName)) existing.Name = itemName;
                                }
                                else if (!string.IsNullOrEmpty(itemId))
                                {
                                    result.Add(new DeviceInfo
                                    {
                                        DevId = itemId,
                                        Name = itemName,
                                        LocalKey = itemKey,
                                        Ip = itemIp,
                                        GroupName = "Default Group"
                                    });
                                }
                            }
                        }
                    }
                }

                if (result.Count > 0) return (result, "");
                return (result, "No devices found in Tuya Cloud account.");
            }
            catch (Exception ex)
            {
                return (result, ex.Message);
            }
        }
    }
}
