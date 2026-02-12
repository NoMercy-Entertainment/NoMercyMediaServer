using System.ComponentModel;
using System.Runtime.CompilerServices;
using NoMercy.Tray.Models;
using NoMercy.Tray.Services;

namespace NoMercy.Tray.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ServerConnection _serverConnection;

    private string _serverStatus = "Disconnected";
    private string _serverVersion = "Unknown";
    private long _uptimeSeconds;

    public string ServerStatus
    {
        get => _serverStatus;
        set { _serverStatus = value; OnPropertyChanged(); }
    }

    public string ServerVersion
    {
        get => _serverVersion;
        set { _serverVersion = value; OnPropertyChanged(); }
    }

    public long UptimeSeconds
    {
        get => _uptimeSeconds;
        set { _uptimeSeconds = value; OnPropertyChanged(); }
    }

    public MainViewModel(ServerConnection serverConnection)
    {
        _serverConnection = serverConnection;
    }

    public async Task RefreshStatusAsync(
        CancellationToken cancellationToken = default)
    {
        ServerStatusResponse? status =
            await _serverConnection.GetAsync<ServerStatusResponse>(
                "/manage/status", cancellationToken);

        if (status is null)
        {
            ServerStatus = "Disconnected";
            ServerVersion = "Unknown";
            UptimeSeconds = 0;
            return;
        }

        ServerStatus = status.Status;
        ServerVersion = string.IsNullOrEmpty(status.Version)
            ? "Unknown"
            : status.Version;
        UptimeSeconds = status.UptimeSeconds;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this, new PropertyChangedEventArgs(propertyName));
    }
}
