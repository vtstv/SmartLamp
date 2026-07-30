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
    }
}
