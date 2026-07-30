// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SmartLampApp.Services
{
    public static class InstallationService
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private const uint SHCNE_ALLEVENTS = 0x7FFFFFFF;
        private const uint SHCNF_FLUSH = 0x1000;

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

                // Copy icon file for shortcuts
                string iconPath = Path.Combine(installDir, "app_icon.ico");
                try
                {
                    string sourceIcon = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app_icon.ico");
                    if (File.Exists(sourceIcon))
                    {
                        File.Copy(sourceIcon, iconPath, true);
                    }
                    else
                    {
                        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                        using var stream = assembly.GetManifestResourceStream("SmartLampApp.Assets.app_icon.ico");
                        if (stream != null)
                        {
                            using var fs = File.Create(iconPath);
                            stream.CopyTo(fs);
                        }
                    }
                }
                catch { }

                string iconLocation = File.Exists(iconPath) ? iconPath : $"{targetExe},0";

                // 1. Create Start Menu Shortcuts (both root Programs & subfolder)
                string startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                
                // Direct shortcut in Start Menu Programs
                string rootShortcutPath = Path.Combine(startMenuPath, "SmartLamp Studio.lnk");
                CreateShortcut(rootShortcutPath, targetExe, "", "SmartLamp Studio Application", iconLocation);

                // Subfolder shortcut in Start Menu Programs\SmartLamp Studio
                string startMenuFolder = Path.Combine(startMenuPath, "SmartLamp Studio");
                Directory.CreateDirectory(startMenuFolder);
                string folderShortcutPath = Path.Combine(startMenuFolder, "SmartLamp Studio.lnk");
                CreateShortcut(folderShortcutPath, targetExe, "", "SmartLamp Studio Application", iconLocation);

                // 2. Create Desktop Shortcut
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string desktopShortcutPath = Path.Combine(desktopPath, "SmartLamp Studio.lnk");
                CreateShortcut(desktopShortcutPath, targetExe, "", "SmartLamp Studio Application", iconLocation);

                // 3. Register in Windows Add/Remove Programs (Installed Apps)
                using (var key = Registry.CurrentUser.CreateSubKey(UninstallRegistryKey))
                {
                    key.SetValue("DisplayName", AppVersion.FullTitle);
                    key.SetValue("DisplayVersion", AppVersion.Version);
                    key.SetValue("Publisher", AppVersion.Author);
                    key.SetValue("DisplayIcon", iconLocation);
                    key.SetValue("InstallLocation", installDir);
                    key.SetValue("UninstallString", $"\"{targetExe}\" --uninstall");
                    key.SetValue("HelpLink", AppVersion.GitHubUrl);
                }

                // 4. Force Windows Shell & Start Menu Indexer to refresh
                try { SHChangeNotify(SHCNE_ALLEVENTS, SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero); } catch { }

                message = $"SmartLamp Studio successfully installed to:\n{installDir}\n\nStart Menu & Desktop shortcuts created!";
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
                // Remove Start Menu shortcuts
                string startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                string rootShortcutPath = Path.Combine(startMenuPath, "SmartLamp Studio.lnk");
                if (File.Exists(rootShortcutPath)) File.Delete(rootShortcutPath);

                string startMenuFolder = Path.Combine(startMenuPath, "SmartLamp Studio");
                if (Directory.Exists(startMenuFolder)) Directory.Delete(startMenuFolder, true);

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

                // Force Windows Shell Refresh
                try { SHChangeNotify(SHCNE_ALLEVENTS, SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero); } catch { }

                message = "SmartLamp Studio shortcuts and registration cleanly removed.";
                return true;
            }
            catch (Exception ex)
            {
                message = $"Uninstallation failed: {ex.Message}";
                return false;
            }
        }

        public static void CreateShortcut(string shortcutPath, string targetPath, string arguments, string description, string iconPath)
        {
            try
            {
                string workDir = Path.GetDirectoryName(targetPath) ?? "";
                string script = $"$s=(New-Object -COM WScript.Shell).CreateShortcut('{shortcutPath.Replace("'", "''")}'); " +
                               $"$s.TargetPath='{targetPath.Replace("'", "''")}'; " +
                               $"$s.Arguments='{arguments.Replace("'", "''")}'; " +
                               $"$s.WorkingDirectory='{workDir.Replace("'", "''")}'; " +
                               $"$s.Description='{description.Replace("'", "''")}'; " +
                               $"$s.IconLocation='{iconPath.Replace("'", "''")}'; " +
                               $"$s.Save()";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -WindowStyle Hidden -Command \"{script}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(4000);
            }
            catch { }
        }
    }
}
