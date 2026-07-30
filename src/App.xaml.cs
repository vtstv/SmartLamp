// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace SmartLampApp
{
    public partial class App : System.Windows.Application
    {
        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);

        private static Mutex _mutex = null;
        private static EventWaitHandle _showEvent = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Check for CLI Headless Arguments FIRST (e.g. --toggle, --on, --off, --brightness, etc.)
            if (e.Args != null && e.Args.Length > 0)
            {
                bool handled = HandleCommandLineArgs(e.Args);
                if (handled)
                {
                    Environment.Exit(0);
                    return;
                }
            }

            const string appName = "SmartLampApp_SingleInstanceMutex";
            const string eventName = "SmartLampApp_ShowEvent";
            bool createdNew;

            _mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                // App is already running! Signal the existing instance to show itself.
                try
                {
                    var current = Process.GetCurrentProcess();
                    foreach (var process in Process.GetProcessesByName(current.ProcessName))
                    {
                        if (process.Id != current.Id)
                        {
                            AllowSetForegroundWindow(process.Id);
                            break;
                        }
                    }

                    var existingEvent = EventWaitHandle.OpenExisting(eventName);
                    existingEvent.Set();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Could not signal existing instance: {ex.Message}", "Single Instance Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                
                Environment.Exit(0);
                return;
            }

            // We are the first instance. Create the event and listen for signals.
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            Thread waitThread = new Thread(() =>
            {
                while (true)
                {
                    _showEvent.WaitOne();
                    if (Current != null)
                    {
                        Current.Dispatcher.BeginInvoke((Action)(() =>
                        {
                            foreach (Window window in Current.Windows)
                            {
                                if (window.Title.Contains("Smart Lamp Studio") || window is MainWindow)
                                {
                                    window.Show();
                                    if (window.WindowState == WindowState.Minimized)
                                    {
                                        window.WindowState = WindowState.Normal;
                                    }
                                    window.Activate();
                                    window.Topmost = true;
                                    window.Topmost = false;
                                    window.Focus();
                                }
                            }
                        }));
                    }
                }
            });
            waitThread.IsBackground = true;
            waitThread.Start();

            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogCrash(e.Exception);
            System.Windows.MessageBox.Show($"Application Error:\n{e.Exception.Message}\n\nDetails saved to crash.log", "SmartLamp Studio Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogCrash(ex);
            }
        }

        private void LogCrash(Exception ex)
        {
            try
            {
                string log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Unhandled Exception: {ex}\n----------------------------------------\n";
                File.AppendAllText("crash.log", log);
            }
            catch { }
        }

        public static bool IsAutostartLaunch { get; set; } = false;

        private bool HandleCommandLineArgs(string[] args)
        {
            string argStr = string.Join(" ", args).ToLowerInvariant();

            if (argStr.Contains("--install"))
            {
                SmartLampApp.Services.InstallationService.InstallToSystem(out var msg);
                System.Windows.MessageBox.Show(msg, "SmartLamp Studio Installer", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            if (argStr.Contains("--uninstall"))
            {
                SmartLampApp.Services.InstallationService.UninstallFromSystem(out var msg);
                System.Windows.MessageBox.Show(msg, "SmartLamp Studio Uninstaller", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            if (argStr.Contains("--autostart"))
            {
                IsAutostartLaunch = true;
                return false; // Proceed to run normal WPF app, but minimized to tray
            }

            if (!argStr.Contains("--toggle") && !argStr.Contains("-t") &&
                !argStr.Contains("--on") && !argStr.Contains("--off") &&
                !argStr.Contains("--brightness") && !argStr.Contains("--temp") &&
                !argStr.Contains("--color"))
            {
                return false;
            }

            try
            {
                var config = SmartLampApp.Models.ConfigManager.Load();
                var protocol = new SmartLampApp.Services.TuyaProtocol(config);
                var active = protocol.GetActiveDevice();

                if (active == null || string.IsNullOrWhiteSpace(active.LocalKey))
                {
                    return true;
                }

                System.Threading.Tasks.Task.Run(async () =>
                {
                    if (argStr.Contains("--on"))
                    {
                        await protocol.SetPowerAsync(true);
                    }
                    else if (argStr.Contains("--off"))
                    {
                        await protocol.SetPowerAsync(false);
                    }
                    else if (argStr.Contains("--toggle") || argStr.Contains("-t"))
                    {
                        var status = await protocol.GetStatusAsync();
                        bool targetPower = !status.IsPowerOn;
                        await protocol.SetPowerAsync(targetPower);
                    }

                    for (int i = 0; i < args.Length; i++)
                    {
                        if (args[i].Equals("--brightness", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                        {
                            if (int.TryParse(args[i + 1], out int bPct))
                            {
                                await protocol.SetBrightnessAsync(bPct);
                            }
                        }
                        else if (args[i].Equals("--temp", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                        {
                            if (int.TryParse(args[i + 1], out int kVal))
                            {
                                await protocol.SetColorTempAsync(kVal);
                            }
                        }
                        else if (args[i].Equals("--color", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                        {
                            await protocol.SetColorHexAsync(args[i + 1]);
                        }
                    }
                }).GetAwaiter().GetResult();
            }
            catch { }

            return true;
        }
    }
}
