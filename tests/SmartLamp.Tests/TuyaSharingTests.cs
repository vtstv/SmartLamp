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

        [Fact]
        public async System.Threading.Tasks.Task RequestQrCodeAsync_ParsesTuyaQrcodePropertyCorrectly()
        {
            string jsonResponse = "{\"success\":true,\"tid\":\"2f4eceb2c74c11f1a4f1fe311d4ee919\",\"t\":1791926545041,\"result\":{\"qrcode\":\"EU1791926545039HkNuczMDdeHtSisDmapiklgaRc179192684503903\"}}";
            var mockHandler = new MockHttpMessageHandler(req => new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(jsonResponse)
            });

            var service = new TuyaSharingAuthService(new System.Net.Http.HttpClient(mockHandler));
            var tokenRes = await service.RequestQrCodeAsync("CxWGgPB", "smartlife");

            Assert.True(tokenRes.Success);
            Assert.Equal("EU1791926545039HkNuczMDdeHtSisDmapiklgaRc179192684503903", tokenRes.Token);
            Assert.Equal("smartlife--qrLogin?token=EU1791926545039HkNuczMDdeHtSisDmapiklgaRc179192684503903", tokenRes.QrCodeContent);
        }

        [Fact]
        public async System.Threading.Tasks.Task PollLoginResultAsync_ReturnsUnscannedWhenPending()
        {
            string jsonResponse = "{\"code\":\"E0020003\",\"msg\":\"Login failed, please scan and try again!\",\"success\":false,\"tid\":\"34f2c0c1c74c11f1befb3a77ad39ec5d\",\"t\":1791926554503}";
            var mockHandler = new MockHttpMessageHandler(req => new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(jsonResponse)
            });

            var service = new TuyaSharingAuthService(new System.Net.Http.HttpClient(mockHandler));
            var (scanned, session, err) = await service.PollLoginResultAsync("token_123", "CxWGgPB");

            Assert.False(scanned);
            Assert.Null(session);
            Assert.Contains("E0020003", err);
        }

        [Fact]
        public async System.Threading.Tasks.Task PollLoginResultAsync_ReturnsSessionWhenConfirmed()
        {
            string jsonResponse = "{\"success\":true,\"result\":{\"accessToken\":\"acc_123\",\"refreshToken\":\"ref_456\",\"expireTime\":1791999999,\"uid\":\"uid_789\",\"endpoint\":\"https://openapi.tuyaeu.com\"}}";
            var mockHandler = new MockHttpMessageHandler(req => new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(jsonResponse)
            });

            var service = new TuyaSharingAuthService(new System.Net.Http.HttpClient(mockHandler));
            var (scanned, session, err) = await service.PollLoginResultAsync("token_123", "CxWGgPB");

            Assert.True(scanned);
            Assert.NotNull(session);
            Assert.Equal("acc_123", session.AccessToken);
            Assert.Equal("ref_456", session.RefreshToken);
            Assert.Equal("https://openapi.tuyaeu.com", session.Endpoint);
        }

        [Fact]
        public void ParseHomesArray_HandlesDirectArrayAndEnvelope()
        {
            string directArray = "[{\"ownerId\":12345,\"name\":\"Living Room Home\"},{\"ownerId\":67890,\"name\":\"Office Home\"}]";
            var directResult = TuyaSharingAuthService.ParseHomesArray(directArray);
            Assert.Equal(2, directResult.Count);
            Assert.Equal("12345", directResult[0].Key);
            Assert.Equal("Living Room Home", directResult[0].Value);

            string envelope = "{\"success\":true,\"result\":[{\"ownerId\":\"99999\",\"name\":\"Villa\"}]}";
            var envResult = TuyaSharingAuthService.ParseHomesArray(envelope);
            Assert.Single(envResult);
            Assert.Equal("99999", envResult[0].Key);
            Assert.Equal("Villa", envResult[0].Value);
        }

        [Fact]
        public void ParseDevicesArray_HandlesBothFormatsAndAlternativeProperties()
        {
            string json = "[{\"id\":\"dev_abc\",\"name\":\"Desk Lamp\",\"local_key\":\"key123456789012\",\"ip\":\"192.168.0.68\",\"online\":true}]";
            var devs = TuyaSharingAuthService.ParseDevicesArray(json, "Home A");
            Assert.Single(devs);
            Assert.Equal("dev_abc", devs[0].Id);
            Assert.Equal("Desk Lamp", devs[0].Name);
            Assert.Equal("key123456789012", devs[0].LocalKey);
            Assert.Equal("192.168.0.68", devs[0].Ip);
            Assert.True(devs[0].Online);
            Assert.Equal("Home A", devs[0].HomeName);

            // Test alternative casing (localKey, devId, dev_name) and envelope
            string envelopeJson = "{\"success\":true,\"result\":[{\"devId\":\"dev_xyz\",\"dev_name\":\"Floor Lamp\",\"localKey\":\"key987654321098\",\"online\":false}]}";
            var envDevs = TuyaSharingAuthService.ParseDevicesArray(envelopeJson, "Home B");
            Assert.Single(envDevs);
            Assert.Equal("dev_xyz", envDevs[0].Id);
            Assert.Equal("Floor Lamp", envDevs[0].Name);
            Assert.Equal("key987654321098", envDevs[0].LocalKey);
            Assert.False(envDevs[0].Online);
        }
    }

    public class MockHttpMessageHandler : System.Net.Http.HttpMessageHandler
    {
        private readonly Func<System.Net.Http.HttpRequestMessage, System.Net.Http.HttpResponseMessage> _handler;
        public MockHttpMessageHandler(Func<System.Net.Http.HttpRequestMessage, System.Net.Http.HttpResponseMessage> handler) => _handler = handler;
        protected override System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            => System.Threading.Tasks.Task.FromResult(_handler(request));
    }
}
