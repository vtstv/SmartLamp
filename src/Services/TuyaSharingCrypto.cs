// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

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

        public static bool IsPrivateIp(string? ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return false;
            if (System.Net.IPAddress.TryParse(ip.Trim(), out var addr))
            {
                byte[] bytes = addr.GetAddressBytes();
                if (bytes.Length == 4)
                {
                    if (bytes[0] == 10) return true;
                    if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                    if (bytes[0] == 192 && bytes[1] == 168) return true;
                    if (bytes[0] == 127) return true;
                    if (bytes[0] == 169 && bytes[1] == 254) return true;
                }
            }
            return false;
        }
    }
}
