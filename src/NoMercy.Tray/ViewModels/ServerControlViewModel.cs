using System.ComponentModel;
using System.Runtime.CompilerServices;
using NoMercy.Tray.Models;
using NoMercy.Tray.Services;

namespace NoMercy.Tray.ViewModels;

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
        ActionStatus = "Restarting server...";

        try
        {
            bool success = await _serverConnection
                .PostAsync("/manage/restart");

            ActionStatus = success
                ? "Restart command sent"
                : "Failed to send restart command";

            await Task.Delay(2000);
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

    public async Task StartServerAsync()
    {
        if (IsActionInProgress) return;

        IsActionInProgress = true;
        IsServerStopped = false;
        ActionStatus = "Starting server...";

        try
        {
            bool started = await _processLauncher.StartServerAsync();

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
