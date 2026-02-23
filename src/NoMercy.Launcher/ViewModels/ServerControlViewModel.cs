using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using NoMercy.Launcher.Models;
using NoMercy.Launcher.Services;

namespace NoMercy.Launcher.ViewModels;

public class ServerControlViewModel : INotifyPropertyChanged
{
    private readonly ServerConnection _serverConnection;
    private readonly ServerProcessLauncher _processLauncher;
    private CancellationTokenSource? _pollCts;

    private string _serverStatus = "Disconnected";
    private string _serverName = "--";
    private string _version = "--";
    private string _platform = "--";
    private string _uptime = "--";
    private bool _isServerRunning;
    private bool _isServerStopped = true;
    private bool _isActionInProgress;
    private string _actionStatus = string.Empty;
    private string _statusColor = "#EF4444";
    private bool _autoStartEnabled;
    private bool _updateAvailable;
    private bool _restartNeeded;
    private string _latestVersion = string.Empty;

    private bool _configLoaded;
    private string _configServerName = string.Empty;
    private int _internalPort;
    private int _externalPort;
    private int _libraryWorkers;
    private int _importWorkers;
    private int _extrasWorkers;
    private int _encoderWorkers;
    private int _cronWorkers;
    private int _imageWorkers;
    private int _fileWorkers;
    private int _musicWorkers;

    public string ServerStatus
    {
        get => _serverStatus;
        set { _serverStatus = value; OnPropertyChanged(); }
    }

    public string ServerName
    {
        get => _serverName;
        set { _serverName = value; OnPropertyChanged(); }
    }

    public string Version
    {
        get => _version;
        set { _version = value; OnPropertyChanged(); }
    }

    public string Platform
    {
        get => _platform;
        set { _platform = value; OnPropertyChanged(); }
    }

    public string Uptime
    {
        get => _uptime;
        set { _uptime = value; OnPropertyChanged(); }
    }

    public bool IsServerRunning
    {
        get => _isServerRunning;
        set { _isServerRunning = value; OnPropertyChanged(); }
    }

    public bool IsServerStopped
    {
        get => _isServerStopped;
        set { _isServerStopped = value; OnPropertyChanged(); }
    }

    public bool IsActionInProgress
    {
        get => _isActionInProgress;
        set { _isActionInProgress = value; OnPropertyChanged(); }
    }

    public string ActionStatus
    {
        get => _actionStatus;
        set { _actionStatus = value; OnPropertyChanged(); }
    }

    public string StatusColor
    {
        get => _statusColor;
        set { _statusColor = value; OnPropertyChanged(); }
    }

    public bool AutoStartEnabled
    {
        get => _autoStartEnabled;
        set { _autoStartEnabled = value; OnPropertyChanged(); }
    }

    public bool UpdateAvailable
    {
        get => _updateAvailable;
        set { _updateAvailable = value; OnPropertyChanged(); }
    }

    public bool RestartNeeded
    {
        get => _restartNeeded;
        set { _restartNeeded = value; OnPropertyChanged(); }
    }

    public string LatestVersion
    {
        get => _latestVersion;
        set { _latestVersion = value; OnPropertyChanged(); }
    }

    public bool ConfigLoaded
    {
        get => _configLoaded;
        set { _configLoaded = value; OnPropertyChanged(); }
    }

    public string ConfigServerName
    {
        get => _configServerName;
        set { _configServerName = value; OnPropertyChanged(); }
    }

    public int InternalPort
    {
        get => _internalPort;
        set { _internalPort = value; OnPropertyChanged(); }
    }

    public int ExternalPort
    {
        get => _externalPort;
        set { _externalPort = value; OnPropertyChanged(); }
    }

    public int LibraryWorkers
    {
        get => _libraryWorkers;
        set { _libraryWorkers = value; OnPropertyChanged(); }
    }

    public int ImportWorkers
    {
        get => _importWorkers;
        set { _importWorkers = value; OnPropertyChanged(); }
    }

    public int ExtrasWorkers
    {
        get => _extrasWorkers;
        set { _extrasWorkers = value; OnPropertyChanged(); }
    }

    public int EncoderWorkers
    {
        get => _encoderWorkers;
        set { _encoderWorkers = value; OnPropertyChanged(); }
    }

    public int CronWorkers
    {
        get => _cronWorkers;
        set { _cronWorkers = value; OnPropertyChanged(); }
    }

    public int ImageWorkers
    {
        get => _imageWorkers;
        set { _imageWorkers = value; OnPropertyChanged(); }
    }

    public int FileWorkers
    {
        get => _fileWorkers;
        set { _fileWorkers = value; OnPropertyChanged(); }
    }

    public int MusicWorkers
    {
        get => _musicWorkers;
        set { _musicWorkers = value; OnPropertyChanged(); }
    }

    public ServerControlViewModel(
        ServerConnection serverConnection,
        ServerProcessLauncher processLauncher)
    {
        _serverConnection = serverConnection;
        _processLauncher = processLauncher;
    }

    public async Task RefreshStatusAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_serverConnection.IsConnected)
            await _serverConnection.ConnectAsync(cancellationToken);

        ServerStatusResponse? status =
            await _serverConnection.GetAsync<ServerStatusResponse>(
                "/manage/status", cancellationToken);

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
            _ => status.Status
        };

        ServerName = string.IsNullOrEmpty(status.ServerName)
            ? "--"
            : status.ServerName;

        Version = string.IsNullOrEmpty(status.Version)
            ? "--"
            : status.Version;

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
            _ => "#EF4444"
        };

        AutoStartEnabled = status.AutoStart;
        UpdateAvailable = status.UpdateAvailable;
        RestartNeeded = status.RestartNeeded;
        LatestVersion = status.LatestVersion ?? string.Empty;

        if (!ConfigLoaded)
            await LoadConfigAsync(cancellationToken);
    }

    public async Task StopServerAsync()
    {
        if (IsActionInProgress) return;

        IsActionInProgress = true;
        ActionStatus = "Stopping server...";

        try
        {
            bool success = await _serverConnection
                .PostAsync("/manage/stop");

            ActionStatus = success
                ? "Stop command sent"
                : "Failed to send stop command";

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
        if (IsActionInProgress) return;

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
                ActionStatus = "Server did not stop in time";
                return;
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
        await _serverConnection.PostAsync(
            "/manage/autostart",
            new { enabled },
            default);

        await RefreshStatusAsync();
    }

    public async Task ApplyUpdateAsync()
    {
        if (IsActionInProgress) return;

        IsActionInProgress = true;
        ActionStatus = "Downloading update...";
        LauncherLog.Info("Update started: requesting server to download update");

        try
        {
            (bool downloaded, string? downloadBody) =
                await _serverConnection.PostWithBodyAsync("/manage/update");

            LauncherLog.Info($"POST /manage/update => success={downloaded}, body={downloadBody}");

            if (!downloaded)
            {
                string reason = ExtractMessage(downloadBody) ?? "Server returned an error";
                LauncherLog.Error($"Download step failed: {reason}");
                ActionStatus = $"Failed to download update: {reason}";
                return;
            }

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
                LauncherLog.Error("Server did not exit within 30 seconds");
                ActionStatus = "Server did not stop in time";
                return;
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
        catch (FileNotFoundException ex)
        {
            LauncherLog.Error("No staged update file found", ex);
            ActionStatus = "No staged update file found";
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

    private static string? ExtractMessage(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            dynamic? obj = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
            return obj?.message?.ToString();
        }
        catch
        {
            return null;
        }
    }

    public async Task LoadConfigAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_serverConnection.IsConnected)
            await _serverConnection.ConnectAsync(cancellationToken);

        ServerConfigResponse? config =
            await _serverConnection.GetAsync<ServerConfigResponse>(
                "/manage/config", cancellationToken);

        if (config is null) return;

        ConfigServerName = config.ServerName ?? string.Empty;
        InternalPort = config.InternalPort;
        ExternalPort = config.ExternalPort;
        LibraryWorkers = config.LibraryWorkers;
        ImportWorkers = config.ImportWorkers;
        ExtrasWorkers = config.ExtrasWorkers;
        EncoderWorkers = config.EncoderWorkers;
        CronWorkers = config.CronWorkers;
        ImageWorkers = config.ImageWorkers;
        FileWorkers = config.FileWorkers;
        MusicWorkers = config.MusicWorkers;
        ConfigLoaded = true;
    }

    public async Task SaveConfigAsync(
        CancellationToken cancellationToken = default)
    {
        if (IsActionInProgress) return;

        IsActionInProgress = true;
        ActionStatus = "Saving configuration...";

        try
        {
            bool success = await _serverConnection.PutAsync(
                "/manage/config",
                new
                {
                    server_name = ConfigServerName,
                    library_workers = LibraryWorkers,
                    import_workers = ImportWorkers,
                    extras_workers = ExtrasWorkers,
                    encoder_workers = EncoderWorkers,
                    cron_workers = CronWorkers,
                    image_workers = ImageWorkers,
                    file_workers = FileWorkers,
                    music_workers = MusicWorkers
                },
                cancellationToken);

            ActionStatus = success
                ? "Configuration saved"
                : "Failed to save configuration";
        }
        finally
        {
            IsActionInProgress = false;
        }
    }

    public async Task LaunchAppAsync()
    {
        if (IsActionInProgress) return;

        IsActionInProgress = true;
        ActionStatus = "Launching app...";

        try
        {
            bool launched;

            if (_serverConnection.IsConnected)
                launched = await _serverConnection.PostAsync("/manage/app/start");
            else
                launched = await _processLauncher.LaunchAppAsync();

            ActionStatus = launched
                ? "App launched"
                : "Failed to launch app";
        }
        finally
        {
            IsActionInProgress = false;
        }
    }

    public async Task StartServerAsync()
    {
        if (IsActionInProgress) return;

        IsActionInProgress = true;
        IsServerStopped = false;
        ActionStatus = "Starting server...";

        try
        {
            string extraArgs = LauncherSettings.Load().StartupArguments;
            bool started = await _processLauncher.StartServerAsync(extraArgs);

            ActionStatus = started
                ? "Server process launched"
                : "Failed to start server";

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

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await RefreshStatusAsync(token);
                await Task.Delay(
                    TimeSpan.FromSeconds(5), token);
            }
        }, token);
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
            _ => status
        };
    }

    internal static string GetStatusColor(string status)
    {
        return status switch
        {
            "running" or "Running" => "#22C55E",
            "starting" or "Starting" => "#EAB308",
            _ => "#EF4444"
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this, new(propertyName));
    }
}
