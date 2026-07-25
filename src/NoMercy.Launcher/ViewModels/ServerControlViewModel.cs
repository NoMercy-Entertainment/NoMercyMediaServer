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
        _installerUpdater = new(serverConnection);
    }

    public async Task RefreshStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!_serverConnection.IsConnected)
            await _serverConnection.ConnectAsync(cancellationToken);

        ServerStatusResponse? status = await _serverConnection.GetAsync<ServerStatusResponse>(
            "/manage/status",
            cancellationToken
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

        ServerName = string.IsNullOrEmpty(status.ServerName) ? "--" : status.ServerName;

        Version = string.IsNullOrEmpty(status.Version) ? "--" : status.Version;

        Platform = string.IsNullOrEmpty(status.Platform)
            ? "--"
            : $"{status.Platform} ({status.Architecture})";

        Uptime = TrayIconManager.FormatUptime(status.UptimeSeconds);

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
            bool success = await _serverConnection.PostAsync("/manage/stop");

            ActionStatus = success ? "Stop command sent" : "Failed to send stop command";

            await Task.Delay(1000);
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
            bool stopSent = await _serverConnection.PostAsync("/manage/stop");
            if (!stopSent)
            {
                ActionStatus = "Failed to send stop command";
                return;
            }

            ActionStatus = "Waiting for server to exit...";
            bool exited = await _processLauncher.WaitForServerExitAsync(TimeSpan.FromSeconds(30));
            if (!exited)
            {
                ActionStatus = "Server did not stop gracefully — force killing...";
                await _processLauncher.ForceKillServerAsync();
                await Task.Delay(1000);
            }

            _serverConnection.IsConnected = false;

            ActionStatus = "Starting server...";
            string extraArgs = LauncherSettings.Load().StartupArguments;
            bool started = await _processLauncher.StartServerAsync(extraArgs);
            if (!started)
            {
                ActionStatus = "Failed to start server";
                return;
            }

            ActionStatus = "Waiting for server to come back up...";
            await WaitForServerReadyAsync(TimeSpan.FromSeconds(30));

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
        await _serverConnection.PostAsync("/manage/autostart", new { enabled });

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
        LauncherLog.Info("Update started: requesting server to download update");

        try
        {
            (bool downloaded, string? downloadBody) = await _serverConnection.PostWithBodyAsync(
                "/manage/update"
            );

            LauncherLog.Info($"POST /manage/update => success={downloaded}, body={downloadBody}");

            if (!downloaded)
            {
                string reason = ExtractMessage(downloadBody) ?? "Server returned an error";
                LauncherLog.Error($"Download step failed: {reason}");
                ActionStatus = $"Failed to download update: {reason}";
                return;
            }

            UpdateCheckResult? result = null;
            if (!string.IsNullOrEmpty(downloadBody))
            {
                try
                {
                    result = JsonConvert.DeserializeObject<UpdateCheckResult>(downloadBody);
                }
                catch
                {
                    // Ignore parse errors — old server, fall through to binary-swap
                }
            }

            bool useInstaller =
                result?.UseInstaller == true
                && RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
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
                    ? await ShowActiveSessionDialog(activity)
                    : false; // no dialog registered → default to Wait

                if (!proceed)
                {
                    LauncherLog.Info("User chose to wait — aborting update due to active sessions");
                    ActionStatus = "Update deferred — active sessions in progress";
                    return;
                }

                LauncherLog.Info("User chose to interrupt — proceeding with update");
            }

            if (useInstaller)
            {
                await ApplyInstallerUpdateAsync(result!.LatestVersion ?? LatestVersion);
            }
            else
            {
                await ApplyBinarySwapUpdateAsync();
            }
        }
        catch (FileNotFoundException ex)
        {
            LauncherLog.Error("No staged update file found", ex);
            ActionStatus = "No staged update file found";
        }
        catch (InvalidDataException ex)
        {
            LauncherLog.Error("Installer integrity check failed", ex);
            ActionStatus = $"Update aborted: {ex.Message}";
        }
        catch (Exception ex)
        {
            LauncherLog.Error("Update failed", ex);
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
        LauncherLog.Info($"Using installer update path for version {version}");

        ActionStatus = "Downloading installer...";

        Progress<double> progress = new(pct =>
        {
            ActionStatus = $"Downloading installer... {pct:P0}";
        });

        TraySettings settings = LauncherSettings.Load();
        bool autoStart = settings.AutoStart;

        await _installerUpdater.DoUpdateAsync(
            version,
            autoStart,
            _serverConnection,
            _processLauncher,
            progress
        );

        // DoUpdateAsync calls Environment.Exit(0) after spawning the installer,
        // so execution never reaches here in normal flow.
    }

    // Binary-swap path (Linux, macOS, standalone Windows)
    private async Task ApplyBinarySwapUpdateAsync()
    {
        ActionStatus = "Stopping server...";
        LauncherLog.Info("Sending stop command");
        bool stopSent = await _serverConnection.PostAsync("/manage/stop");
        if (!stopSent)
        {
            LauncherLog.Error("Failed to send stop command via IPC");
            ActionStatus = "Failed to send stop command";
            return;
        }

        ActionStatus = "Waiting for server to exit...";
        LauncherLog.Info("Waiting for server process to exit (30s timeout)");
        bool exited = await _processLauncher.WaitForServerExitAsync(TimeSpan.FromSeconds(30));
        if (!exited)
        {
            LauncherLog.Error("Server did not exit within 30 seconds — force killing");
            ActionStatus = "Server did not stop gracefully — force killing...";
            await _processLauncher.ForceKillServerAsync();
            await Task.Delay(1000);
        }

        LauncherLog.Info("Server process exited");
        _serverConnection.IsConnected = false;

        ActionStatus = "Applying update...";
        LauncherLog.Info("Applying staged update binary");
        await _processLauncher.ApplyUpdateIfStagedAsync();
        LauncherLog.Info("Binary replacement complete");

        ActionStatus = "Starting updated server...";
        string updateExtraArgs = LauncherSettings.Load().StartupArguments;
        LauncherLog.Info($"Starting server with args: {updateExtraArgs}");
        bool started = await _processLauncher.StartServerAsync(updateExtraArgs);
        if (!started)
        {
            LauncherLog.Error("Failed to start server process after update");
            ActionStatus = "Failed to start server";
            return;
        }

        ActionStatus = "Waiting for server to come back up...";
        LauncherLog.Info("Waiting for server to become ready (30s timeout)");
        await WaitForServerReadyAsync(TimeSpan.FromSeconds(30));

        LauncherLog.Info("Update complete");
        ActionStatus = "Update complete";
        await RefreshStatusAsync();
    }

    private static string? ExtractMessage(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            dynamic? obj = JsonConvert.DeserializeObject(json);
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
                launched = await _serverConnection.PostAsync("/manage/app/start");
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
            bool started = await _processLauncher.StartServerAsync(extraArgs);

            ActionStatus = started ? "Server process launched" : "Failed to start server";

            await Task.Delay(2000);
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
            async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    await RefreshStatusAsync(token);
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                }
            },
            token
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
        using CancellationTokenSource cts = new(timeout);

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                await _serverConnection.ConnectAsync(cts.Token);
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
                await Task.Delay(1000, cts.Token);
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
        PropertyChanged?.Invoke(this, new(propertyName));
    }
}
