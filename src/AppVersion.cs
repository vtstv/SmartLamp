// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Reflection;

namespace SmartLampApp
{
    public static class AppVersion
    {
        // ====================================================================
        // CENTRAL APPLICATION VERSION & BRANDING
        // Version is resolved dynamically at runtime from Assembly metadata,
        // which is set centrally in Directory.Build.props from the root VERSION file.
        // ====================================================================
        private static readonly Lazy<string> _lazyVersion = new(() =>
        {
            try
            {
                var asm = Assembly.GetEntryAssembly() ?? typeof(AppVersion).Assembly;
                var infoAttr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (!string.IsNullOrWhiteSpace(infoAttr?.InformationalVersion))
                {
                    int plusIdx = infoAttr.InformationalVersion.IndexOf('+');
                    return plusIdx > 0 ? infoAttr.InformationalVersion.Substring(0, plusIdx).Trim() : infoAttr.InformationalVersion.Trim();
                }
                var ver = asm.GetName().Version;
                if (ver != null)
                {
                    return $"{ver.Major}.{ver.Minor}.{ver.Build}";
                }
            }
            catch { }
            return "3.3.1";
        });

        public static string Version => _lazyVersion.Value;
        public const string AppName = "SmartLamp Studio";
        public const string Author = "Murr";
        public const string GitHubUrl = "https://github.com/vtstv";

        public static string FullTitle => $"{AppName} v{Version}";
        public static string DisplayTitle => $"{AppName} v{Version}";
        public static string SystemTrayToolTip => $"{AppName} v{Version}";
        public static string CopyrightNotice => $"© 2026 {Author} ({GitHubUrl})";
    }
}
