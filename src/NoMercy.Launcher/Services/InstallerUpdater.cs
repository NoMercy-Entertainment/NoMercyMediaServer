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

using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using NoMercy.Launcher.Models;

namespace NoMercy.Launcher.Services;

/// <summary>
/// Handles installer-based auto-update on Windows.
/// Mirrors Docker Desktop's pattern: stable cache dir, SHA-256 verify, prune stale installers.
/// </summary>
public class InstallerUpdater(ServerConnection serverConnection)
{
    private static readonly HttpClient HttpClient = new();

    private const string GithubReleasesApiUrl =
        "https://api.github.com/repos/NoMercy-Entertainment/nomercy-media-server/releases/latest";

    // %LocalAppData%\NoMercy\UpdateCache\
    private static string CacheDir =>
        Path.Combine(
            path1: Environment.GetFolderPath(folder: Environment.SpecialFolder.LocalApplicationData),
            path2: "NoMercy",
            path3: "UpdateCache"
        );

    static InstallerUpdater()
    {
        HttpClient.DefaultRequestHeaders.Add(name: "User-Agent", value: "NoMercyLauncher/1.0");
    }

    /// <summary>
    /// True when running from an installer deployment (NOMERCY_INSTALL_DIR env is set,
    /// or the launcher lives outside the binaries path).
    /// </summary>
    public Task<bool> IsInstallerDeploymentAsync()
    {
        string? installDir = Environment.GetEnvironmentVariable(variable: "NOMERCY_INSTALL_DIR");
        if (!string.IsNullOrEmpty(value: installDir))
            return Task.FromResult(result: true);

        string? ownDir = Path.GetDirectoryName(
            path: Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location
        );

        if (ownDir is null)
            return Task.FromResult(result: false);

        string binariesPath = Path.Combine(
            path1: Environment.GetFolderPath(folder: Environment.SpecialFolder.ApplicationData),
            path2: "NoMercy",
            path3: "binaries"
        );

        bool isInBinaries = string.Equals(
            a: Path.GetFullPath(path: ownDir),
            b: Path.GetFullPath(path: binariesPath),
            comparisonType: StringComparison.OrdinalIgnoreCase
        );

        return Task.FromResult(result: !isInBinaries);
    }

    /// <summary>
    /// GET /manage/activity — returns stream and encode counts from the running server.
    /// </summary>
    public async Task<ActivityInfo?> GetActivityAsync(CancellationToken ct = default)
    {
        return await serverConnection.GetAsync<ActivityInfo>(path: "/manage/activity", cancellationToken: ct);
    }

    /// <summary>
    /// Downloads the installer to the stable cache dir.
    /// Skips download if the file already exists and its SHA-256 matches the sibling .sha256 asset.
    /// Returns true when the installer is ready to launch.
    /// </summary>
    public async Task<bool> DownloadInstallerAsync(
        string version,
        IProgress<double>? progress = null,
        CancellationToken ct = default
    )
    {
        Directory.CreateDirectory(path: CacheDir);

        string fileName = $"NoMercyMediaServer-{version}-windows-x64-setup.exe";
        string destPath = Path.Combine(path1: CacheDir, path2: fileName);
        string sha256Path = destPath + ".sha256";

        // Check for valid cached copy first
        if (File.Exists(path: destPath) && File.Exists(path: sha256Path))
        {
            LauncherLog.Info(message: $"Installer already cached at {destPath}, verifying SHA-256...");
            if (await VerifyInstallerAsync(version: version, ct: ct))
            {
                LauncherLog.Info(message: "Cached installer SHA-256 matches — skipping download");
                return true;
            }

            LauncherLog.Info(message: "SHA-256 mismatch on cached installer — re-downloading");
        }

        string tag = $"v{version}";
        string baseUrl =
            $"https://github.com/NoMercy-Entertainment/nomercy-media-server/releases/download/{tag}";
        string installerUrl = $"{baseUrl}/{fileName}";
        string sha256Url = $"{baseUrl}/{fileName}.sha256";

        // Download SHA-256 sidecar first (best-effort)
        try
        {
            using HttpResponseMessage sha256Response = await HttpClient.GetAsync(
                requestUri: sha256Url,
                completionOption: HttpCompletionOption.ResponseHeadersRead,
                cancellationToken: ct
            );

            if (sha256Response.IsSuccessStatusCode)
            {
                string sha256Content = await sha256Response.Content.ReadAsStringAsync(cancellationToken: ct);
                await File.WriteAllTextAsync(path: sha256Path, contents: sha256Content, cancellationToken: ct);
                LauncherLog.Info(message: "Downloaded SHA-256 sidecar");
            }
            else
            {
                LauncherLog.Info(
                    message: $"SHA-256 sidecar not found for {version} — will continue without verification"
                );
            }
        }
        catch (Exception ex)
        {
            LauncherLog.Info(message: $"Could not fetch SHA-256 sidecar: {ex.Message}");
        }

        // Download installer
        LauncherLog.Info(message: $"Downloading installer from {installerUrl}");

        using HttpResponseMessage response = await HttpClient.GetAsync(
            requestUri: installerUrl,
            completionOption: HttpCompletionOption.ResponseHeadersRead,
            cancellationToken: ct
        );

        response.EnsureSuccessStatusCode();

        long? total = response.Content.Headers.ContentLength;

        await using FileStream fileStream = new(
            path: destPath,
            mode: FileMode.Create,
            access: FileAccess.Write,
            share: FileShare.None,
            bufferSize: 81920,
            useAsync: true
        );

        await using Stream download = await response.Content.ReadAsStreamAsync(cancellationToken: ct);

        byte[] buffer = new byte[81920];
        long bytesRead = 0;
        int read;

        while ((read = await download.ReadAsync(buffer: buffer, cancellationToken: ct)) > 0)
        {
            await fileStream.WriteAsync(buffer: buffer.AsMemory(start: 0, length: read), cancellationToken: ct);
            bytesRead += read;

            if (progress is not null && total is > 0)
                progress.Report(value: (double)bytesRead / total.Value);
        }

        LauncherLog.Info(message: $"Installer downloaded to {destPath} ({bytesRead} bytes)");
        return true;
    }

    /// <summary>
    /// Verifies the cached installer against its .sha256 sidecar.
    /// Returns true when the file matches or when no sidecar exists (legacy release).
    /// Throws <see cref="InvalidDataException"/> on mismatch.
    /// </summary>
    public async Task<bool> VerifyInstallerAsync(string version, CancellationToken ct = default)
    {
        string fileName = $"NoMercyMediaServer-{version}-windows-x64-setup.exe";
        string destPath = Path.Combine(path1: CacheDir, path2: fileName);
        string sha256Path = destPath + ".sha256";

        if (!File.Exists(path: sha256Path))
        {
            LauncherLog.Info(message: $"No SHA-256 sidecar for {version} — skipping verification");
            return true;
        }

        string expectedLine = (await File.ReadAllTextAsync(path: sha256Path, cancellationToken: ct)).Trim();

        // Accept both bare-hash and "HASH  filename" formats
        string expected = expectedLine.Split(separator: ' ', count: 2)[0].ToUpperInvariant();

        using SHA256 sha256 = SHA256.Create();
        await using FileStream fs = File.OpenRead(path: destPath);
        byte[] hash = await sha256.ComputeHashAsync(inputStream: fs, cancellationToken: ct);
        string actual = Convert.ToHexString(inArray: hash).ToUpperInvariant();

        if (!string.Equals(a: expected, b: actual, comparisonType: StringComparison.Ordinal))
        {
            LauncherLog.Error(message: $"SHA-256 mismatch for {version}: expected {expected}, got {actual}");
            throw new InvalidDataException(
                message: $"Installer SHA-256 mismatch — file may be corrupted. Expected {expected[..8]}..., got {actual[..8]}..."
            );
        }

        LauncherLog.Info(message: $"SHA-256 verified for {version}");
        return true;
    }

    /// <summary>
    /// Graceful orderly shutdown: stop App via IPC, stop Server via IPC.
    /// The installer handles any remaining processes via CLOSEAPPLICATIONS.
    /// </summary>
    public async Task ShutdownAllAsync(
        ServerConnection connection,
        ServerProcessLauncher processLauncher,
        CancellationToken ct = default
    )
    {
        // Stop the App process first
        try
        {
            await connection.PostAsync(path: "/manage/app/stop", cancellationToken: ct);
            LauncherLog.Info(message: "App stop command sent");
        }
        catch (Exception ex)
        {
            LauncherLog.Info(message: $"App stop via IPC skipped: {ex.Message}");
        }

        await Task.Delay(millisecondsDelay: 500, cancellationToken: ct);

        // Stop the Server
        try
        {
            await connection.PostAsync(path: "/manage/stop", cancellationToken: ct);
            LauncherLog.Info(message: "Server stop command sent");
        }
        catch (Exception ex)
        {
            LauncherLog.Info(message: $"Server stop via IPC skipped: {ex.Message}");
        }

        LauncherLog.Info(message: "Waiting for server process to exit (30s timeout)");
        bool exited = await processLauncher.WaitForServerExitAsync(timeout: TimeSpan.FromSeconds(seconds: 30));

        if (!exited)
        {
            LauncherLog.Info(message: "Server did not exit gracefully within 30s — continuing anyway");
        }
    }

    /// <summary>
    /// Spawns the installer in silent mode with the appropriate flags.
    /// Passes /UpdateCacheCleanup=1 so the ISS [Code] section can delete the installer file post-install.
    /// Passes /LaunchAfterInstall=1 or 0 based on the user's auto-start preference.
    /// Then exits the launcher so the installer can apply CLOSEAPPLICATIONS cleanly.
    /// </summary>
    public Task LaunchInstallerAsync(string version, bool launcherAutoStart)
    {
        string fileName = $"NoMercyMediaServer-{version}-windows-x64-setup.exe";
        string installerPath = Path.Combine(path1: CacheDir, path2: fileName);

        if (!File.Exists(path: installerPath))
            throw new FileNotFoundException(message: "Installer not found in cache", fileName: installerPath);

        string launchAfter = launcherAutoStart ? "1" : "0";

        ProcessStartInfo startInfo = new(fileName: installerPath)
        {
            UseShellExecute = true, // needed for UAC elevation
            Arguments =
                $"/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /UpdateCacheCleanup=1 /LaunchAfterInstall={launchAfter}",
        };

        LauncherLog.Info(message: $"Launching installer: {installerPath} {startInfo.Arguments}");

        Process.Start(startInfo: startInfo);

        // Exit the launcher — the installer handles the rest via CLOSEAPPLICATIONS
        Environment.Exit(exitCode: 0);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Prunes stale installers from the cache.
    /// Keeps the installer for the current running version and the pending version; deletes all others.
    /// </summary>
    public Task CleanCacheAsync(
        string currentVersion,
        string? pendingVersion,
        CancellationToken ct = default
    )
    {
        if (!Directory.Exists(path: CacheDir))
            return Task.CompletedTask;

        HashSet<string> keep =
        [
            $"NoMercyMediaServer-{currentVersion}-windows-x64-setup.exe",
            $"NoMercyMediaServer-{currentVersion}-windows-x64-setup.exe.sha256",
        ];

        if (pendingVersion is not null)
        {
            keep.Add(item: $"NoMercyMediaServer-{pendingVersion}-windows-x64-setup.exe");
            keep.Add(item: $"NoMercyMediaServer-{pendingVersion}-windows-x64-setup.exe.sha256");
        }

        foreach (string file in Directory.EnumerateFiles(path: CacheDir, searchPattern: "NoMercyMediaServer-*-setup*"))
        {
            string name = Path.GetFileName(path: file);
            if (keep.Contains(item: name))
                continue;

            try
            {
                File.Delete(path: file);
                LauncherLog.Info(message: $"Pruned stale installer cache entry: {name}");
            }
            catch (Exception ex)
            {
                LauncherLog.Info(message: $"Could not prune {name}: {ex.Message}");
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Full update orchestration for installer deployments.
    /// </summary>
    public async Task<bool> DoUpdateAsync(
        string version,
        bool launcherAutoStart,
        ServerConnection connection,
        ServerProcessLauncher processLauncher,
        IProgress<double>? progress = null,
        CancellationToken ct = default
    )
    {
        // Background cache prune (fire-and-forget)
        _ = Task.Run(function: () => CleanCacheAsync(currentVersion: string.Empty, pendingVersion: version, ct: ct), cancellationToken: ct);

        bool downloaded = await DownloadInstallerAsync(version: version, progress: progress, ct: ct);
        if (!downloaded)
            return false;

        await VerifyInstallerAsync(version: version, ct: ct);

        await ShutdownAllAsync(connection: connection, processLauncher: processLauncher, ct: ct);

        await LaunchInstallerAsync(version: version, launcherAutoStart: launcherAutoStart);

        return true;
    }
}
