// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.FileSystem;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Dto;
using NoMercy.Storage;
using Serilog.Events;
using Downloader = NoMercy.NmSystem.SystemCalls.Download;
using FileAttributes = NoMercy.NmSystem.FileSystem.FileAttributes;
using HttpClient = System.Net.Http.HttpClient;

namespace NoMercy.Setup.Server;

public enum ServerUpdateResult
{
    Downloaded,
    AlreadyUpToDate,
    UseInstaller,
    RestartNeeded,
    NoAssetFound,
}

public class Binaries
{
    private readonly IStorageDriver _driver;
    private readonly IStorage _storage;
    private readonly HttpClient _httpClient;

    private const string GithubMediaServerApiUrl =
        "https://api.github.com/repos/NoMercy-Entertainment/nomercy-media-server/releases/latest";
    private const string GithubFfmpegApiUrl =
        "https://api.github.com/repos/NoMercy-Entertainment/nomercy-ffmpeg/releases/latest";
    private const string GithubTesseractApiUrl =
        "https://api.github.com/repos/NoMercy-Entertainment/nomercy-tesseract/releases/latest";
    private const string GithubWhisperModelApiUrl =
        "https://api.github.com/repos/NoMercy-Entertainment/nomercy-whisper-models/releases/latest";

    private const string GithubYtdlpApiUrl =
        "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";
    private const string GithubCloudflaredApiUrl =
        "https://api.github.com/repos/cloudflare/cloudflared/releases/latest";

    public Binaries(IStorageDriver driver, IStorage storage)
    {
        _driver = driver;
        _storage = storage;
        _httpClient = new()
        {
            // Default HttpClient.Timeout is 100s which is fine for API calls
            // but binary downloads can be hundreds of MB. Cap at 10 minutes
            // so a stuck CDN connection eventually surfaces as a TaskCanceled
            // instead of pinning the setup phase forever.
            Timeout = TimeSpan.FromMinutes(10),
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", ExternalServicesConfig.Current.UserAgent);
    }

    /// <summary>
    /// Returns true when the binary exists in an installer directory (not the binaries path).
    /// This prevents redundant downloads when binaries were installed by an installer.
    /// </summary>
    private bool ExistsInInstalledDirectory(string executableName)
    {
        // Check the Launcher's install directory (set for installer deployments only)
        string? installDir = Environment.GetEnvironmentVariable("NOMERCY_INSTALL_DIR");
        if (
            !string.IsNullOrEmpty(installDir)
            && _driver.FileExists(Path.Combine(installDir, executableName))
        )
            return true;

        // Also check the server's own directory, but only if it differs from the binaries path
        // (otherwise this is a standalone deployment and we DO want to download updates)
        string? ownDir = Path.GetDirectoryName(
            Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location
        );

        if (
            ownDir is not null
            && !string.Equals(
                Path.GetFullPath(ownDir),
                Path.GetFullPath(AppFiles.BinariesPath),
                StringComparison.OrdinalIgnoreCase
            )
            && _driver.FileExists(Path.Combine(ownDir, executableName))
        )
            return true;

        return false;
    }

    private readonly List<string> _binaryReport = new();

    public Task DownloadAll()
    {
        return Task.Run(async () =>
        {
            Logger.Setup("Downloading Binaries");

            await DownloadApp();
            await DownloadLauncher();
            await DownloadCli();
            await DownloadServerUpdate();
            await DownloadFfmpeg();
            await DownloadCloudflared();
            await DownloadYtdlp();
            await DownloadWhisperModels(AppFiles.WhisperModel);

            List<string> tesseractLanguages = ["eng", "jpn"];
            if (!CultureInfo.CurrentCulture.Equals(CultureInfo.InvariantCulture))
            {
                string currentCulture = CultureInfo.CurrentCulture.EnglishLanguageTag();
                if (
                    !string.IsNullOrEmpty(currentCulture)
                    && !tesseractLanguages.Contains(currentCulture)
                )
                    tesseractLanguages.Add(currentCulture);
            }
            await DownloadTesseractData(tesseractLanguages);

            if (_binaryReport.Count > 0)
            {
                const int columns = 3;
                int rows = (_binaryReport.Count + columns - 1) / columns;
                int[] columnWidth = new int[columns];
                for (int i = 0; i < _binaryReport.Count; i++)
                {
                    int col = i % columns;
                    if (_binaryReport[i].Length > columnWidth[col])
                        columnWidth[col] = _binaryReport[i].Length;
                }

                System.Text.StringBuilder report = new();
                report.Append($"Binaries up to date ({_binaryReport.Count}):");
                for (int row = 0; row < rows; row++)
                {
                    report.Append('\n').Append("  ");
                    for (int col = 0; col < columns; col++)
                    {
                        int index = (row * columns) + col;
                        if (index >= _binaryReport.Count)
                            break;

                        bool last = col == columns - 1 || index == _binaryReport.Count - 1;
                        report.Append(
                            last
                                ? _binaryReport[index]
                                : _binaryReport[index].PadRight(columnWidth[col] + 2)
                        );
                    }
                }

                Logger.Setup(report.ToString(), LogEventLevel.Verbose);
            }
        });
    }

    private bool CheckLocalVersion(
        GithubReleaseResponse releaseInfo,
        string destination,
        out string version
    )
    {
        version = releaseInfo.TagName.StartsWith("v")
            ? releaseInfo.TagName[1..]
            : releaseInfo.TagName;

        bool fileExists = _storage.Exists(destination);
        if (!fileExists)
            return false;

        DateTime creationTime = _storage.LastModified(destination).UtcDateTime;
        DateTimeOffset releaseDate =
            releaseInfo.PublishedAt != DateTimeOffset.MinValue
                ? releaseInfo.PublishedAt.UtcDateTime
                : DateTimeOffset.Now;

        return creationTime >= releaseDate;
    }

    private async Task<GithubReleaseResponse> GetLatestReleaseInfo(string apiUrl)
    {
        int attempt = 0;
        TimeSpan backoff = TimeSpan.FromSeconds(30);

        while (true)
        {
            attempt++;
            try
            {
                using HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);

                if (
                    response.StatusCode
                    is HttpStatusCode.Forbidden
                        or HttpStatusCode.TooManyRequests
                )
                {
                    TimeSpan waitTime = backoff;

                    if (
                        response.Headers.TryGetValues(
                            "X-RateLimit-Reset",
                            out IEnumerable<string>? values
                        )
                    )
                    {
                        string? resetValue = values.FirstOrDefault();
                        if (resetValue is not null && long.TryParse(resetValue, out long resetUnix))
                        {
                            DateTimeOffset resetTime = DateTimeOffset.FromUnixTimeSeconds(
                                resetUnix
                            );
                            TimeSpan untilReset = resetTime - DateTimeOffset.UtcNow;
                            if (untilReset > TimeSpan.Zero)
                                waitTime = untilReset + TimeSpan.FromSeconds(2);
                        }
                    }

                    Logger.Setup(
                        $"GitHub API rate limited (attempt {attempt}), waiting {waitTime.TotalSeconds:F0}s to retry: {apiUrl}",
                        LogEventLevel.Warning
                    );

                    await Task.Delay(waitTime);
                    backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 600));
                    continue;
                }

                response.EnsureSuccessStatusCode();

                string jsonResponse = await response.Content.ReadAsStringAsync();

                return jsonResponse.FromJson<GithubReleaseResponse>()
                    ?? new GithubReleaseResponse();
            }
            catch (Exception e)
            {
                Logger.Setup(
                    $"Error fetching release info from {apiUrl}: {e.Message}",
                    LogEventLevel.Warning
                );
                return new();
            }
        }
    }

    private async Task DownloadApp()
    {
        if (ExistsInInstalledDirectory("NoMercyApp" + Info.ExecSuffix))
        {
            Logger.Setup(
                "App found in installed directory, skipping download",
                LogEventLevel.Verbose
            );
            return;
        }

        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfo(GithubMediaServerApiUrl);
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup("No assets found for App release.", LogEventLevel.Warning);
            return;
        }

        if (CheckLocalVersion(releaseInfo, AppFiles.AppExePath, out string version))
        {
            _binaryReport.Add($"App = {version}");
            return;
        }

        await Downloader.DeleteSourceDownload(_storage, AppFiles.AppExePath);

        Uri? downloadUrl = null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a =>
                    a.Name.Equals("NoMercyApp-windows-x64.exe", StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.Arm64
        )
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a =>
                    a.Name.Equals("NoMercyApp-linux-arm64", StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.X64
        )
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a =>
                    a.Name.Equals("NoMercyApp-linux-x64", StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a =>
                    a.Name.Equals("NoMercyApp-macos-x64.dmg", StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }

        if (downloadUrl == null)
        {
            Logger.Setup(
                "No suitable NoMercyApp asset found for the current platform.",
                LogEventLevel.Warning
            );
            return;
        }

        string path = await Downloader.DownloadFile(
            _storage,
            "NoMercyApp",
            downloadUrl,
            AppFiles.AppExePath
        );

        await FileAttributes.SetCreatedAttribute(path, releaseInfo.PublishedAt);

        await FilePermissions.SetExecutionPermissions(path);
    }

    private async Task DownloadLauncher()
    {
        if (ExistsInInstalledDirectory("NoMercyLauncher" + Info.ExecSuffix))
        {
            Logger.Setup(
                "Launcher found in installed directory, skipping download",
                LogEventLevel.Verbose
            );
            return;
        }

        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfo(GithubMediaServerApiUrl);
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup("No assets found for Launcher release.", LogEventLevel.Warning);
            return;
        }

        if (CheckLocalVersion(releaseInfo, AppFiles.LauncherExePath, out string version))
        {
            _binaryReport.Add($"Launcher = {version}");
            return;
        }

        await Downloader.DeleteSourceDownload(_storage, AppFiles.LauncherExePath);

        Uri? downloadUrl = null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a =>
                    a.Name.Equals(
                        "NoMercyLauncher-windows-x64.exe",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.Arm64
        )
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a =>
                    a.Name.Equals("NoMercyLauncher-linux-arm64", StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.X64
        )
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a =>
                    a.Name.Equals("NoMercyLauncher-linux-x64", StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a =>
                    a.Name.Equals("NoMercyLauncher-macos-x64", StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }

        if (downloadUrl == null)
        {
            Logger.Setup(
                "No suitable NoMercyLauncher asset found for the current platform.",
                LogEventLevel.Warning
            );
            return;
        }

        string path = await Downloader.DownloadFile(
            _storage,
            "NoMercyLauncher",
            downloadUrl,
            AppFiles.LauncherExePath
        );

        await FileAttributes.SetCreatedAttribute(path, releaseInfo.PublishedAt);

        await FilePermissions.SetExecutionPermissions(path);
    }

    private async Task DownloadCli()
    {
        if (ExistsInInstalledDirectory("nomercy" + Info.ExecSuffix))
        {
            Logger.Setup(
                "CLI found in installed directory, skipping download",
                LogEventLevel.Verbose
            );
            return;
        }

        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfo(GithubMediaServerApiUrl);
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup("No assets found for CLI release.", LogEventLevel.Warning);
            return;
        }

        if (CheckLocalVersion(releaseInfo, AppFiles.CliExePath, out string version))
        {
            _binaryReport.Add($"CLI = {version}");
            return;
        }

        await Downloader.DeleteSourceDownload(_storage, AppFiles.CliExePath);

        Uri? downloadUrl = null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a =>
                    a.Name.Equals("nomercy-windows-x64.exe", StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.Arm64
        )
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a =>
                    a.Name.Equals("nomercy-linux-arm64", StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.X64
        )
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a =>
                    a.Name.Equals("nomercy-linux-x64", StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a =>
                    a.Name.Equals("nomercy-macos-x64", StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }

        if (downloadUrl == null)
        {
            Logger.Setup(
                "No suitable nomercy CLI asset found for the current platform.",
                LogEventLevel.Warning
            );
            return;
        }

        string path = await Downloader.DownloadFile(
            _storage,
            "nomercy",
            downloadUrl,
            AppFiles.CliExePath
        );

        await FileAttributes.SetCreatedAttribute(path, releaseInfo.PublishedAt);

        await FilePermissions.SetExecutionPermissions(path);
    }

    public async Task<ServerUpdateResult> DownloadServerUpdate()
    {
        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfo(GithubMediaServerApiUrl);
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup("No assets found for Server release.", LogEventLevel.Warning);
            return ServerUpdateResult.NoAssetFound;
        }

        string latestVersion = releaseInfo.TagName.StartsWith("v")
            ? releaseInfo.TagName[1..]
            : releaseInfo.TagName;

        string currentVersion = Software.GetReleaseVersion();

        if (string.Equals(latestVersion, currentVersion, StringComparison.OrdinalIgnoreCase))
        {
            _binaryReport.Add($"Server = {currentVersion}");
            return ServerUpdateResult.AlreadyUpToDate;
        }

        if (
            Version.TryParse(latestVersion, out Version? latest)
            && Version.TryParse(currentVersion, out Version? current)
            && latest <= current
        )
        {
            _binaryReport.Add($"Server = {currentVersion}");
            return ServerUpdateResult.AlreadyUpToDate;
        }

        // Installer deployment: the installer handles updates, don't download to binaries path
        string? installDir = Environment.GetEnvironmentVariable("NOMERCY_INSTALL_DIR");
        if (!string.IsNullOrEmpty(installDir))
        {
            Logger.Setup(
                $"Server update available: {currentVersion} -> {latestVersion} (use installer to update)"
            );
            return ServerUpdateResult.UseInstaller;
        }

        string? onDiskVersion = Software.GetFileVersion(_driver, AppFiles.ServerExePath);
        if (
            onDiskVersion is not null
            && string.Equals(latestVersion, onDiskVersion, StringComparison.OrdinalIgnoreCase)
        )
        {
            Logger.Setup(
                $"Server binary on disk is already {onDiskVersion} (running {currentVersion}), restart needed to apply"
            );
            return ServerUpdateResult.RestartNeeded;
        }

        Logger.Setup($"Server update available: {currentVersion} -> {latestVersion}");

        await Downloader.DeleteSourceDownload(_storage, AppFiles.ServerTempExePath);

        Uri? downloadUrl = null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a =>
                    a.Name.Equals(
                        "NoMercyMediaServer-windows-x64.exe",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.Arm64
        )
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a =>
                    a.Name.Equals(
                        "NoMercyMediaServer-linux-arm64",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.X64
        )
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a =>
                    a.Name.Equals(
                        "NoMercyMediaServer-linux-x64",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                ?.BrowserDownloadUrl;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a =>
                    a.Name.Equals(
                        "NoMercyMediaServer-macos-x64",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                ?.BrowserDownloadUrl;
        }

        if (downloadUrl == null)
        {
            Logger.Setup(
                "No suitable NoMercyMediaServer asset found for the current platform.",
                LogEventLevel.Warning
            );
            return ServerUpdateResult.NoAssetFound;
        }

        string path = await Downloader.DownloadFile(
            _storage,
            "NoMercyMediaServer Update",
            downloadUrl,
            AppFiles.ServerTempExePath
        );

        // Wait for the file to become available (antivirus scanning can briefly lock/quarantine it)
        bool fileReady = false;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (_storage.Exists(path) && _storage.SizeOrZero(path) > 0)
            {
                fileReady = true;
                break;
            }

            Logger.Setup(
                $"Waiting for staged update file to become available (attempt {attempt + 1}/5)...",
                LogEventLevel.Debug
            );
            await Task.Delay(1000);
        }

        if (!fileReady)
        {
            Logger.Setup(
                $"Staged update file not available at {path} after download",
                LogEventLevel.Error
            );
            return ServerUpdateResult.NoAssetFound;
        }

        await FileAttributes.SetCreatedAttribute(path, releaseInfo.PublishedAt);

        await FilePermissions.SetExecutionPermissions(path);

        Logger.Setup($"Server update staged at {path} ({_storage.SizeOrZero(path)} bytes)");
        return ServerUpdateResult.Downloaded;
    }

    private async Task DownloadFfmpeg()
    {
        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfo(GithubFfmpegApiUrl);
        if (releaseInfo.Assets.Length == 0)
        {
            if (!_storage.Exists(AppFiles.FfmpegPath))
                throw new InvalidOperationException(
                    "FFmpeg is not installed and release info could not be fetched. Will retry."
                );

            Logger.Setup(
                "No assets found for FFMpeg release, keeping existing binaries.",
                LogEventLevel.Warning
            );
            return;
        }

        if (CheckLocalVersion(releaseInfo, AppFiles.FfmpegPath, out string version))
        {
            _binaryReport.Add($"Ffmpeg = {version}");
            return;
        }

        // Skip the update when ffmpeg is locked by a running encode — the zip
        // extraction would fail mid-way, leave AppFiles.FfmpegFolder in a partial
        // state, and trip the Phase 4 "required startup task failed" alert. The
        // update will land on the next boot when no encode is in flight.
        if (_storage.Exists(AppFiles.FfmpegPath) && Locking.IsFileLocked(AppFiles.FfmpegPath))
        {
            Logger.Setup(
                "FFmpeg binary is in use by a running encode — deferring update to next boot.",
                LogEventLevel.Information
            );
            return;
        }

        await Downloader.DeleteSourceDownload(_storage, AppFiles.FfmpegPath);
        await Downloader.DeleteSourceDownload(_storage, AppFiles.FfProbePath);
        await Downloader.DeleteSourceDownload(_storage, AppFiles.FfPlayPath);

        Uri? downloadUrl = null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a => a.Name.Contains("windows"))
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.Arm64
        )
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a => a.Name.Contains("linux") && a.Name.Contains("aarch64"))
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.X64
        )
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a => a.Name.Contains("linux") && a.Name.Contains("x86_64"))
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            && RuntimeInformation.ProcessArchitecture == Architecture.Arm64
        )
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a => a.Name.Contains("darwin") && a.Name.Contains("arm64"))
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            && RuntimeInformation.ProcessArchitecture == Architecture.X64
        )
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a => a.Name.Contains("darwin") && a.Name.Contains("x86_64"))
                ?.BrowserDownloadUrl;
        }

        if (downloadUrl == null)
        {
            Logger.Setup(
                "No suitable FFMpeg asset found for the current platform.",
                LogEventLevel.Warning
            );
            return;
        }

        string path = await Downloader.DownloadFile(_storage, "FFMpeg", downloadUrl);

        // Re-check the lock right before extraction — the encoder worker may have
        // reserved a job and started running ffmpeg between the pre-download check
        // and the multi-minute zip download finishing. Without this gate, extraction
        // races the encode: ExtractToFile deletes ffmpeg.exe, in-flight Process.Start
        // hits ERROR_FILE_NOT_FOUND, encode fails. Keep the downloaded zip so the
        // update lands on the next boot when no encode is in flight.
        if (_storage.Exists(AppFiles.FfmpegPath) && Locking.IsFileLocked(AppFiles.FfmpegPath))
        {
            Logger.Setup(
                "FFmpeg binary became locked by a running encode while the update downloaded — "
                    + "deferring extraction to next boot.",
                LogEventLevel.Information
            );
            return;
        }

        List<string> files = await Archiving.ExtractArchive(_storage, path, AppFiles.FfmpegFolder);
        foreach (string file in files)
        {
            await FileAttributes.SetCreatedAttribute(file, releaseInfo.PublishedAt);
            await FilePermissions.SetExecutionPermissions(file);
        }

        await Downloader.DeleteSourceDownload(_storage, path);
    }

    private async Task DownloadYtdlp()
    {
        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfo(GithubYtdlpApiUrl);
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup("No assets found for yt-dlp release.", LogEventLevel.Warning);
            return;
        }

        if (CheckLocalVersion(releaseInfo, AppFiles.YtdlpPath, out string version))
        {
            _binaryReport.Add($"Yt-dlp = {version}");
            return;
        }

        await Downloader.DeleteSourceDownload(_storage, AppFiles.YtdlpPath);

        Uri? downloadUrl = null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a =>
                    a.Name.Equals("yt-dlp_x86.exe", StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.Arm64
        )
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a =>
                    a.Name.Equals("yt-dlp_linux_aarch64", StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.X64
        )
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a =>
                    a.Name.Equals("yt-dlp_linux", StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a =>
                    a.Name.Equals("yt-dlp_macos", StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }

        if (downloadUrl == null)
        {
            Logger.Setup(
                "No suitable yt-dlp asset found for the current platform.",
                LogEventLevel.Warning
            );
            return;
        }

        string outputPath = await Downloader.DownloadFile(
            _storage,
            "yt-dlp",
            downloadUrl,
            AppFiles.YtdlpPath
        );

        await FileAttributes.SetCreatedAttribute(outputPath, releaseInfo.PublishedAt);

        await FilePermissions.SetExecutionPermissions(outputPath);

        Logger.Setup($"Downloaded yt-dlp to {outputPath}");
    }

    private async Task DownloadCloudflared()
    {
        string destinationPath = AppFiles.CloudflareDPath;

        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfo(GithubCloudflaredApiUrl);
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup("No assets found for cloudflared release.", LogEventLevel.Warning);
            return;
        }

        if (CheckLocalVersion(releaseInfo, destinationPath, out string version))
        {
            _binaryReport.Add($"Cloudflared = {version}");
            return;
        }

        await Downloader.DeleteSourceDownload(_storage, destinationPath);

        Uri? downloadUrl = null;
        bool needsExtraction = false;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a => a.Name.Equals("cloudflared-windows-amd64.exe"))
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.Arm64
        )
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a => a.Name.Equals("cloudflared-linux-arm"))
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.X64
        )
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a => a.Name.Equals("cloudflared-linux-amd64"))
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            && RuntimeInformation.ProcessArchitecture == Architecture.Arm64
        )
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a => a.Name.Equals("cloudflared-darwin-arm64.tgz"))
                ?.BrowserDownloadUrl;
            needsExtraction = true;
        }
        else if (
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            && RuntimeInformation.ProcessArchitecture == Architecture.X64
        )
        {
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a => a.Name.Equals("cloudflared-darwin-amd64.tgz"))
                ?.BrowserDownloadUrl;
            needsExtraction = true;
        }

        if (downloadUrl == null)
        {
            Logger.Setup(
                "No suitable cloudflared asset found for the current platform.",
                LogEventLevel.Warning
            );
            return;
        }

        string path = await Downloader.DownloadFile(_storage, "cloudflared", downloadUrl);

        Logger.Setup($"Downloaded cloudflared to {path}");

        if (needsExtraction)
        {
            List<string> files = await Archiving.ExtractArchive(
                _storage,
                path,
                AppFiles.DependenciesPath
            );
            foreach (string file in files)
            {
                await FileAttributes.SetCreatedAttribute(file, releaseInfo.PublishedAt);
                await FilePermissions.SetExecutionPermissions(file);
            }
            await Downloader.DeleteSourceDownload(_storage, path);
        }
        else
        {
            if (_storage.Exists(destinationPath))
                _storage.Delete(destinationPath);

            _storage.Move(path, destinationPath);

            await FileAttributes.SetCreatedAttribute(destinationPath, releaseInfo.PublishedAt);

            await FilePermissions.SetExecutionPermissions(destinationPath);
        }
    }

    private async Task DownloadWhisperModels(string modelName = "ggml-large-v3")
    {
        string destinationPath = Path.Combine(AppFiles.FfmpegFolder, modelName + ".bin");

        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfo(GithubWhisperModelApiUrl);
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup(
                "No assets found for nomercy-whisper-models release.",
                LogEventLevel.Warning
            );
            return;
        }

        if (CheckLocalVersion(releaseInfo, destinationPath, out string version))
        {
            _binaryReport.Add($"Whisper = {version}");
            return;
        }

        await Downloader.DeleteSourceDownload(_storage, destinationPath);

        List<Uri> downloadUrls = releaseInfo
            .Assets.Where(a => a.Name.Contains(modelName, StringComparison.OrdinalIgnoreCase))
            .Select(a => a.BrowserDownloadUrl)
            .ToList();

        if (downloadUrls.Count == 0)
        {
            Logger.Setup(
                $"No assets found for model {modelName} in nomercy-whisper-models release.",
                LogEventLevel.Warning
            );
            return;
        }

        List<string> paths = [];
        foreach (Uri downloadUrl in downloadUrls)
        {
            paths.Add(
                await Downloader.DownloadFile(_storage, "nomercy-whisper-models", downloadUrl)
            );
        }

        if (downloadUrls.Count > 1)
        {
            string outputPath = await ConcatenateModelParts(modelName, downloadUrls);

            foreach (string path in paths)
            {
                await Downloader.DeleteSourceDownload(_storage, path);
            }

            await FileAttributes.SetCreatedAttribute(outputPath, releaseInfo.PublishedAt);

            Logger.Setup($"Downloaded and concatenated Whisper model parts to {outputPath}");
        }
        else
        {
            Logger.Setup($"Downloaded Whisper model to {paths[0]}");
        }
    }

    private async Task<string> ConcatenateModelParts(string modelName, IEnumerable<Uri> partUrls)
    {
        string destinationPath = Path.Combine(AppFiles.FfmpegFolder, modelName + ".bin");

        await using Stream destinationStream = _driver.OpenWrite(destinationPath, overwrite: true);

        foreach (Uri partUrl in partUrls)
        {
            string partPath = Path.Combine(
                AppFiles.DependenciesPath,
                Path.GetFileName(partUrl.ToString())
            );

            await using Stream partStream = _driver.OpenRead(partPath);
            await partStream.CopyToAsync(destinationStream);
        }

        Logger.Setup($"Concatenated model parts into {destinationPath}", LogEventLevel.Verbose);

        return destinationPath;
    }

    private async Task DownloadTesseractData(IEnumerable<string> languages)
    {
        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfo(GithubTesseractApiUrl);
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup("No assets found for TesseractData release.", LogEventLevel.Warning);
            return;
        }

        foreach (string lang in languages)
        {
            Uri? downloadUrl = releaseInfo
                .Assets.FirstOrDefault(a =>
                    a.Name.Equals($"{lang}.traineddata", StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;

            if (downloadUrl == null)
            {
                Logger.Setup(
                    $"No asset found for language {lang} in TesseractData release.",
                    LogEventLevel.Warning
                );
                continue;
            }

            string destinationPath = Path.Combine(
                AppFiles.TesseractModelsFolder,
                $"{lang}.traineddata"
            );

            if (CheckLocalVersion(releaseInfo, destinationPath, out string version))
            {
                _binaryReport.Add($"Tesseract[{lang}] = {version}");
                continue;
            }

            await Downloader.DeleteSourceDownload(_storage, destinationPath);

            string path = await Downloader.DownloadFile(
                _storage,
                $"Tesseract data for {lang}",
                downloadUrl,
                $"{lang}.traineddata"
            );

            _storage.Move(path, destinationPath);

            await FileAttributes.SetCreatedAttribute(destinationPath, releaseInfo.PublishedAt);

            Logger.Setup($"Downloaded Tesseract data for {lang} to {destinationPath}");
        }
    }
}
