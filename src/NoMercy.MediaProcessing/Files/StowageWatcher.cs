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

using System.Collections.Concurrent;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using Stowage;

namespace NoMercy.MediaProcessing.Files;

public enum StowageChangeType
{
    Created,
    Changed,
    Deleted,
}

public class StowageWatcherEventArgs : EventArgs
{
    public string Path { get; init; } = string.Empty;
    public StowageChangeType ChangeType { get; init; }
    public IOEntry? Entry { get; init; }
    public DateTime EventTimestamp { get; init; } = DateTime.UtcNow;

    public FileWatcherEventArgs ToFileWatcherEventArgs()
    {
        return new(
            sender: null,
            fileSystemEventArgs: new(
                changeType: ChangeType switch
                {
                    StowageChangeType.Created => WatcherChangeTypes.Created,
                    StowageChangeType.Changed => WatcherChangeTypes.Changed,
                    StowageChangeType.Deleted => WatcherChangeTypes.Deleted,
                    _ => throw new ArgumentOutOfRangeException(),
                },
                directory: System.IO.Path.GetDirectoryName(path: Path).OrEmpty(),
                name: System.IO.Path.GetFileName(path: Path)
            )
        );
    }

    public FileSystemEventArgs ToFileSystemEventArgsEventArgs(string folder = "")
    {
        return new(
            changeType: ChangeType switch
            {
                StowageChangeType.Created => WatcherChangeTypes.Created,
                StowageChangeType.Changed => WatcherChangeTypes.Changed,
                StowageChangeType.Deleted => WatcherChangeTypes.Deleted,
                _ => throw new ArgumentOutOfRangeException(),
            },
            directory: System.IO.Path.GetDirectoryName(path: folder + Path).OrEmpty(),
            name: System.IO.Path.GetFileName(path: folder + Path)
        );
    }
}

internal class StowageWatcher : IDisposable
{
    private readonly IFileStorage _storage;
    private readonly string _path;
    private readonly ConcurrentDictionary<string, IOEntry> _snapshot = new();
    private CancellationTokenSource? _cts;
    private Task? _runTask;

    public event Action<StowageWatcherEventArgs>? Changed;
    public event Action<StowageWatcherEventArgs>? Created;
    public event Action<StowageWatcherEventArgs>? Deleted;

    public StowageWatcher(IFileStorage storage, string path = "/")
    {
        _storage = storage;
        _path = path;
    }

    public void Watch(TimeSpan interval)
    {
        if (_runTask != null)
            return;
        _cts = new();
        _runTask = Task.Run(function: () => WatchLoopAsync(interval: interval, ct: _cts.Token));
    }

    private async Task WatchLoopAsync(TimeSpan interval, CancellationToken ct)
    {
        using PeriodicTimer timer = new(period: interval);

        // Seed the snapshot WITHOUT emitting events. A network backend (NFS/SMB/S3)
        // may not be reachable yet at boot; if this first scan threw here — outside
        // the loop — the whole watch task faulted and died unobserved, leaving the
        // folder permanently unwatched until a restart. Retry on the timer instead,
        // and don't enter change-detection until a baseline exists, otherwise the
        // first successful scan would report every pre-existing file as newly Created.
        bool seeded = false;
        do
        {
            try
            {
                await Scan(initial: true);
                seeded = true;
            }
            catch (Exception ex)
            {
                Logger.Error(
                    message: $"StowageWatcher initial scan of '{_path}' failed, retrying: {ex.Message}"
                );
            }
        } while (!seeded && await timer.WaitForNextTickAsync(cancellationToken: ct));

        while (await timer.WaitForNextTickAsync(cancellationToken: ct))
        {
            try
            {
                await Scan(initial: false);
            }
            catch (Exception ex)
            {
                Logger.Error(message: $"Fout: {ex.Message}");
            }
        }
    }

    private async Task Scan(bool initial)
    {
        IReadOnlyCollection<IOEntry> entries = await _storage.Ls(path: _path, recurse: true);
        List<IOEntry> files = entries.Where(predicate: e => !e.Path.IsFolder).ToList();

        ConcurrentBag<string> foundPaths = [];

        Parallel.ForEach(
            source: files,
            body: entry =>
            {
                foundPaths.Add(item: entry.Path);

                if (!_snapshot.TryGetValue(key: entry.Path, value: out IOEntry? oldEntry))
                {
                    _snapshot[key: entry.Path] = entry;
                    if (initial)
                        return;
                    Created?.Invoke(
                        obj: new()
                        {
                            Path = entry.Path.ToString(),
                            ChangeType = StowageChangeType.Created,
                            Entry = entry,
                            EventTimestamp = DateTime.UtcNow,
                        }
                    );
                }
                // Check op LastModification of Size (Stowage entries hebben deze eigenschappen ook)
                else if (
                    entry.LastModificationTime > oldEntry.LastModificationTime
                    || entry.Size != oldEntry.Size
                )
                {
                    _snapshot[key: entry.Path] = entry;
                    if (initial)
                        return;
                    Changed?.Invoke(
                        obj: new()
                        {
                            Path = entry.Path.ToString(),
                            ChangeType = StowageChangeType.Changed,
                            Entry = entry,
                            EventTimestamp = DateTime.UtcNow,
                        }
                    );
                }
            }
        );

        ICollection<string> snapshotKeys = _snapshot.Keys;
        HashSet<string> currentPathsSet = new(collection: foundPaths);

        Parallel.ForEach(
            source: snapshotKeys,
            body: path =>
            {
                if (currentPathsSet.Contains(item: path))
                    return;
                if (!_snapshot.TryRemove(key: path, value: out IOEntry? oldEntry))
                    return;
                if (initial)
                    return;
                Deleted?.Invoke(
                    obj: new()
                    {
                        Path = path,
                        ChangeType = StowageChangeType.Deleted,
                        Entry = null,
                        EventTimestamp = DateTime.UtcNow,
                    }
                );
            }
        );
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException) { }

        try
        {
            _runTask?.Wait(timeout: TimeSpan.FromSeconds(seconds: 5));
        }
        catch (AggregateException ae)
            when (ae.InnerExceptions.All(predicate: e => e is OperationCanceledException))
        {
            // Expected when cancellation cooperatively stopped the loop.
        }

        _cts?.Dispose();
        GC.SuppressFinalize(obj: this);
    }
}
