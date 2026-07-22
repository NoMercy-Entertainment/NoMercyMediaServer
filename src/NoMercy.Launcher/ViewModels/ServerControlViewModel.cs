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

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using NoMercy.Launcher.Models;
using NoMercy.Launcher.Services;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Launcher.ViewModels;

public class ServerControlViewModel : INotifyPropertyChanged
{
    private readonly ServerConnection _serverConnection;
    private readonly ServerProcessLauncher _processLauncher;
    private CancellationTokenSource? _pollCts;

    public string ServerStatus
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "Disconnected";

    public string ServerName
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "--";

    public string Version
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "--";

    public string Platform
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "--";

    public string Uptime
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "--";

    public bool IsServerRunning
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public bool IsServerStopped
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = true;

    public bool IsActionInProgress
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public string ActionStatus
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = string.Empty;

    public string StatusColor
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "#EF4444";

    public bool AutoStartEnabled
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public bool UpdateAvailable
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public bool RestartNeeded
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public string LatestVersion
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = string.Empty;

    private readonly InstallerUpdater _installerUpdater;

    public ServerControlViewModel(
        ServerConnection serverConnection,
        ServerProcessLauncher processLauncher
    )
    {
        _serverConnection = serverConnection;
        _processLauncher = processLauncher;
        _installerUpdater = new(serverConnection: serverConnection);
    }

    public async Task RefreshStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!_serverConnection.IsConnected)
            await _serverConnection.ConnectAsync(cancellationToken: cancellationToken);

        ServerStatusResponse? status = await _serverConnection.GetAsync<ServerStatusResponse>(
            path: "/manage/status",
            cancellationToken: cancellationToken
        );

        if (status is null)
        {
            ServerStatus = "Disconnected";
            ServerName = "--";
            Version = "--";
            Platform = "--";
            Uptime = "--";
            IsServerRunning = false;
            IsServerStopped = !IsActionInProgress;
            StatusColor = "#EF4444";
            return;
        }

        ServerStatus = status.Status switch
        {
            "running" => "Running",
            "starting" => "Starting",
            _ => status.Status,
        };

        ServerName = string.IsNullOrEmpty(value: status.ServerName) ? "--" : status.ServerName;

        Version = string.IsNullOrEmpty(value: status.Version) ? "--" : status.Version;

        Platform = string.IsNullOrEmpty(value: status.Platform)
            ? "--"
            : $"{status.Platform} ({status.Architecture})";

        Uptime = TrayIconManager.FormatUptime(totalSeconds: status.UptimeSeconds);

        IsServerRunning = status.Status == "running";
        IsServerStopped = false;

        StatusColor = status.Status switch
        {
            "running" => "#22C55E",
            "starting" => "#EAB308",
            _ => "#EF4444",
        };

        AutoStartEnabled = status.AutoStart;
        UpdateAvailable = status.UpdateAvailable;
        RestartNeeded = status.RestartNeeded;
        LatestVersion = status.LatestVersion.OrEmpty();
    }

    public async Task StopServerAsync()
    {
        if (IsActionInProgress)
            return;

        IsActionInProgress = true;
        ActionStatus = "Stopping server...";

        try
        {
            bool success = await _serverConnection.PostAsync(path: "/manage/stop");

            ActionStatus = success ? "Stop command sent" : "Failed to send stop command";

            await Task.Delay(millisecondsDelay: 1000);
            await RefreshStatusAsync();
        }
        finally
        {
            IsActionInProgress = false;
        }
    }

    public async Task RestartServerAsync()
    {
        if (IsActionInProgress)
            return;

        IsActionInProgress = true;
        ActionStatus = "Stopping server...";

        try
        {
            bool stopSent = await _serverConnection.PostAsync(path: "/manage/stop");
            if (!stopSent)
            {
                ActionStatus = "Failed to send stop command";
                return;
            }

            ActionStatus = "Waiting for server to exit...";
            bool exited = await _processLauncher.WaitForServerExitAsync(timeout: TimeSpan.FromSeconds(seconds: 30));
            if (!exited)
            {
                ActionStatus = "Server did not stop gracefully — force killing...";
                await _processLauncher.ForceKillServerAsync();
                await Task.Delay(millisecondsDelay: 1000);
            }

            _serverConnection.IsConnected = false;

            ActionStatus = "Starting server...";
            string extraArgs = LauncherSettings.Load().StartupArguments;
            bool started = await _processLauncher.StartServerAsync(extraArguments: extraArgs);
            if (!started)
            {
                ActionStatus = "Failed to start server";
                return;
            }

            ActionStatus = "Waiting for server to come back up...";
            await WaitForServerReadyAsync(timeout: TimeSpan.FromSeconds(seconds: 30));

            ActionStatus = "Server restarted";
            await RefreshStatusAsync();
        }
        finally
        {
            IsActionInProgress = false;
        }
    }

    public async Task ToggleAutoStartAsync(bool enabled)
    {
        await _serverConnection.PostAsync(path: "/manage/autostart", body: new { enabled });

        await RefreshStatusAsync();
    }

    /// <summary>
    /// Called by the View to show the "active sessions" dialog.
    /// Return true → user chose "Interrupt and update now".
    /// Return false → user chose "Wait — I'll update later".
    /// </summary>
    public Func<ActivityInfo, Task<bool>>? ShowActiveSessionDialog { get; set; }

    public async Task ApplyUpdateAsync()
    {
        if (IsActionInProgress)
            return;

        IsActionInProgress = true;
        ActionStatus = "Checking for update...";
        LauncherLog.Info(message: "Update started: requesting server to download update");

        try
        {
            (bool downloaded, string? downloadBody) = await _serverConnection.PostWithBodyAsync(
                path: "/manage/update"
            );

            LauncherLog.Info(message: $"POST /manage/update => success={downloaded}, body={downloadBody}");

            if (!downloaded)
            {
                string reason = ExtractMessage(json: downloadBody) ?? "Server returned an error";
                LauncherLog.Error(message: $"Download step failed: {reason}");
                ActionStatus = $"Failed to download update: {reason}";
                return;
            }

            UpdateCheckResult? result = null;
            if (!string.IsNullOrEmpty(value: downloadBody))
            {
                try
                {
                    result = JsonConvert.DeserializeObject<UpdateCheckResult>(value: downloadBody);
                }
                catch
                {
                    // Ignore parse errors — old server, fall through to binary-swap
                }
            }

            bool useInstaller =
                result?.UseInstaller == true
                && RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows)
                && await _installerUpdater.IsInstallerDeploymentAsync();

            // Check for active streams/encodes before proceeding with either path
            ActivityInfo? activity = null;
            try
            {
                activity = await _installerUpdater.GetActivityAsync();
            }
            catch
            {
                // Server may not support /manage/activity (older build) — continue
            }

            if (activity is not null && (activity.ActiveStreams > 0 || activity.ActiveEncodes > 0))
            {
                bool proceed = ShowActiveSessionDialog is not null
                    ? await ShowActiveSessionDialog(arg: activity)
                    : false; // no dialog registered → default to Wait

                if (!proceed)
                {
                    LauncherLog.Info(message: "User chose to wait — aborting update due to active sessions");
                    ActionStatus = "Update deferred — active sessions in progress";
                    return;
                }

                LauncherLog.Info(message: "User chose to interrupt — proceeding with update");
            }

            if (useInstaller)
            {
                await ApplyInstallerUpdateAsync(version: result!.LatestVersion ?? LatestVersion);
            }
            else
            {
                await ApplyBinarySwapUpdateAsync();
            }
        }
        catch (FileNotFoundException ex)
        {
            LauncherLog.Error(message: "No staged update file found", ex: ex);
            ActionStatus = "No staged update file found";
        }
        catch (InvalidDataException ex)
        {
            LauncherLog.Error(message: "Installer integrity check failed", ex: ex);
            ActionStatus = $"Update aborted: {ex.Message}";
        }
        catch (Exception ex)
        {
            LauncherLog.Error(message: "Update failed", ex: ex);
            ActionStatus = $"Update failed: {ex.Message}";
        }
        finally
        {
            IsActionInProgress = false;
        }
    }

    // Installer path (Windows installer deployment only)
    private async Task ApplyInstallerUpdateAsync(string version)
    {
        LauncherLog.Info(message: $"Using installer update path for version {version}");

        ActionStatus = "Downloading installer...";

        Progress<double> progress = new(handler: pct =>
        {
            ActionStatus = $"Downloading installer... {pct:P0}";
        });

        TraySettings settings = LauncherSettings.Load();
        bool autoStart = settings.AutoStart;

        await _installerUpdater.DoUpdateAsync(
            version: version,
            launcherAutoStart: autoStart,
            connection: _serverConnection,
            processLauncher: _processLauncher,
            progress: progress
        );

        // DoUpdateAsync calls Environment.Exit(0) after spawning the installer,
        // so execution never reaches here in normal flow.
    }

    // Binary-swap path (Linux, macOS, standalone Windows)
    private async Task ApplyBinarySwapUpdateAsync()
    {
        ActionStatus = "Stopping server...";
        LauncherLog.Info(message: "Sending stop command");
        bool stopSent = await _serverConnection.PostAsync(path: "/manage/stop");
        if (!stopSent)
        {
            LauncherLog.Error(message: "Failed to send stop command via IPC");
            ActionStatus = "Failed to send stop command";
            return;
        }

        ActionStatus = "Waiting for server to exit...";
        LauncherLog.Info(message: "Waiting for server process to exit (30s timeout)");
        bool exited = await _processLauncher.WaitForServerExitAsync(timeout: TimeSpan.FromSeconds(seconds: 30));
        if (!exited)
        {
            LauncherLog.Error(message: "Server did not exit within 30 seconds — force killing");
            ActionStatus = "Server did not stop gracefully — force killing...";
            await _processLauncher.ForceKillServerAsync();
            await Task.Delay(millisecondsDelay: 1000);
        }

        LauncherLog.Info(message: "Server process exited");
        _serverConnection.IsConnected = false;

        ActionStatus = "Applying update...";
        LauncherLog.Info(message: "Applying staged update binary");
        await _processLauncher.ApplyUpdateIfStagedAsync();
        LauncherLog.Info(message: "Binary replacement complete");

        ActionStatus = "Starting updated server...";
        string updateExtraArgs = LauncherSettings.Load().StartupArguments;
        LauncherLog.Info(message: $"Starting server with args: {updateExtraArgs}");
        bool started = await _processLauncher.StartServerAsync(extraArguments: updateExtraArgs);
        if (!started)
        {
            LauncherLog.Error(message: "Failed to start server process after update");
            ActionStatus = "Failed to start server";
            return;
        }

        ActionStatus = "Waiting for server to come back up...";
        LauncherLog.Info(message: "Waiting for server to become ready (30s timeout)");
        await WaitForServerReadyAsync(timeout: TimeSpan.FromSeconds(seconds: 30));

        LauncherLog.Info(message: "Update complete");
        ActionStatus = "Update complete";
        await RefreshStatusAsync();
    }

    private static string? ExtractMessage(string? json)
    {
        if (string.IsNullOrEmpty(value: json))
            return null;

        try
        {
            dynamic? obj = JsonConvert.DeserializeObject(value: json);
            return obj?.message?.ToString();
        }
        catch
        {
            return null;
        }
    }

    public async Task LaunchAppAsync()
    {
        if (IsActionInProgress)
            return;

        IsActionInProgress = true;
        ActionStatus = "Launching app...";

        try
        {
            bool launched;

            if (_serverConnection.IsConnected)
                launched = await _serverConnection.PostAsync(path: "/manage/app/start");
            else
                launched = await _processLauncher.LaunchAppAsync();

            ActionStatus = launched ? "App launched" : "Failed to launch app";
        }
        finally
        {
            IsActionInProgress = false;
        }
    }

    public async Task StartServerAsync()
    {
        if (IsActionInProgress)
            return;

        IsActionInProgress = true;
        IsServerStopped = false;
        ActionStatus = "Starting server...";

        try
        {
            string extraArgs = LauncherSettings.Load().StartupArguments;
            bool started = await _processLauncher.StartServerAsync(extraArguments: extraArgs);

            ActionStatus = started ? "Server process launched" : "Failed to start server";

            await Task.Delay(millisecondsDelay: 2000);
            await RefreshStatusAsync();
        }
        finally
        {
            IsActionInProgress = false;
        }
    }

    public void StartPolling()
    {
        StopPolling();

        _pollCts = new();
        CancellationToken token = _pollCts.Token;

        _ = Task.Run(
            function: async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    await RefreshStatusAsync(cancellationToken: token);
                    await Task.Delay(delay: TimeSpan.FromSeconds(seconds: 5), cancellationToken: token);
                }
            },
            cancellationToken: token
        );
    }

    public void StopPolling()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
    }

    private async Task WaitForServerReadyAsync(TimeSpan timeout)
    {
        using CancellationTokenSource cts = new(delay: timeout);

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                await _serverConnection.ConnectAsync(cancellationToken: cts.Token);
                if (_serverConnection.IsConnected)
                    return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // Server not ready yet
            }

            try
            {
                await Task.Delay(millisecondsDelay: 1000, cancellationToken: cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    internal static string FormatStatusDisplay(string status)
    {
        return status switch
        {
            "running" => "Running",
            "starting" => "Starting",
            "Disconnected" => "Disconnected",
            _ => status,
        };
    }

    internal static string GetStatusColor(string status)
    {
        return status switch
        {
            "running" or "Running" => "#22C55E",
            "starting" or "Starting" => "#EAB308",
            _ => "#EF4444",
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(sender: this, e: new(propertyName: propertyName));
    }
}
