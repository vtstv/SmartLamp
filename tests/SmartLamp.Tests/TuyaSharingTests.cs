// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;
using SmartLampApp.Services;
using Xunit;

namespace SmartLamp.Tests
{
    public class TuyaSharingTests
    {
        [Fact]
        public void ComputeSecret_ReturnsExpectedLength()
        {
            string rid = "550e8400-e29b-41d4-a716-446655440000";
            string sid = "";
            string hashKey = "d41d8cd98f00b204e9800998ecf8427e";

            string secret = TuyaSharingCrypto.ComputeSecret(rid, sid, hashKey);

            Assert.Equal(16, secret.Length);
            // Secret must be hex string
            Assert.Matches("^[a-f0-9]{16}$", secret);
        }

        [Fact]
        public void ComputeRestfulSign_ProducesValidHexHash()
        {
            string hashKey = "a1b2c3d4e5f60718293a4b5c6d7e8f90";
            var headers = new Dictionary<string, string>
            {
                { "X-appKey", "test_app_key" },
                { "X-requestId", "test_request_id" },
                { "X-time", "1728850000000" }
            };

            string sign = TuyaSharingCrypto.ComputeRestfulSign(hashKey, "", "", headers);

            Assert.Equal(64, sign.Length); // SHA256 hex is 64 characters
            Assert.Matches("^[a-f0-9]{64}$", sign);
        }

        [Theory]
        [InlineData("Hello, Tuya!")]
        [InlineData("{\"homeId\":\"12345678\",\"name\":\"Living Room\"}")]
        [InlineData("Special characters: #@$%^&*()_+{}[]|\":?><~` \r\n \t")]
        public void EncryptAndDecryptAesGcm_RoundtripSucceeds(string originalText)
        {
            string secret = "1234567890abcdef"; // 16 bytes secret key

            string encrypted = TuyaSharingCrypto.EncryptAesGcm(originalText, secret);
            Assert.False(string.IsNullOrEmpty(encrypted));
            Assert.NotEqual(originalText, encrypted);

            string decrypted = TuyaSharingCrypto.DecryptAesGcm(encrypted, secret);
            Assert.Equal(originalText, decrypted);
        }

        [Fact]
        public void DecryptAesGcm_MalformedData_ReturnsEmpty()
        {
            string secret = "1234567890abcdef";
            string result = TuyaSharingCrypto.DecryptAesGcm("corrupt_not_base64!", secret);
            Assert.Equal(string.Empty, result);

            string shortBase64 = Convert.ToBase64String(new byte[10]);
            string resultShort = TuyaSharingCrypto.DecryptAesGcm(shortBase64, secret);
            Assert.Equal(string.Empty, resultShort);
        }
    }
}
