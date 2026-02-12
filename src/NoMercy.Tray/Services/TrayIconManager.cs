using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace NoMercy.Tray.Services;

public class TrayIconManager
{
    private readonly ServerConnection _serverConnection;
    private readonly IClassicDesktopStyleApplicationLifetime _lifetime;
    private TrayIcon? _trayIcon;

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

        NativeMenuItem statusItem = new("NoMercy MediaServer")
        {
            IsEnabled = false
        };

        NativeMenuItem openDashboardItem = new("Open Dashboard");
        openDashboardItem.Click += OnOpenDashboard;

        NativeMenuItemSeparator separator1 = new();

        NativeMenuItem stopServerItem = new("Stop Server");
        stopServerItem.Click += OnStopServer;

        NativeMenuItemSeparator separator2 = new();

        NativeMenuItem quitItem = new("Quit Tray");
        quitItem.Click += OnQuit;

        menu.Items.Add(statusItem);
        menu.Items.Add(openDashboardItem);
        menu.Items.Add(separator1);
        menu.Items.Add(stopServerItem);
        menu.Items.Add(separator2);
        menu.Items.Add(quitItem);

        _trayIcon = new TrayIcon
        {
            ToolTipText = "NoMercy MediaServer",
            Menu = menu,
            IsVisible = true
        };

        _ = PollServerStatusAsync();
    }

    private async Task PollServerStatusAsync()
    {
        while (_trayIcon?.IsVisible == true)
        {
            bool connected = await _serverConnection.ConnectAsync();

            string tooltip = connected
                ? "NoMercy MediaServer - Running"
                : "NoMercy MediaServer - Disconnected";

            if (_trayIcon is not null)
            {
                _trayIcon.ToolTipText = tooltip;
            }

            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }

    private void OnOpenDashboard(object? sender, EventArgs e)
    {
        string url = "https://app.nomercy.tv";
        OpenUrl(url);
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
