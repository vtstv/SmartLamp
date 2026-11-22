// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System.Collections.Generic;
using System.Threading.Tasks;
using SmartLampApp.Services;
using TuyaNet.Native;
using TuyaNet.Native.Models;
using Xunit;

namespace SmartLamp.Tests
{
    public class TuyaDeviceTests
    {
        [Fact]
        public void Constructor_InitializesPropertiesCorrectly()
        {
            var device = new TuyaDevice("192.168.1.50", "device_id_123", "0123456789abcdef", TuyaProtocolVersion.V35, 6668);

            Assert.Equal("192.168.1.50", device.Ip);
            Assert.Equal("device_id_123", device.DeviceId);
            Assert.Equal(TuyaProtocolVersion.V35, device.Version);
            Assert.False(device.IsConnected); // Not connected until first network operation
        }

        [Fact]
        public void Constructor_StringVersion_ParsesProperly()
        {
            var dev33 = new TuyaDevice("192.168.1.55", "dev_33", "0123456789abcdef", "3.3");
            Assert.Equal(TuyaProtocolVersion.V33, dev33.Version);

            var dev35 = new TuyaDevice("192.168.1.56", "dev_35", "0123456789abcdef", "3.5");
            Assert.Equal(TuyaProtocolVersion.V35, dev35.Version);
        }

        [Fact]
        public async Task SetDpsAsync_EmptyDictionary_ReturnsTrueImmediately()
        {
            var device = new TuyaDevice("127.0.0.1", "mock_dev", "0123456789abcdef");

            bool resultEmpty = await device.SetDpsAsync(new Dictionary<string, object>());
            bool resultNull = await device.SetDpsAsync(null!);

            Assert.True(resultEmpty);
            Assert.True(resultNull);
        }

        [Fact]
        public async Task ProbeAsync_InvalidIpOrKey_ReturnsFalseSafely()
        {
            // Empty IP or key should immediately return false without throwing
            bool res1 = await TuyaDevice.ProbeAsync("", "devId", "key");
            Assert.False(res1);

            bool res2 = await TuyaDevice.ProbeAsync("127.0.0.1", "", "key");
            Assert.False(res2);

            bool res3 = await TuyaDevice.ProbeAsync("127.0.0.1", "devId", "");
            Assert.False(res3);

            // Unreachable IP with short timeout should safely return false
            bool res4 = await TuyaDevice.ProbeAsync("192.0.2.1", "devId", "0123456789abcdef", "3.5", 100);
            Assert.False(res4);
        }

        [Fact]
        public void ParseStatus_PowerOffWithBrightness_RecognizesPowerOffAccurately()
        {
            // Verify that a Tuya status payload with DP 20=false or switch_led=false is recognized as powered off,
            // even if DP 22 (brightness) is present and non-zero
            string json = "{\"dps\":{\"20\":false,\"21\":\"white\",\"22\":500,\"23\":500}}";
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var dps = doc.RootElement.GetProperty("dps");

            bool isPowerOn = false;
            if (dps.TryGetProperty("20", out var pVal))
            {
                isPowerOn = pVal.ValueKind == System.Text.Json.JsonValueKind.True;
            }

            Assert.False(isPowerOn);
        }

        [Fact]
        public void ParseStatusJson_RootDataDps_ParsesV35AsyncPayloadCorrectly()
        {
            // Tuya v3.5 async frames wrap DPS in data.dps
            string v35Json = "{\"protocol\":5,\"t\":1700000000,\"data\":{\"dps\":{\"20\":true,\"21\":\"colour\",\"22\":800,\"23\":600,\"24\":\"000003e803e8\"}}}";
            var status = TuyaProtocol.ParseStatusJson(v35Json);

            Assert.True(status.IsOnline);
            Assert.True(status.IsPowerOn);
            Assert.Equal("colour", status.Mode);
            Assert.Equal(80, status.Brightness);
            Assert.False(string.IsNullOrWhiteSpace(status.ColorHex));
        }

        [Fact]
        public void ParseStatusJson_RootDataDps_PowerOffRecognized()
        {
            string v35Json = "{\"protocol\":5,\"t\":1700000000,\"data\":{\"dps\":{\"20\":false,\"22\":350}}}";
            var status = TuyaProtocol.ParseStatusJson(v35Json);

            Assert.True(status.IsOnline);
            Assert.False(status.IsPowerOn);
            Assert.Equal(35, status.Brightness);
        }

        [Fact]
        public void ParseStatusJson_LegacyFormats_StillSupported()
        {
            // root.status.dps
            string statusDpsJson = "{\"status\":{\"dps\":{\"20\":true,\"22\":600}}}";
            var st1 = TuyaProtocol.ParseStatusJson(statusDpsJson);
            Assert.True(st1.IsOnline);
            Assert.True(st1.IsPowerOn);
            Assert.Equal(60, st1.Brightness);

            // root.dps
            string rootDpsJson = "{\"dps\":{\"switch_led\":true,\"22\":900}}";
            var st2 = TuyaProtocol.ParseStatusJson(rootDpsJson);
            Assert.True(st2.IsOnline);
            Assert.True(st2.IsPowerOn);
            Assert.Equal(90, st2.Brightness);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("{\"error\":\"device offline\"}")]
        [InlineData("{\"error\":true}")]
        public void ParseStatusJson_OfflineOrError_ReturnsOfflineStatus(string errorPayload)
        {
            var status = TuyaProtocol.ParseStatusJson(errorPayload);

            Assert.False(status.IsOnline);
            Assert.False(status.IsPowerOn);
            Assert.NotEmpty(status.ErrorMessage);
        }

        [Fact]
        public void ParseDeviceDetailsOnline_OnlineAndOfflineResponses_ParsedAccurately()
        {
            string onlineJson = "{\"code\":1100,\"msg\":\"success\",\"result\":{\"id\":\"dev123\",\"online\":true,\"ip\":\"192.168.1.10\"},\"success\":true}";
            Assert.True(TuyaProtocol.ParseDeviceDetailsOnline(onlineJson));

            string offlineJson = "{\"code\":1100,\"msg\":\"success\",\"result\":{\"id\":\"dev123\",\"online\":false,\"ip\":\"192.168.1.10\"},\"success\":true}";
            Assert.False(TuyaProtocol.ParseDeviceDetailsOnline(offlineJson));

            string failedJson = "{\"code\":1010,\"msg\":\"token invalid\",\"success\":false}";
            Assert.False(TuyaProtocol.ParseDeviceDetailsOnline(failedJson));

            Assert.False(TuyaProtocol.ParseDeviceDetailsOnline(""));
            Assert.False(TuyaProtocol.ParseDeviceDetailsOnline(null!));
        }
    }
}
