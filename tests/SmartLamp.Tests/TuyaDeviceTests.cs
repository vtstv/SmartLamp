// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System.Collections.Generic;
using System.Threading.Tasks;
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
    }
}
