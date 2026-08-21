// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

namespace SmartLampApp
{
    public static class AppVersion
    {
        // ====================================================================
        // CENTRAL APPLICATION VERSION & BRANDING CONFIGURATION
        // Edit the constants below to update the version across the entire app.
        // ====================================================================
        public const string Version = "2.1";
        public const string AppName = "SmartLamp Studio";
        public const string Author = "Murr";
        public const string GitHubUrl = "https://github.com/vtstv";

        public static string FullTitle => $"{AppName} v{Version}";
        public static string DisplayTitle => $"{AppName} v{Version}";
        public static string SystemTrayToolTip => $"{AppName} v{Version}";
        public static string CopyrightNotice => $"© 2026 {Author} ({GitHubUrl})";
    }
}
