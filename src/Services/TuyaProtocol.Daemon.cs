// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SmartLampApp.Services
{
    public partial class TuyaProtocol
    {
        private static Process? _bridgeDaemon = null;
        private static string? _extractedExePath = null;
        private const int ServerPort = 18889;
        private static readonly SemaphoreSlim _daemonLock = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        private static string GetBridgeExecutablePath()
        {
            if (_extractedExePath != null && File.Exists(_extractedExePath)) return _extractedExePath;

            // 1. Check local directory
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tuya_bridge.exe");
            if (File.Exists(localPath))
            {
                _extractedExePath = localPath;
                return localPath;
            }

            // 2. Extract embedded resource if not found in local dir
            string tempDir = Path.Combine(Path.GetTempPath(), "SmartLampStudio");
            Directory.CreateDirectory(tempDir);
            string tempPath = Path.Combine(tempDir, "tuya_bridge.exe");

            if (!File.Exists(tempPath))
            {
                try
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    using var stream = assembly.GetManifestResourceStream("SmartLampApp.TuyaBridge.tuya_bridge.exe");
                    if (stream != null)
                    {
                        using var fileStream = File.Create(tempPath);
                        stream.CopyTo(fileStream);
                    }
                }
                catch { }
            }

            _extractedExePath = File.Exists(tempPath) ? tempPath : "python";
            return _extractedExePath;
        }

        private static void EnsureBridgeDaemonRunning()
        {
            _ = EnsureBridgeDaemonRunningAsync();
        }

        private static async Task EnsureBridgeDaemonRunningAsync()
        {
            await _daemonLock.WaitAsync().ConfigureAwait(false);
            try
            {
                try
                {
                    var res = await _httpClient.GetAsync($"http://127.0.0.1:{ServerPort}/health").ConfigureAwait(false);
                    if (res.IsSuccessStatusCode) return;
                }
                catch { }

                if ((_bridgeDaemon != null && !_bridgeDaemon.HasExited) || Process.GetProcessesByName("tuya_bridge").Length > 0)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        await Task.Delay(250).ConfigureAwait(false);
                        try
                        {
                            var res = await _httpClient.GetAsync($"http://127.0.0.1:{ServerPort}/health").ConfigureAwait(false);
                            if (res.IsSuccessStatusCode) return;
                        }
                        catch { }
                    }
                }

                string exePath = GetBridgeExecutablePath();
                if (exePath == "python" || !File.Exists(exePath)) return;

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"--server {ServerPort}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                _bridgeDaemon = Process.Start(psi);
                if (_bridgeDaemon != null)
                {
                    ChildProcessTracker.AddProcess(_bridgeDaemon);
                }

                // Wait for the daemon to become ready after starting
                for (int i = 0; i < 40; i++) // up to 10 seconds
                {
                    await Task.Delay(250).ConfigureAwait(false);
                    try
                    {
                        var res = await _httpClient.GetAsync($"http://127.0.0.1:{ServerPort}/health").ConfigureAwait(false);
                        if (res.IsSuccessStatusCode) return;
                    }
                    catch { }
                }
            }
            catch { }
            finally
            {
                _daemonLock.Release();
            }
        }

        public async Task WaitForBridgeReadyAsync()
        {
            await EnsureBridgeDaemonRunningAsync();
        }

        public static void StopDaemon()
        {
            try
            {
                if (_bridgeDaemon != null && !_bridgeDaemon.HasExited)
                {
                    _bridgeDaemon.Kill(true);
                    _bridgeDaemon.Dispose();
                    _bridgeDaemon = null;
                }
            }
            catch { }
        }

        private static async Task<bool> IsBridgeDaemonRunningAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(150);
                var req = await _httpClient.GetAsync($"http://127.0.0.1:{ServerPort}/health", cts.Token);
                return req.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<string> SendBridgeRequestAsync(object payload, string rawPythonFallback)
        {
            await _semaphore.WaitAsync();
            try
            {
                EnsureBridgeDaemonRunning();
                try
                {
                    string json = JsonSerializer.Serialize(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync($"http://127.0.0.1:{ServerPort}", content);
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStringAsync();
                    }
                }
                catch { }

                // Fallback to single-process CLI if daemon is unreachable
                return RunPythonCommandCLI(rawPythonFallback);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private string RunPythonCommandCLI(string pythonCode)
        {
            if (string.IsNullOrWhiteSpace(pythonCode)) return "";
            try
            {
                string exePath = GetBridgeExecutablePath();
                if (string.IsNullOrWhiteSpace(exePath) || (!File.Exists(exePath) && exePath != "python")) return "";

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"-c \"{pythonCode}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                if (process == null) return "";
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output;
            }
            catch
            {
                return "";
            }
        }
    }
}
