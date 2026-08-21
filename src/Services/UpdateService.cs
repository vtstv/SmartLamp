// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace SmartLampApp.Services
{
    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; } = false;
        public string LatestVersion { get; set; } = "";
        public string CurrentVersion { get; set; } = "";
        public string ReleaseTitle { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string FileName { get; set; } = "";
        public long FileSize { get; set; } = 0;
        public string HtmlUrl { get; set; } = "";
    }

    public static class UpdateService
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private const string GitHubApiLatestReleaseUrl = "https://api.github.com/repos/vtstv/SmartLamp/releases/latest";

        static UpdateService()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("SmartLampStudio", AppVersion.Version.Replace(" ", "_")));
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        }

        public static async Task<UpdateInfo> CheckForUpdatesAsync(string currentVersion)
        {
            var updateInfo = new UpdateInfo
            {
                CurrentVersion = currentVersion
            };

            try
            {
                using var response = await _httpClient.GetAsync(GitHubApiLatestReleaseUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return updateInfo;
                }

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
                string releaseTitle = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                string releaseNotes = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";
                string htmlUrl = root.TryGetProperty("html_url", out var htmlProp) ? htmlProp.GetString() ?? "" : "";

                string cleanRemoteVer = tagName.TrimStart('v', 'V').Trim();
                updateInfo.LatestVersion = cleanRemoteVer;
                updateInfo.ReleaseTitle = string.IsNullOrWhiteSpace(releaseTitle) ? $"Release v{cleanRemoteVer}" : releaseTitle;
                updateInfo.ReleaseNotes = releaseNotes;
                updateInfo.HtmlUrl = htmlUrl;

                if (IsNewerVersion(currentVersion, cleanRemoteVer))
                {
                    updateInfo.IsUpdateAvailable = true;

                    // Locate best asset: Prefer standalone exe or win-x64 zip
                    if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
                    {
                        string bestUrl = "";
                        string bestName = "";
                        long bestSize = 0;

                        foreach (var asset in assetsProp.EnumerateArray())
                        {
                            string assetName = asset.TryGetProperty("name", out var aName) ? aName.GetString() ?? "" : "";
                            string assetUrl = asset.TryGetProperty("browser_download_url", out var aUrl) ? aUrl.GetString() ?? "" : "";
                            long assetSize = asset.TryGetProperty("size", out var aSize) ? aSize.GetInt64() : 0;

                            // Priority 1: Standalone executable
                            if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                bestUrl = assetUrl;
                                bestName = assetName;
                                bestSize = assetSize;
                                break;
                            }

                            // Priority 2: win-x64 Zip package
                            if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && assetName.Contains("win-x64", StringComparison.OrdinalIgnoreCase))
                            {
                                bestUrl = assetUrl;
                                bestName = assetName;
                                bestSize = assetSize;
                            }
                            else if (string.IsNullOrEmpty(bestUrl) && assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                bestUrl = assetUrl;
                                bestName = assetName;
                                bestSize = assetSize;
                            }
                        }

                        updateInfo.DownloadUrl = bestUrl;
                        updateInfo.FileName = bestName;
                        updateInfo.FileSize = bestSize;
                    }
                }
            }
            catch
            {
                // Network unavailable or rate limit reached
            }

            return updateInfo;
        }

        public static bool IsNewerVersion(string currentVersion, string remoteVersion)
        {
            if (string.IsNullOrWhiteSpace(remoteVersion)) return false;

            string cleanCurrent = currentVersion.TrimStart('v', 'V').Trim();
            string cleanRemote = remoteVersion.TrimStart('v', 'V').Trim();

            if (Version.TryParse(cleanCurrent, out var currVer) && Version.TryParse(cleanRemote, out var remVer))
            {
                return remVer > currVer;
            }

            // Fallback numeric segment comparison
            var currParts = cleanCurrent.Split('.');
            var remParts = cleanRemote.Split('.');
            int maxLen = Math.Max(currParts.Length, remParts.Length);

            for (int i = 0; i < maxLen; i++)
            {
                int currNum = (i < currParts.Length && int.TryParse(currParts[i], out int c)) ? c : 0;
                int remNum = (i < remParts.Length && int.TryParse(remParts[i], out int r)) ? r : 0;

                if (remNum > currNum) return true;
                if (remNum < currNum) return false;
            }

            return false;
        }

        public static async Task<string> DownloadUpdateAsync(string downloadUrl, string fileName, IProgress<int> progress)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "SmartLampStudio_Update");
            Directory.CreateDirectory(tempDir);
            string destinationPath = Path.Combine(tempDir, fileName);

            if (File.Exists(destinationPath))
            {
                try { File.Delete(destinationPath); } catch { }
            }

            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1L;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0L;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalRead += bytesRead;

                if (totalBytes > 0)
                {
                    int percentage = (int)Math.Round((double)totalRead / totalBytes * 100);
                    progress?.Report(percentage);
                }
            }

            progress?.Report(100);
            return destinationPath;
        }

        public static void ApplyUpdateAndRestart(string downloadedFilePath)
        {
            string currentExePath = Environment.ProcessPath 
                ?? Process.GetCurrentProcess().MainModule?.FileName 
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SmartLampApp.exe");

            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            string scriptPath = Path.Combine(Path.GetTempPath(), "smartlamp_updater.ps1");

            bool isZip = downloadedFilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

            string scriptContent;
            if (isZip)
            {
                scriptContent = $@"
Start-Sleep -Seconds 1
$extractedDir = Join-Path $env:TEMP 'smartlamp_extracted'
if (Test-Path $extractedDir) {{ Remove-Item -Path $extractedDir -Recurse -Force }}
Expand-Archive -Path '{downloadedFilePath}' -DestinationPath $extractedDir -Force
Copy-Item -Path ""$extractedDir\*"" -Destination '{currentDir}' -Recurse -Force
Start-Sleep -Milliseconds 500
Start-Process -FilePath '{currentExePath}'
";
            }
            else
            {
                scriptContent = $@"
Start-Sleep -Seconds 1
Copy-Item -Path '{downloadedFilePath}' -Destination '{currentExePath}' -Force
Start-Sleep -Milliseconds 500
Start-Process -FilePath '{currentExePath}'
";
            }

            File.WriteAllText(scriptPath, scriptContent);

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(startInfo);

            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                System.Windows.Application.Current.Shutdown();
            });
        }
    }
}
