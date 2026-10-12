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

        private static Mutex? _mutex = null;
        private static EventWaitHandle? _showEvent = null;
        private static EventWaitHandle? _toggleEvent = null;
        private static EventWaitHandle? _onEvent = null;
        private static EventWaitHandle? _offEvent = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            string[] args = e.Args;
            if (args == null || args.Length == 0)
            {
                var sysArgs = Environment.GetCommandLineArgs();
                if (sysArgs.Length > 1)
                {
                    args = new string[sysArgs.Length - 1];
                    Array.Copy(sysArgs, 1, args, 0, args.Length);
                }
                else
                {
                    args = Array.Empty<string>();
                }
            }

            if (args.Length > 0)
            {
                IsCommandLineLaunch = true;
                bool handled = HandleCommandLineArgs(args);
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
                catch { }
                
                Environment.Exit(0);
                return;
            }

            // We are the first instance. Create the events and listen for signals.
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            _toggleEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "SmartLampApp_ToggleEvent");
            _onEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "SmartLampApp_OnEvent");
            _offEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "SmartLampApp_OffEvent");

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

            Thread toggleThread = new Thread(() =>
            {
                while (true)
                {
                    _toggleEvent.WaitOne();
                    if (Current != null)
                    {
                        Current.Dispatcher.BeginInvoke((Action)(async () =>
                        {
                            try
                            {
                                foreach (Window window in Current.Windows)
                                {
                                    if (window is MainWindow mw)
                                    {
                                        await mw.TogglePowerFromExternalTriggerAsync();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                try { File.AppendAllText("cli.log", $"[{DateTime.Now}] IPC Toggle Error: {ex.Message}\n"); } catch { }
                            }
                        }));
                    }
                }
            });
            toggleThread.IsBackground = true;
            toggleThread.Start();

            Thread onThread = new Thread(() =>
            {
                while (true)
                {
                    _onEvent.WaitOne();
                    if (Current != null)
                    {
                        Current.Dispatcher.BeginInvoke((Action)(async () =>
                        {
                            try
                            {
                                foreach (Window window in Current.Windows)
                                {
                                    if (window is MainWindow mw)
                                    {
                                        await mw.SetPowerFromExternalTriggerAsync(true);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                try { File.AppendAllText("cli.log", $"[{DateTime.Now}] IPC On Error: {ex.Message}\n"); } catch { }
                            }
                        }));
                    }
                }
            });
            onThread.IsBackground = true;
            onThread.Start();

            Thread offThread = new Thread(() =>
            {
                while (true)
                {
                    _offEvent.WaitOne();
                    if (Current != null)
                    {
                        Current.Dispatcher.BeginInvoke((Action)(async () =>
                        {
                            try
                            {
                                foreach (Window window in Current.Windows)
                                {
                                    if (window is MainWindow mw)
                                    {
                                        await mw.SetPowerFromExternalTriggerAsync(false);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                try { File.AppendAllText("cli.log", $"[{DateTime.Now}] IPC Off Error: {ex.Message}\n"); } catch { }
                            }
                        }));
                    }
                }
            });
            offThread.IsBackground = true;
            offThread.Start();

            var mainWindow = new MainWindow();
            if (!IsAutostartLaunch)
            {
                mainWindow.Show();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SmartLampApp.Services.TuyaProtocol.StopDaemon();
            base.OnExit(e);
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
        public static bool IsCommandLineLaunch { get; set; } = false;

        private bool HandleCommandLineArgs(string[] args)
        {
            string argStr = string.Join(" ", args).ToLowerInvariant();
            try { File.AppendAllText("cli.log", $"[{DateTime.Now}] Args: '{argStr}'\n"); } catch { }

            if (argStr.Contains("--install"))
            {
                SmartLampApp.Services.InstallationService.InstallToSystem(out var msg);
                Console.WriteLine(msg);
                return true;
            }
            if (argStr.Contains("--uninstall"))
            {
                SmartLampApp.Services.InstallationService.UninstallFromSystem(out var msg);
                Console.WriteLine(msg);
                return true;
            }
            if (argStr.Contains("--autostart"))
            {
                IsAutostartLaunch = true;
                return false; // Proceed to run normal WPF app, but minimized to tray
            }
            if (argStr.Contains("--test-local"))
            {
                var cfg = SmartLampApp.Models.ConfigManager.Load();
                var dev = cfg.devices.Count > 0 ? cfg.devices[0] : null;
                if (dev != null)
                {
                    Console.WriteLine($"Testing pure C# TuyaLocalClient against {dev.Ip} ({dev.DevId}, ver={dev.Version})...");
                    using var client = new SmartLampApp.Services.TuyaLocalClient(dev.Ip, dev.DevId, dev.LocalKey, dev.Version);
                    try
                    {
                        string status = client.GetStatusJsonAsync().GetAwaiter().GetResult();
                        Console.WriteLine($"STATUS SUCCESS: {status}");
                        File.AppendAllText("cli.log", $"[{DateTime.Now}] TestLocal Result: {status}\n");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"STATUS ERROR: {ex}");
                        File.AppendAllText("cli.log", $"[{DateTime.Now}] TestLocal Error: {ex}\n");
                    }
                }
                return true;
            }
            if (argStr.Contains("--test-toggle"))
            {
                var cfg = SmartLampApp.Models.ConfigManager.Load();
                var dev = cfg.devices.Count > 0 ? cfg.devices[0] : null;
                if (dev != null)
                {
                    Console.WriteLine($"Testing SetDpsAsync against {dev.Ip}...");
                    using var client = new SmartLampApp.Services.TuyaLocalClient(dev.Ip, dev.DevId, dev.LocalKey, dev.Version);
                    try
                    {
                        string status = client.GetStatusJsonAsync().GetAwaiter().GetResult();
                        bool currentPower = status.Contains("\"20\":true");
                        bool targetPower = !currentPower;
                        Console.WriteLine($"Current power: {currentPower}, toggling to: {targetPower}");
                        bool ok = client.SetDpsAsync(new Dictionary<string, object> { ["20"] = targetPower }).GetAwaiter().GetResult();
                        Console.WriteLine($"SET_DPS RESULT: {ok}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"SET_DPS ERROR: {ex}");
                    }
                }
                return true;
            }

            if (!argStr.Contains("--toggle") && !argStr.Contains("-t") &&
                !argStr.Contains("--on") && !argStr.Contains("--off") &&
                !argStr.Contains("--brightness") && !argStr.Contains("--temp") &&
                !argStr.Contains("--color"))
            {
                return false;
            }

            // If an active instance of SmartLampApp is already running, delegate command via Win32 EventWaitHandle for instant response
            if (argStr.Contains("--toggle") || argStr.Contains("-t"))
            {
                try
                {
                    using var evt = EventWaitHandle.OpenExisting("SmartLampApp_ToggleEvent");
                    evt.Set();
                    try { File.AppendAllText("cli.log", $"[{DateTime.Now}] Delegated --toggle to active instance.\n"); } catch { }
                    return true;
                }
                catch { }
            }
            else if (argStr.Contains("--on"))
            {
                try
                {
                    using var evt = EventWaitHandle.OpenExisting("SmartLampApp_OnEvent");
                    evt.Set();
                    try { File.AppendAllText("cli.log", $"[{DateTime.Now}] Delegated --on to active instance.\n"); } catch { }
                    return true;
                }
                catch { }
            }
            else if (argStr.Contains("--off"))
            {
                try
                {
                    using var evt = EventWaitHandle.OpenExisting("SmartLampApp_OffEvent");
                    evt.Set();
                    try { File.AppendAllText("cli.log", $"[{DateTime.Now}] Delegated --off to active instance.\n"); } catch { }
                    return true;
                }
                catch { }
            }

            try
            {
                var config = SmartLampApp.Models.ConfigManager.Load();
                var protocol = new SmartLampApp.Services.TuyaProtocol(config);
                var active = protocol.GetActiveDevice();

                bool hasCloud = !string.IsNullOrWhiteSpace(config.access_id) && !string.IsNullOrWhiteSpace(config.access_key);
                bool hasLocal = active != null && !string.IsNullOrWhiteSpace(active.LocalKey);

                if (!hasCloud && !hasLocal)
                {
                    try { File.AppendAllText("cli.log", $"[{DateTime.Now}] Device not configured (missing keys)\n"); } catch { }
                    return true;
                }

                System.Threading.Tasks.Task.Run(async () =>
                {
                    bool success = false;
                    if (argStr.Contains("--on"))
                    {
                        success = await protocol.SetPowerAsync(true);
                    }
                    else if (argStr.Contains("--off"))
                    {
                        success = await protocol.SetPowerAsync(false);
                    }
                    else if (argStr.Contains("--toggle") || argStr.Contains("-t"))
                    {
                        success = await protocol.TogglePowerAsync();
                    }

                    try { File.AppendAllText("cli.log", $"[{DateTime.Now}] Command '{argStr}' executed. Success: {success}\n"); } catch { }

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
            catch (Exception ex)
            {
                try { File.AppendAllText("cli.log", $"[{DateTime.Now}] CLI Error: {ex.Message}\n"); } catch { }
            }

            return true;
        }
    }
}
