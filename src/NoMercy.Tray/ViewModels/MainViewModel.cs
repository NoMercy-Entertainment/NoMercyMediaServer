using System.ComponentModel;
using System.Runtime.CompilerServices;
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
        Dictionary<string, object>? status =
            await _serverConnection.GetAsync<Dictionary<string, object>>(
                "/manage/status", cancellationToken);

        if (status is null)
        {
            ServerStatus = "Disconnected";
            return;
        }

        if (status.TryGetValue("status", out object? s))
            ServerStatus = s?.ToString() ?? "Unknown";

        if (status.TryGetValue("version", out object? v))
            ServerVersion = v?.ToString() ?? "Unknown";

        if (status.TryGetValue("uptimeSeconds", out object? u)
            && long.TryParse(u?.ToString(), out long uptime))
            UptimeSeconds = uptime;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this, new PropertyChangedEventArgs(propertyName));
    }
}
