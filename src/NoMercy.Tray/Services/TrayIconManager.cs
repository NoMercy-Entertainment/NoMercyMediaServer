using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using NoMercy.Tray.Models;
using NoMercy.Tray.ViewModels;
using NoMercy.Tray.Views;

namespace NoMercy.Tray.Services;

public class TrayIconManager
{
    private readonly ServerConnection _serverConnection;
    private readonly IClassicDesktopStyleApplicationLifetime _lifetime;
    private TrayIcon? _trayIcon;
    private NativeMenuItem? _statusItem;
    private NativeMenuItem? _versionItem;
    private NativeMenuItem? _uptimeItem;
    private NativeMenuItem? _stopServerItem;
    private ServerState _currentState = ServerState.Disconnected;
    private LogViewerWindow? _logViewerWindow;
    private ServerControlWindow? _serverControlWindow;

    public TrayIconManager(
        ServerConnection serverConnection,
        IClassicDesktopStyleApplicationLifetime lifetime)
    {
        _serverConnection = serverConnection;
        _lifetime = lifetime;
    }

    public void Initialize()
    {
        NativeMenu menu = new();

        _statusItem = new NativeMenuItem("Server: Disconnected")
        {
            IsEnabled = false
        };

        _versionItem = new NativeMenuItem("Version: --")
        {
            IsEnabled = false
        };

        _uptimeItem = new NativeMenuItem("Uptime: --")
        {
            IsEnabled = false
        };

        NativeMenuItemSeparator separator1 = new();

        NativeMenuItem openDashboardItem = new("Open Dashboard");
        openDashboardItem.Click += OnOpenDashboard;

        NativeMenuItem viewLogsItem = new("View Logs");
        viewLogsItem.Click += OnViewLogs;

        NativeMenuItem serverControlItem = new("Server Control");
        serverControlItem.Click += OnServerControl;

        NativeMenuItemSeparator separator2 = new();

        _stopServerItem = new NativeMenuItem("Stop Server");
        _stopServerItem.Click += OnStopServer;

        NativeMenuItemSeparator separator3 = new();

        NativeMenuItem quitItem = new("Quit Tray");
        quitItem.Click += OnQuit;

        menu.Items.Add(_statusItem);
        menu.Items.Add(_versionItem);
        menu.Items.Add(_uptimeItem);
        menu.Items.Add(separator1);
        menu.Items.Add(openDashboardItem);
        menu.Items.Add(viewLogsItem);
        menu.Items.Add(serverControlItem);
        menu.Items.Add(separator2);
        menu.Items.Add(_stopServerItem);
        menu.Items.Add(separator3);
        menu.Items.Add(quitItem);

        WindowIcon initialIcon =
            TrayIconFactory.CreateIcon(ServerState.Disconnected);

        _trayIcon = new TrayIcon
        {
            Icon = initialIcon,
            ToolTipText = "NoMercy MediaServer - Disconnected",
            Menu = menu,
            IsVisible = true
        };

        _ = PollServerStatusAsync();
    }

    private async Task PollServerStatusAsync()
    {
        while (_trayIcon?.IsVisible == true)
        {
            await UpdateStatusAsync();
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }

    private async Task UpdateStatusAsync()
    {
        ServerStatusResponse? status =
            await _serverConnection.GetAsync<ServerStatusResponse>(
                "/manage/status");

        if (status is null)
        {
            bool wasConnected =
                await _serverConnection.ConnectAsync();

            if (!wasConnected)
            {
                SetState(
                    ServerState.Disconnected,
                    "Disconnected",
                    null,
                    null);
                return;
            }

            status = await _serverConnection
                .GetAsync<ServerStatusResponse>("/manage/status");
        }

        if (status is null)
        {
            SetState(
                ServerState.Disconnected,
                "Disconnected",
                null,
                null);
            return;
        }

        ServerState state = status.Status switch
        {
            "running" => ServerState.Running,
            "starting" => ServerState.Starting,
            _ => ServerState.Running
        };

        string uptimeText = FormatUptime(status.UptimeSeconds);
        string versionText = string.IsNullOrEmpty(status.Version)
            ? null!
            : status.Version;

        SetState(state, status.Status, versionText, uptimeText);
    }

    private void SetState(
        ServerState state,
        string statusText,
        string? version,
        string? uptime)
    {
        if (_trayIcon is null) return;

        if (state != _currentState)
        {
            _currentState = state;
            _trayIcon.Icon = TrayIconFactory.CreateIcon(state);
        }

        string stateLabel = state switch
        {
            ServerState.Running => "Running",
            ServerState.Starting => "Starting",
            ServerState.Disconnected => "Disconnected",
            _ => "Unknown"
        };

        _trayIcon.ToolTipText =
            $"NoMercy MediaServer - {stateLabel}";

        if (_statusItem is not null)
            _statusItem.Header = $"Server: {stateLabel}";

        if (_versionItem is not null)
            _versionItem.Header = version is not null
                ? $"Version: {version}"
                : "Version: --";

        if (_uptimeItem is not null)
            _uptimeItem.Header = uptime is not null
                ? $"Uptime: {uptime}"
                : "Uptime: --";

        if (_stopServerItem is not null)
            _stopServerItem.IsEnabled =
                state != ServerState.Disconnected;
    }

    internal static string FormatUptime(long totalSeconds)
    {
        TimeSpan span = TimeSpan.FromSeconds(totalSeconds);

        if (span.TotalDays >= 1)
            return $"{(int)span.TotalDays}d {span.Hours}h {span.Minutes}m";

        if (span.TotalHours >= 1)
            return $"{span.Hours}h {span.Minutes}m";

        return $"{span.Minutes}m {span.Seconds}s";
    }

    private void OnOpenDashboard(object? sender, EventArgs e)
    {
        string url = "https://app.nomercy.tv";
        OpenUrl(url);
    }

    private void OnViewLogs(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_logViewerWindow is { IsVisible: true })
            {
                _logViewerWindow.Activate();
                return;
            }

            LogViewerViewModel viewModel = new(_serverConnection);
            _logViewerWindow = new LogViewerWindow(viewModel);
            _logViewerWindow.Closed += (_, _) => _logViewerWindow = null;
            _logViewerWindow.Show();
        });
    }

    private void OnServerControl(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_serverControlWindow is { IsVisible: true })
            {
                _serverControlWindow.Activate();
                return;
            }

            ServerControlViewModel viewModel = new(_serverConnection);
            _serverControlWindow = new ServerControlWindow(viewModel);
            _serverControlWindow.Closed += (_, _) =>
                _serverControlWindow = null;
            _serverControlWindow.Show();
        });
    }

    private async void OnStopServer(object? sender, EventArgs e)
    {
        await _serverConnection.PostAsync("/manage/stop");
    }

    private void OnQuit(object? sender, EventArgs e)
    {
        if (_trayIcon is not null)
        {
            _trayIcon.IsVisible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        _serverConnection.Dispose();
        _lifetime.Shutdown();
    }

    private static void OpenUrl(string url)
    {
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
    }
}
