using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NoMercy.Tray.Models;
using NoMercy.Tray.Services;

namespace NoMercy.Tray.ViewModels;

public class LogViewerViewModel : INotifyPropertyChanged
{
    private readonly ServerConnection _serverConnection;
    private CancellationTokenSource? _autoRefreshCts;

    private string _searchText = string.Empty;
    private string _selectedLevel = "All";
    private int _tailCount = 200;
    private bool _autoRefresh = true;
    private bool _isLoading;
    private string _statusText = "Ready";

    public ObservableCollection<LogEntryResponse> LogEntries { get; } = [];
    public ObservableCollection<LogEntryResponse> FilteredEntries { get; } = [];

    public List<string> LevelOptions { get; } =
    [
        "All",
        "Verbose",
        "Debug",
        "Information",
        "Warning",
        "Error",
        "Fatal"
    ];

    public List<int> TailOptions { get; } = [100, 200, 500, 1000];

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    public string SelectedLevel
    {
        get => _selectedLevel;
        set
        {
            _selectedLevel = value;
            OnPropertyChanged();
            _ = RefreshLogsAsync();
        }
    }

    public int TailCount
    {
        get => _tailCount;
        set
        {
            _tailCount = value;
            OnPropertyChanged();
            _ = RefreshLogsAsync();
        }
    }

    public bool AutoRefresh
    {
        get => _autoRefresh;
        set
        {
            _autoRefresh = value;
            OnPropertyChanged();

            if (value)
                StartAutoRefresh();
            else
                StopAutoRefresh();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public LogViewerViewModel(ServerConnection serverConnection)
    {
        _serverConnection = serverConnection;
    }

    public async Task RefreshLogsAsync(
        CancellationToken cancellationToken = default)
    {
        IsLoading = true;

        try
        {
            string path = $"/manage/logs?tail={_tailCount}";

            if (_selectedLevel != "All")
                path += $"&levels={_selectedLevel}";

            List<LogEntryResponse>? logs = await _serverConnection
                .GetAsync<List<LogEntryResponse>>(path, cancellationToken);

            if (logs is null)
            {
                StatusText = "Failed to fetch logs";
                return;
            }

            LogEntries.Clear();

            foreach (LogEntryResponse entry in logs)
                LogEntries.Add(entry);

            ApplyFilter();
            StatusText = $"{FilteredEntries.Count} entries" +
                $" (fetched {logs.Count} at {DateTime.Now:HH:mm:ss})";
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch
        {
            StatusText = "Error fetching logs";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void StartAutoRefresh()
    {
        StopAutoRefresh();

        _autoRefreshCts = new();
        CancellationToken token = _autoRefreshCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await RefreshLogsAsync(token);
                await Task.Delay(
                    TimeSpan.FromSeconds(5), token);
            }
        }, token);
    }

    public void StopAutoRefresh()
    {
        _autoRefreshCts?.Cancel();
        _autoRefreshCts?.Dispose();
        _autoRefreshCts = null;
    }

    internal void ApplyFilter()
    {
        FilteredEntries.Clear();

        IEnumerable<LogEntryResponse> filtered = LogEntries.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            filtered = filtered.Where(e =>
                e.Message.Contains(
                    _searchText, StringComparison.OrdinalIgnoreCase) ||
                e.Type.Contains(
                    _searchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (LogEntryResponse entry in filtered)
            FilteredEntries.Add(entry);
    }

    public void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedLevel = "All";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this, new(propertyName));
    }
}
