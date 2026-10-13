// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using SmartLampApp.Services;
using Xunit;

namespace SmartLamp.Tests
{
    public class LocalizationServiceTests
    {
        [Fact]
        public void CurrentLanguage_DefaultsOrNormalizesProperly()
        {
            LocalizationService.CurrentLanguage = "DE";
            Assert.Equal("de", LocalizationService.CurrentLanguage);

            LocalizationService.CurrentLanguage = "RU";
            Assert.Equal("ru", LocalizationService.CurrentLanguage);

            LocalizationService.CurrentLanguage = "fr"; // unsupported falls back to en
            Assert.Equal("en", LocalizationService.CurrentLanguage);
        }

        [Fact]
        public void LanguageChanged_FiresWhenLanguageSwitches()
        {
            bool fired = false;
            void Handler() => fired = true;

            LocalizationService.LanguageChanged += Handler;
            try
            {
                LocalizationService.CurrentLanguage = "de";
                LocalizationService.CurrentLanguage = "ru";
                Assert.True(fired);
            }
            finally
            {
                LocalizationService.LanguageChanged -= Handler;
                LocalizationService.CurrentLanguage = "en";
            }
        }

        [Theory]
        [InlineData("NavDash")]
        [InlineData("NavGroups")]
        [InlineData("NavDevices")]
        [InlineData("NavSettings")]
        [InlineData("WizardTitle")]
        [InlineData("MyDevicesTitle")]
        [InlineData("WizardStep1Subtitle")]
        [InlineData("WizardStep2Subtitle")]
        [InlineData("WizardStep3Subtitle")]
        [InlineData("WizardTestConnection")]
        [InlineData("WizardFinish")]
        public void CoreAndWizardKeys_ExistInAllLanguages(string key)
        {
            foreach (var lang in new[] { "en", "de", "ru" })
            {
                LocalizationService.CurrentLanguage = lang;
                string translation = LocalizationService.T(key);

                Assert.False(string.IsNullOrWhiteSpace(translation));
                // Should not return the raw key name
                Assert.NotEqual(key, translation);
            }
        }

        [Fact]
        public void T_WithFormattingArgs_InterpolatesParameters()
        {
            LocalizationService.CurrentLanguage = "en";
            string formatted = LocalizationService.T("ConfirmDeleteDevice", "Desk Lamp", "192.168.1.100");

            Assert.Contains("Desk Lamp", formatted);
            Assert.Contains("192.168.1.100", formatted);
        }

        [Fact]
        public void T_UnknownKey_ReturnsKeyItselfWithoutThrowing()
        {
            string unknown = "NonExistentTranslationKey_12345";
            string result = LocalizationService.T(unknown);

            Assert.Equal(unknown, result);
        }
    }
}
