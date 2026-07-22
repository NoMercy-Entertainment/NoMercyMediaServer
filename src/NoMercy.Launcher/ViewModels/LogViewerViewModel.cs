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

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using NoMercy.Launcher.Models;
using NoMercy.Launcher.Services;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Logging;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Launcher.ViewModels;

public partial class LogViewerViewModel : INotifyPropertyChanged
{
    private readonly ServerConnection _serverConnection;
    private CancellationTokenSource? _streamCts;

    [GeneratedRegex(pattern: @"(\x1b|\\u001[bB])\[[0-9;]*[A-Za-z]")]
    private static partial Regex AnsiEscapeRegex();

    private string _searchText = string.Empty;
    private string _selectedLevel = "All";
    private int _tailCount = 200;
    private bool _autoRefresh = true;

    public ObservableCollection<LogEntryResponse> LogEntries { get; } = [];
    public ObservableCollection<LogEntryResponse> FilteredEntries { get; } = [];

    public List<string> LevelOptions { get; } =
    ["All", "Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

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
            ApplyFilter();
        }
    }

    public int TailCount
    {
        get => _tailCount;
        set
        {
            _tailCount = value;
            OnPropertyChanged();
            _ = RestartStreamAsync();
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
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "Ready";

    public LogViewerViewModel(ServerConnection serverConnection)
    {
        _serverConnection = serverConnection;
    }

    public async Task RefreshLogsAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;

        try
        {
            string path = $"/manage/logs?tail={_tailCount}";

            if (_selectedLevel != "All")
                path += $"&levels={_selectedLevel}";

            List<LogEntryResponse>? logs = await _serverConnection.GetAsync<List<LogEntryResponse>>(
                path: path,
                cancellationToken: cancellationToken
            );

            if (logs is null)
            {
                // Fall back to reading log files from disk
                await LoadLogsFromDiskAsync(cancellationToken: cancellationToken);
                return;
            }

            LogEntries.Clear();

            foreach (LogEntryResponse entry in logs)
            {
                CleanMessage(entry: entry);
                LogEntries.Add(item: entry);
            }

            ApplyFilter();
            StatusText =
                $"{FilteredEntries.Count} entries"
                + $" (fetched {logs.Count} at {DateTime.Now:HH:mm:ss})";
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch
        {
            await LoadLogsFromDiskAsync(cancellationToken: cancellationToken);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadLogsFromDiskAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string logPath = AppFiles.LogPath;
            // LOCAL-ONLY: Launcher is a separate GUI process; NoMercy.Service DI is not available here.
            IStorageDriver driver = new LocalStorageDriver();
            IStorage storage = new LocalStorage(driver: driver, guard: new(allowedRoots: [], driver: driver));
            if (!driver.DirectoryExists(path: logPath))
            {
                StatusText = "No log directory found";
                return;
            }

            List<LogEntry> diskLogs = await LogReader.GetLatestRunLogsAsync(storage: storage, logDirectoryPath: logPath);
            diskLogs = diskLogs
                .OrderByDescending(keySelector: e => e.Time)
                .Take(count: _tailCount)
                .OrderBy(keySelector: e => e.Time)
                .ToList();

            LogEntries.Clear();
            foreach (LogEntry entry in diskLogs)
            {
                LogEntryResponse response = new()
                {
                    Type = entry.Type,
                    Message = entry.Message,
                    Color = entry.Color,
                    ThreadId = entry.ThreadId,
                    Time = entry.Time,
                    Level = entry.Level,
                };
                CleanMessage(entry: response);
                LogEntries.Add(item: response);
            }

            ApplyFilter();
            StatusText = $"{FilteredEntries.Count} entries (from disk at {DateTime.Now:HH:mm:ss})";
        }
        catch
        {
            StatusText = "Error reading logs from disk";
        }
    }

    public void StartAutoRefresh()
    {
        StopAutoRefresh();
        _ = StartStreamAsync();
    }

    private async Task StartStreamAsync()
    {
        _streamCts = new();
        CancellationToken token = _streamCts.Token;

        // Load initial history (from server if connected, disk otherwise)
        await RefreshLogsAsync(cancellationToken: token);

        if (!_serverConnection.IsConnected)
            StatusText = $"{FilteredEntries.Count} entries (waiting for server)";
        else
            StatusText = $"{FilteredEntries.Count} entries (streaming)";

        // Open SSE stream for real-time updates.
        // StreamLogsAsync handles reconnection with backoff internally,
        // so this works even if the server isn't up yet.
        _ = Task.Run(
            function: async () =>
            {
                await _serverConnection.StreamLogsAsync(
                    onEntry: entry =>
                    {
                        CleanMessage(entry: entry);

                        // Filter by level client-side
                        if (
                            _selectedLevel != "All"
                            && !string.Equals(
                                a: entry.Level,
                                b: _selectedLevel,
                                comparisonType: StringComparison.OrdinalIgnoreCase
                            )
                        )
                        {
                            return;
                        }

                        Dispatcher.UIThread.Post(action: () =>
                        {
                            LogEntries.Add(item: entry);

                            // Check if entry matches current filter
                            if (MatchesFilter(entry: entry))
                            {
                                FilteredEntries.Add(item: entry);
                                StatusText = $"{FilteredEntries.Count} entries (streaming)";
                            }

                            // Trim old entries to keep memory bounded
                            while (LogEntries.Count > _tailCount * 2)
                                LogEntries.RemoveAt(index: 0);
                            while (FilteredEntries.Count > _tailCount * 2)
                                FilteredEntries.RemoveAt(index: 0);
                        });
                    },
                    cancellationToken: token,
                    onConnected: () =>
                    {
                        Dispatcher.UIThread.Post(action: () =>
                            StatusText = $"{FilteredEntries.Count} entries (streaming)"
                        );
                    },
                    onDisconnected: () =>
                    {
                        Dispatcher.UIThread.Post(action: () =>
                            StatusText = $"{FilteredEntries.Count} entries (reconnecting...)"
                        );
                    }
                );
            },
            cancellationToken: token
        );
    }

    private async Task RestartStreamAsync()
    {
        StopAutoRefresh();
        if (_autoRefresh)
            await StartStreamAsync();
        else
            await RefreshLogsAsync();
    }

    public void StopAutoRefresh()
    {
        _streamCts?.Cancel();
        _streamCts?.Dispose();
        _streamCts = null;
    }

    private bool MatchesFilter(LogEntryResponse entry)
    {
        if (string.IsNullOrWhiteSpace(value: _searchText))
            return true;

        return entry.Message.Contains(value: _searchText, comparisonType: StringComparison.OrdinalIgnoreCase)
            || entry.Type.Contains(value: _searchText, comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    internal void ApplyFilter()
    {
        FilteredEntries.Clear();

        IEnumerable<LogEntryResponse> filtered = LogEntries.AsEnumerable();

        if (_selectedLevel != "All")
        {
            filtered = filtered.Where(predicate: e =>
                string.Equals(a: e.Level, b: _selectedLevel, comparisonType: StringComparison.OrdinalIgnoreCase)
            );
        }

        if (!string.IsNullOrWhiteSpace(value: _searchText))
        {
            filtered = filtered.Where(predicate: e =>
                e.Message.Contains(value: _searchText, comparisonType: StringComparison.OrdinalIgnoreCase)
                || e.Type.Contains(value: _searchText, comparisonType: StringComparison.OrdinalIgnoreCase)
            );
        }

        foreach (LogEntryResponse entry in filtered)
            FilteredEntries.Add(item: entry);
    }

    private static void CleanMessage(LogEntryResponse entry)
    {
        string message = entry.Message;

        // Strip surrounding quotes from double-serialization
        if (message is ['"', _, ..] && message[^1] == '"')
        {
            message = message[1..^1];
        }

        // Strip ANSI escape codes
        message = AnsiEscapeRegex().Replace(input: message, replacement: "");

        // Unescape any remaining JSON escapes from double-serialization
        message = message
            .Replace(oldValue: "\\n", newValue: "\n")
            .Replace(oldValue: "\\r", newValue: "\r")
            .Replace(oldValue: "\\t", newValue: "\t")
            .Replace(oldValue: "\\\"", newValue: "\"")
            .Replace(oldValue: @"\\", newValue: "\\");

        entry.Message = message;
    }

    public void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedLevel = "All";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(sender: this, e: new(propertyName: propertyName));
    }
}
