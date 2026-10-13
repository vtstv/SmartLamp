// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using TuyaNet.Native.Models;
using Xunit;

namespace SmartLamp.Tests
{
    public class TuyaProtocolVersionTests
    {
        [Theory]
        [InlineData("3.1", TuyaProtocolVersion.V31)]
        [InlineData("3.3", TuyaProtocolVersion.V33)]
        [InlineData("3.4", TuyaProtocolVersion.V34)]
        [InlineData("3.5", TuyaProtocolVersion.V35)]
        [InlineData("3.3.0", TuyaProtocolVersion.V33)]
        [InlineData("3.5.2", TuyaProtocolVersion.V35)]
        public void ParseVersion_ValidStrings_ReturnsCorrectEnum(string input, TuyaProtocolVersion expected)
        {
            var parsed = TuyaProtocolVersionExtensions.ParseVersion(input);
            Assert.Equal(expected, parsed);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("unknown")]
        [InlineData("4.0")]
        public void ParseVersion_NullOrInvalid_FallsBackToV35(string? input)
        {
            var parsed = TuyaProtocolVersionExtensions.ParseVersion(input);
            Assert.Equal(TuyaProtocolVersion.V35, parsed);
        }

        [Theory]
        [InlineData(TuyaProtocolVersion.V31, "3.1")]
        [InlineData(TuyaProtocolVersion.V33, "3.3")]
        [InlineData(TuyaProtocolVersion.V34, "3.4")]
        [InlineData(TuyaProtocolVersion.V35, "3.5")]
        public void ToVersionString_ReturnsExpectedString(TuyaProtocolVersion version, string expected)
        {
            Assert.Equal(expected, version.ToVersionString());
        }
    }
}
