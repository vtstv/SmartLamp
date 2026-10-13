// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using SmartLampApp.Services;
using Xunit;

namespace SmartLamp.Tests
{
    public class ColorConversionTests
    {
        [Theory]
        [InlineData(0, 1000, 1000, "#FF0000")]     // Pure Red
        [InlineData(120, 1000, 1000, "#00FF00")]   // Pure Green
        [InlineData(240, 1000, 1000, "#0000FF")]   // Pure Blue
        [InlineData(60, 1000, 1000, "#FFFF00")]    // Yellow
        [InlineData(180, 1000, 1000, "#00FFFF")]   // Cyan
        [InlineData(300, 1000, 1000, "#FF00FF")]   // Magenta
        [InlineData(0, 0, 1000, "#FFFFFF")]       // White
        [InlineData(0, 0, 0, "#000000")]          // Black
        public void TuyaHsvToRgbHex_PrimaryAndSecondaryColors_MatchesExpected(int h, int s, int v, string expectedHex)
        {
            string hex = TuyaProtocol.TuyaHsvToRgbHex(h, s, v);
            Assert.Equal(expectedHex, hex);
        }

        [Fact]
        public void TuyaHsvToRgbHex_ClampsOutOfRangeInputs_Gracefully()
        {
            // Negative and oversized values should be clamped safely without throwing
            string hexLow = TuyaProtocol.TuyaHsvToRgbHex(-50, -200, -100);
            Assert.Equal("#000000", hexLow);

            string hexHigh = TuyaProtocol.TuyaHsvToRgbHex(400, 2000, 2000);
            Assert.NotNull(hexHigh);
            Assert.StartsWith("#", hexHigh);
            Assert.Equal(7, hexHigh.Length);
        }
    }
}
