// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace SmartLampApp.Services
{
    public static class InstallationService
    {
        private const string RegistryRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRegistryName = "SmartLampStudio";
        private const string UninstallRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SmartLampStudio";

        // ====================================================================
        // 1. WINDOWS AUTOSTART (Run at Windows Startup)
        // ====================================================================
        public static bool IsAutostartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, false);
                return key?.GetValue(AppRegistryName) != null;
            }
            catch
            {
                return false;
            }
        }

        public static void SetAutostart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, true);
                if (key != null)
                {
                    if (enable)
                    {
                        string currentExePath = Process.GetCurrentProcess().MainModule?.FileName 
                                         ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SmartLampApp.exe");
                        key.SetValue(AppRegistryName, $"\"{currentExePath}\" --autostart");
                    }
                    else
                    {
                        key.DeleteValue(AppRegistryName, false);
                    }
                }
            }
            catch { }
        }

        // ====================================================================
        // 2. ONE-CLICK SYSTEM INSTALLATION (Start Menu + Apps & Features Registration)
        // ====================================================================
        public static bool IsInstalledInSystem()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(UninstallRegistryKey, false);
                return key != null;
            }
            catch
            {
                return false;
            }
        }

        public static bool InstallToSystem(out string message)
        {
            message = "";
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string installDir = Path.Combine(localAppData, "SmartLampStudio");
                Directory.CreateDirectory(installDir);

                string currentExe = Process.GetCurrentProcess().MainModule?.FileName 
                                    ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SmartLampApp.exe");
                string targetExe = Path.Combine(installDir, "SmartLampApp.exe");

                // Copy executable to LocalAppData if not running from there
                if (!string.Equals(currentExe, targetExe, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(currentExe, targetExe, true);
                }

                // 1. Create Start Menu Shortcut
                string startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                string shortcutPath = Path.Combine(startMenuPath, "SmartLamp Studio.lnk");
                CreateShortcut(shortcutPath, targetExe, "", "SmartLamp Studio Application", targetExe);

                // 2. Create Desktop Shortcut
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string desktopShortcutPath = Path.Combine(desktopPath, "SmartLamp Studio.lnk");
                CreateShortcut(desktopShortcutPath, targetExe, "", "SmartLamp Studio Application", targetExe);

                // 3. Register in Windows Add/Remove Programs (Installed Apps)
                using (var key = Registry.CurrentUser.CreateSubKey(UninstallRegistryKey))
                {
                    key.SetValue("DisplayName", AppVersion.FullTitle);
                    key.SetValue("DisplayVersion", AppVersion.Version);
                    key.SetValue("Publisher", AppVersion.Author);
                    key.SetValue("DisplayIcon", targetExe);
                    key.SetValue("InstallLocation", installDir);
                    key.SetValue("UninstallString", $"\"{targetExe}\" --uninstall");
                    key.SetValue("HelpLink", AppVersion.GitHubUrl);
                }

                message = $"SmartLamp Studio successfully installed to:\n{installDir}\n\nStart Menu shortcut & Desktop shortcut created!";
                return true;
            }
            catch (Exception ex)
            {
                message = $"Installation failed: {ex.Message}";
                return false;
            }
        }

        public static bool UninstallFromSystem(out string message)
        {
            message = "";
            try
            {
                // Remove Start Menu shortcut
                string startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                string shortcutPath = Path.Combine(startMenuPath, "SmartLamp Studio.lnk");
                if (File.Exists(shortcutPath)) File.Delete(shortcutPath);

                // Remove Desktop shortcuts
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string desktopShortcutPath = Path.Combine(desktopPath, "SmartLamp Studio.lnk");
                if (File.Exists(desktopShortcutPath)) File.Delete(desktopShortcutPath);
                string toggleShortcutPath = Path.Combine(desktopPath, "Toggle Smart Lamp.lnk");
                if (File.Exists(toggleShortcutPath)) File.Delete(toggleShortcutPath);

                // Remove Autostart
                SetAutostart(false);

                // Remove Registry entry
                Registry.CurrentUser.DeleteSubKeyTree(UninstallRegistryKey, false);

                message = "SmartLamp Studio shortcuts and registration cleanly removed.";
                return true;
            }
            catch (Exception ex)
            {
                message = $"Uninstallation failed: {ex.Message}";
                return false;
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string arguments, string description, string iconPath)
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic shell = Activator.CreateInstance(shellType)!;
                var shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.Arguments = arguments;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Description = description;
                shortcut.IconLocation = iconPath;
                shortcut.Save();
            }
        }
    }
}
