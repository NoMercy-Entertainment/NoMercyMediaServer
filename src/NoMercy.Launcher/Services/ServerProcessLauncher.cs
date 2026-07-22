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
using NoMercy.NmSystem.FileSystem;
using NoMercy.NmSystem.Information;

namespace NoMercy.Launcher.Services;

public class ServerProcessLauncher
{
    private Process? _serverProcess;
    private Process? _appProcess;

    public bool IsServerProcessRunning => _serverProcess is { HasExited: false };

    public bool IsAppProcessRunning => _appProcess is { HasExited: false };

    public Process? ServerProcess => _serverProcess;

    public Task<bool> StartServerAsync(string? extraArguments = null)
    {
        if (IsServerProcessRunning)
            return Task.FromResult(result: false);

        // Wipe stale log files so the new run's logs aren't buried under
        // the previous execution's. Best-effort: any file held by another
        // process (a stray Rider-launched server, the CLI's logs SSE
        // stream) is skipped, the start still proceeds.
        ClearLogsDirectory();

        // Prefer the binary next to the Launcher (installer deployment),
        // then fall back to the binaries path (standalone deployment)
        ProcessStartInfo? startInfo =
            CreateInstalledStartInfo()
            ?? CreateProductionStartInfo()
            ?? CreateDevBinaryStartInfo()
            ?? CreateDotnetRunStartInfo();

        if (startInfo is null)
            return Task.FromResult(result: false);

        // Append user-configured startup arguments
        if (!string.IsNullOrWhiteSpace(value: extraArguments))
        {
            foreach (string arg in ParseArguments(input: extraArguments))
                startInfo.ArgumentList.Add(item: arg);
        }

        // Tell the server it's running from an installed deployment so it
        // skips binary downloads (the installer handles updates)
        string? installDir = GetInstallDirectory();
        if (installDir is not null)
            startInfo.Environment[key: "NOMERCY_INSTALL_DIR"] = installDir;

        _serverProcess = new() { StartInfo = startInfo, EnableRaisingEvents = true };

        _serverProcess.Exited += (_, _) =>
        {
            _serverProcess = null;
        };

        bool started = _serverProcess.Start();
        return Task.FromResult(result: started);
    }

    public Task<bool> LaunchAppAsync(string? route = null)
    {
        if (IsAppProcessRunning)
            return Task.FromResult(result: false);

        ProcessStartInfo? startInfo =
            CreateAppInstalledStartInfo()
            ?? CreateAppProductionStartInfo()
            ?? CreateAppDevBinaryStartInfo()
            ?? CreateAppDotnetRunStartInfo();

        if (startInfo is null)
            return Task.FromResult(result: false);

        if (!string.IsNullOrEmpty(value: route))
        {
            startInfo.ArgumentList.Add(item: "--route");
            startInfo.ArgumentList.Add(item: route);
        }

        _appProcess = new() { StartInfo = startInfo, EnableRaisingEvents = true };

        _appProcess.Exited += (_, _) =>
        {
            _appProcess = null;
        };

        bool started = _appProcess.Start();

        if (!started)
        {
            _appProcess = null;
            return Task.FromResult(result: false);
        }

        return Task.FromResult(result: true);
    }

    public async Task<bool> WaitForServerExitAsync(TimeSpan timeout)
    {
        using CancellationTokenSource cts = new(delay: timeout);

        if (_serverProcess is not null)
        {
            try
            {
                await _serverProcess.WaitForExitAsync(cancellationToken: cts.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        // Server wasn't started by the Tray — poll until it's gone
        while (!cts.Token.IsCancellationRequested)
        {
            if (!IsServerProcessRunning)
                return true;

            try
            {
                await Task.Delay(millisecondsDelay: 500, cancellationToken: cts.Token);
            }
            catch (OperationCanceledException)
            {
                return !IsServerProcessRunning;
            }
        }

        return !IsServerProcessRunning;
    }

    public Task ForceKillServerAsync()
    {
        try
        {
            if (_serverProcess is { HasExited: false })
            {
                _serverProcess.Kill(entireProcessTree: true);
                _serverProcess = null;
            }
        }
        catch (Exception ex)
        {
            LauncherLog.Error(message: "Force kill failed", ex: ex);
        }

        return Task.CompletedTask;
    }

    public async Task ApplyUpdateAsync()
    {
        string tempPath = AppFiles.ServerTempExePath;
        string currentPath = AppFiles.ServerExePath;
        string backupPath = currentPath + ".bak";

        LauncherLog.Info(message: $"ApplyUpdate: temp={tempPath}, current={currentPath}");

        if (!File.Exists(path: tempPath))
        {
            LauncherLog.Error(message: $"Staged update not found at {tempPath}");
            throw new FileNotFoundException(message: "No staged update found.", fileName: tempPath);
        }

        // Backup current binary before replacing
        if (File.Exists(path: currentPath))
        {
            LauncherLog.Info(message: $"Backing up current binary to {backupPath}");
            if (File.Exists(path: backupPath))
                File.Delete(path: backupPath);
            File.Move(sourceFileName: currentPath, destFileName: backupPath);
        }

        try
        {
            File.Move(sourceFileName: tempPath, destFileName: currentPath);
            await FilePermissions.SetExecutionPermissions(path: currentPath);
            LauncherLog.Info(message: "Binary replacement successful");

            // Clean up backup on success
            if (File.Exists(path: backupPath))
                File.Delete(path: backupPath);
        }
        catch (Exception ex)
        {
            LauncherLog.Error(message: "Binary replacement failed, attempting rollback", ex: ex);

            // Rollback: restore backup if move failed
            if (File.Exists(path: backupPath) && !File.Exists(path: currentPath))
            {
                File.Move(sourceFileName: backupPath, destFileName: currentPath);
                LauncherLog.Info(message: "Rollback successful");
            }

            throw;
        }
    }

    public async Task ApplyUpdateIfStagedAsync()
    {
        string tempPath = AppFiles.ServerTempExePath;

        LauncherLog.Info(
            message: $"Checking for staged update at {tempPath}: exists={File.Exists(path: tempPath)}"
        );

        if (File.Exists(path: tempPath))
            await ApplyUpdateAsync();
    }

    /// <summary>
    /// Deletes every <c>log*.txt</c> in <see cref="AppFiles.LogPath"/> so
    /// the next server run starts with a clean slate. Files held by another
    /// process are skipped — the start path stays unblocked.
    /// </summary>
    private static void ClearLogsDirectory()
    {
        try
        {
            string logDir = AppFiles.LogPath;
            if (!Directory.Exists(path: logDir))
                return;

            foreach (string file in Directory.EnumerateFiles(path: logDir, searchPattern: "log*.txt"))
            {
                try
                {
                    File.Delete(path: file);
                }
                catch (IOException)
                {
                    // Locked by a still-running server / CLI — leave it.
                }
                catch (UnauthorizedAccessException)
                {
                    // Permission denied — skip rather than crash the launch.
                }
            }
        }
        catch (Exception ex)
        {
            LauncherLog.Error(message: "Failed to clear log directory before server start", ex: ex);
        }
    }

    /// <summary>
    /// Returns the Launcher's directory only if it's an installer deployment
    /// (i.e., running from a different directory than the binaries path).
    /// Returns null for standalone deployments where everything is in the binaries path.
    /// </summary>
    private static string? GetInstallDirectory()
    {
        string? ownDir = Path.GetDirectoryName(
            path: Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location
        );

        if (ownDir is null)
            return null;

        // If the Launcher is in the binaries path, this is a standalone deployment
        if (
            string.Equals(
                a: Path.GetFullPath(path: ownDir),
                b: Path.GetFullPath(path: AppFiles.BinariesPath),
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
            return null;

        return ownDir;
    }

    private static ProcessStartInfo? CreateProductionStartInfo()
    {
        string exePath = AppFiles.ServerExePath;

        if (!File.Exists(path: exePath))
            return null;

        return new(fileName: exePath) { UseShellExecute = false, CreateNoWindow = true };
    }

    private static ProcessStartInfo? CreateInstalledStartInfo()
    {
        string? ownDir = Path.GetDirectoryName(
            path: Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location
        );

        if (ownDir is null)
            return null;

        string candidate = Path.Combine(path1: ownDir, path2: "NoMercyMediaServer" + Info.ExecSuffix);

        if (!File.Exists(path: candidate))
            return null;

        return new(fileName: candidate) { UseShellExecute = false, CreateNoWindow = true };
    }

    private static ProcessStartInfo? CreateDevBinaryStartInfo()
    {
        string? serverBinary = FindServerBinary();

        if (serverBinary is null)
            return null;

        ProcessStartInfo startInfo = new(fileName: serverBinary)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add(item: "--dev");

        return startInfo;
    }

    private static ProcessStartInfo? CreateDotnetRunStartInfo()
    {
        string? serverProjectDir = FindServerProjectDirectory();

        if (serverProjectDir is null)
            return null;

        ProcessStartInfo startInfo = new(fileName: "dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add(item: "run");
        startInfo.ArgumentList.Add(item: "--project");
        startInfo.ArgumentList.Add(item: serverProjectDir);
        startInfo.ArgumentList.Add(item: "--");
        startInfo.ArgumentList.Add(item: "--dev");

        return startInfo;
    }

    private static string? FindServerBinary()
    {
        string? serverProjectDir = FindServerProjectDirectory();

        if (serverProjectDir is null)
            return null;

        string execName = "NoMercyMediaServer" + Info.ExecSuffix;

        string[] searchPaths =
        [
            Path.Combine(paths: [serverProjectDir, "bin", "Debug", $"net{Environment.Version.Major}.{Environment.Version.Minor}", execName]
            ),
            Path.Combine(paths: [serverProjectDir, "bin", "Release", $"net{Environment.Version.Major}.{Environment.Version.Minor}", execName]
            ),
        ];

        foreach (string path in searchPaths)
        {
            if (File.Exists(path: path))
                return path;
        }

        return null;
    }

    private static ProcessStartInfo? CreateAppProductionStartInfo()
    {
        string exePath = AppFiles.AppExePath;

        if (!File.Exists(path: exePath))
            return null;

        return new(fileName: exePath) { UseShellExecute = false, CreateNoWindow = true };
    }

    private static ProcessStartInfo? CreateAppInstalledStartInfo()
    {
        string? ownDir = Path.GetDirectoryName(
            path: Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location
        );

        if (ownDir is null)
            return null;

        string candidate = Path.Combine(path1: ownDir, path2: "NoMercyApp" + Info.ExecSuffix);

        if (!File.Exists(path: candidate))
            return null;

        return new(fileName: candidate) { UseShellExecute = false, CreateNoWindow = true };
    }

    private static ProcessStartInfo? CreateAppDevBinaryStartInfo()
    {
        string? appProjectDir = FindProjectDirectory(projectName: "NoMercy.App");

        if (appProjectDir is null)
            return null;

        string execName = "NoMercyApp" + Info.ExecSuffix;

        string[] searchPaths =
        [
            Path.Combine(paths: [appProjectDir, "bin", "Debug", $"net{Environment.Version.Major}.{Environment.Version.Minor}", execName]
            ),
            Path.Combine(paths: [appProjectDir, "bin", "Release", $"net{Environment.Version.Major}.{Environment.Version.Minor}", execName]
            ),
        ];

        foreach (string path in searchPaths)
        {
            if (File.Exists(path: path))
                return new(fileName: path) { UseShellExecute = false, CreateNoWindow = true };
        }

        return null;
    }

    private static ProcessStartInfo? CreateAppDotnetRunStartInfo()
    {
        string? appProjectDir = FindProjectDirectory(projectName: "NoMercy.App");

        if (appProjectDir is null)
            return null;

        ProcessStartInfo startInfo = new(fileName: "dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add(item: "run");
        startInfo.ArgumentList.Add(item: "--project");
        startInfo.ArgumentList.Add(item: appProjectDir);

        return startInfo;
    }

    private static string? FindServerProjectDirectory()
    {
        return FindProjectDirectory(projectName: "NoMercy.Service");
    }

    private static string? FindProjectDirectory(string projectName)
    {
        string? assemblyLocation = Path.GetDirectoryName(path: Assembly.GetExecutingAssembly().Location);

        string? directory = assemblyLocation;

        while (directory is not null)
        {
            string candidate = Path.Combine(path1: directory, path2: "src", path3: projectName);

            if (Directory.Exists(path: candidate))
                return candidate;

            directory = Path.GetDirectoryName(path: directory);
        }

        return null;
    }

    /// <summary>
    /// Splits a command-line string into individual arguments,
    /// respecting double-quoted segments.
    /// </summary>
    internal static List<string> ParseArguments(string input)
    {
        List<string> args = [];
        int i = 0;

        while (i < input.Length)
        {
            // Skip whitespace
            while (i < input.Length && char.IsWhiteSpace(c: input[index: i]))
                i++;

            if (i >= input.Length)
                break;

            if (input[index: i] == '"')
            {
                // Quoted argument
                i++;
                int start = i;
                while (i < input.Length && input[index: i] != '"')
                    i++;

                args.Add(item: input[start..i]);

                if (i < input.Length)
                    i++; // skip closing quote
            }
            else
            {
                // Unquoted argument
                int start = i;
                while (i < input.Length && !char.IsWhiteSpace(c: input[index: i]))
                    i++;

                args.Add(item: input[start..i]);
            }
        }

        return args;
    }
}
