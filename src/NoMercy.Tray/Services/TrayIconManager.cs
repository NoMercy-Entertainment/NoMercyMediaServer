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
    private readonly ServerProcessLauncher _processLauncher;
    private readonly IClassicDesktopStyleApplicationLifetime _lifetime;
    private TrayIcon? _trayIcon;
    private NativeMenuItem? _statusItem;
    private NativeMenuItem? _versionItem;
    private NativeMenuItem? _uptimeItem;
    private NativeMenuItem? _startServerItem;
    private NativeMenuItem? _stopServerItem;
    private ServerState _currentState = ServerState.Disconnected;
    private MainWindow? _mainWindow;
    private bool _autoStartAttempted;

    public TrayIconManager(
        ServerConnection serverConnection,
        ServerProcessLauncher processLauncher,
        IClassicDesktopStyleApplicationLifetime lifetime)
    {
        _serverConnection = serverConnection;
        _processLauncher = processLauncher;
        _lifetime = lifetime;
    }

    public void Initialize()
    {
        NativeMenu menu = new();

        _statusItem = new("Server: Disconnected")
        {
            IsEnabled = false
        };

        _versionItem = new("Version: --")
        {
            IsEnabled = false
        };

        _uptimeItem = new("Uptime: --")
        {
            IsEnabled = false
        };

        NativeMenuItemSeparator separator1 = new();

        NativeMenuItem openAppItem = new("Open App");
        openAppItem.Click += OnOpenApp;

        NativeMenuItem openDashboardItem = new("Open Dashboard");
        openDashboardItem.Click += OnOpenDashboard;

        NativeMenuItem serverControlItem = new("Server Control");
        serverControlItem.Click += OnServerControl;

        NativeMenuItem viewLogsItem = new("View Logs");
        viewLogsItem.Click += OnViewLogs;

        NativeMenuItemSeparator separator2 = new();

        _startServerItem = new("Start Server");
        _startServerItem.Click += OnStartServer;

        _stopServerItem = new("Stop Server");
        _stopServerItem.IsEnabled = false;
        _stopServerItem.Click += OnStopServer;

        NativeMenuItemSeparator separator3 = new();

        NativeMenuItem quitItem = new("Quit");
        quitItem.Click += OnQuit;

        menu.Items.Add(_statusItem);
        menu.Items.Add(_versionItem);
        menu.Items.Add(_uptimeItem);
        menu.Items.Add(separator1);
        menu.Items.Add(openAppItem);
        menu.Items.Add(openDashboardItem);
        menu.Items.Add(serverControlItem);
        menu.Items.Add(viewLogsItem);
        menu.Items.Add(separator2);
        menu.Items.Add(_startServerItem);
        menu.Items.Add(_stopServerItem);
        menu.Items.Add(separator3);
        menu.Items.Add(quitItem);

        WindowIcon initialIcon =
            TrayIconFactory.CreateIcon(ServerState.Disconnected);

        _trayIcon = new()
        {
            Icon = initialIcon,
            ToolTipText = "NoMercy MediaServer - Disconnected",
            Menu = menu,
            IsVisible = true
        };

        _trayIcon.Clicked += OnTrayIconClicked;

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
                if (!_autoStartAttempted)
                {
                    _autoStartAttempted = true;
                    await _processLauncher.StartServerAsync();
                }

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

        if (_startServerItem is not null)
            _startServerItem.IsEnabled =
                state == ServerState.Disconnected;

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

    private void OpenMainWindow(int selectedTab)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_mainWindow is { IsVisible: true })
            {
                _mainWindow.SelectTab(selectedTab);
                _mainWindow.Activate();
                return;
            }

            MainViewModel viewModel = new(
                _serverConnection, _processLauncher);
            viewModel.SelectedTabIndex = selectedTab;
            _mainWindow = new(viewModel);
            _mainWindow.Closed += (_, _) => _mainWindow = null;
            _mainWindow.Show();
        });
    }

    private void OnTrayIconClicked(object? sender, EventArgs e)
    {
        OpenMainWindow(0);
    }

    private async void OnOpenApp(object? sender, EventArgs e)
    {
        await _processLauncher.LaunchAppAsync();
    }

    private void OnOpenDashboard(object? sender, EventArgs e)
    {
        string url = "https://app.nomercy.tv";
        OpenUrl(url);
    }

    private void OnViewLogs(object? sender, EventArgs e)
    {
        OpenMainWindow(1);
    }

    private void OnServerControl(object? sender, EventArgs e)
    {
        OpenMainWindow(0);
    }

    private async void OnStartServer(object? sender, EventArgs e)
    {
        await _processLauncher.StartServerAsync();
    }

    private async void OnStopServer(object? sender, EventArgs e)
    {
        await _serverConnection.PostAsync("/manage/stop");
    }

    private void OnQuit(object? sender, EventArgs e)
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Clicked -= OnTrayIconClicked;
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
