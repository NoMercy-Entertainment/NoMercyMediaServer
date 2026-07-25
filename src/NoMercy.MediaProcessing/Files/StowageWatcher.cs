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
            null,
            new(
                ChangeType switch
                {
                    StowageChangeType.Created => WatcherChangeTypes.Created,
                    StowageChangeType.Changed => WatcherChangeTypes.Changed,
                    StowageChangeType.Deleted => WatcherChangeTypes.Deleted,
                    _ => throw new ArgumentOutOfRangeException(),
                },
                System.IO.Path.GetDirectoryName(Path).OrEmpty(),
                System.IO.Path.GetFileName(Path)
            )
        );
    }

    public FileSystemEventArgs ToFileSystemEventArgsEventArgs(string folder = "")
    {
        return new(
            ChangeType switch
            {
                StowageChangeType.Created => WatcherChangeTypes.Created,
                StowageChangeType.Changed => WatcherChangeTypes.Changed,
                StowageChangeType.Deleted => WatcherChangeTypes.Deleted,
                _ => throw new ArgumentOutOfRangeException(),
            },
            System.IO.Path.GetDirectoryName(folder + Path).OrEmpty(),
            System.IO.Path.GetFileName(folder + Path)
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
        _runTask = Task.Run(() => WatchLoopAsync(interval, _cts.Token));
    }

    private async Task WatchLoopAsync(TimeSpan interval, CancellationToken ct)
    {
        using PeriodicTimer timer = new(interval);

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
                await Scan(true);
                seeded = true;
            }
            catch (Exception ex)
            {
                Logger.Error(
                    $"StowageWatcher initial scan of '{_path}' failed, retrying: {ex.Message}"
                );
            }
        } while (!seeded && await timer.WaitForNextTickAsync(ct));

        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await Scan(false);
            }
            catch (Exception ex)
            {
                Logger.Error($"Fout: {ex.Message}");
            }
        }
    }

    private async Task Scan(bool initial)
    {
        IReadOnlyCollection<IOEntry> entries = await _storage.Ls(_path, true);
        List<IOEntry> files = entries.Where(e => !e.Path.IsFolder).ToList();

        ConcurrentBag<string> foundPaths = [];

        Parallel.ForEach(
            files,
            entry =>
            {
                foundPaths.Add(entry.Path);

                if (!_snapshot.TryGetValue(entry.Path, out IOEntry? oldEntry))
                {
                    _snapshot[entry.Path] = entry;
                    if (initial)
                        return;
                    Created?.Invoke(
                        new()
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
                    _snapshot[entry.Path] = entry;
                    if (initial)
                        return;
                    Changed?.Invoke(
                        new()
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
        HashSet<string> currentPathsSet = new(foundPaths);

        Parallel.ForEach(
            snapshotKeys,
            path =>
            {
                if (currentPathsSet.Contains(path))
                    return;
                if (!_snapshot.TryRemove(path, out IOEntry? oldEntry))
                    return;
                if (initial)
                    return;
                Deleted?.Invoke(
                    new()
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
            _runTask?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException ae)
            when (ae.InnerExceptions.All(e => e is OperationCanceledException))
        {
            // Expected when cancellation cooperatively stopped the loop.
        }

        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
