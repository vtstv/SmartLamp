// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using SmartLampApp;
using Xunit;

namespace SmartLamp.Tests
{
    public class UpdateVersionTests
    {
        [Theory]
        [InlineData("3.3.3", "3.4.0", true)]
        [InlineData("3.4.0", "3.4.1", true)]
        [InlineData("3.9.0", "3.10.0", true)]
        [InlineData("v3.3.3", "v3.4.0", true)]
        [InlineData("3.3.3", "v3.4.0", true)]
        [InlineData("v3.3.3", "3.4.0", true)]
        public void IsNewerVersion_WhenRemoteIsNewer_ReturnsTrue(string current, string remote, bool expected)
        {
            bool result = AppVersion.IsNewerVersion(current, remote);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("3.4.0", "3.4.0", false)]
        [InlineData("3.4.0", "3.3.3", false)]
        [InlineData("4.0.0", "3.4.0", false)]
        [InlineData("v3.4.0", "v3.4.0", false)]
        [InlineData("3.4.0", null, false)]
        [InlineData("3.4.0", "", false)]
        public void IsNewerVersion_WhenRemoteIsSameOrOlder_ReturnsFalse(string current, string? remote, bool expected)
        {
            bool result = AppVersion.IsNewerVersion(current, remote!);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void AppVersion_Properties_AreConsistent()
        {
            Assert.False(string.IsNullOrWhiteSpace(AppVersion.Version));
            Assert.Equal("SmartLamp Studio", AppVersion.AppName);
            Assert.Contains(AppVersion.Version, AppVersion.FullTitle);
            Assert.Contains("Murr", AppVersion.CopyrightNotice);
        }
    }
}
