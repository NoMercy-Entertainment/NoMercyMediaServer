using System.Collections.Concurrent;
using Stowage;

namespace NoMercy.MediaProcessing.Files;

public enum StowageChangeType { Created, Changed, Deleted }

public class StowageWatcherEventArgs : EventArgs
{
    public string Path { get; init; } = string.Empty;
    public StowageChangeType ChangeType { get; init; }
    public IOEntry? Entry { get; init; }
    public DateTime EventTimestamp { get; init; } = DateTime.UtcNow;

    public FileWatcherEventArgs ToFileWatcherEventArgs()
    {
        return new(null, new(
            changeType: ChangeType switch
            {
                StowageChangeType.Created => WatcherChangeTypes.Created,
                StowageChangeType.Changed => WatcherChangeTypes.Changed,
                StowageChangeType.Deleted => WatcherChangeTypes.Deleted,
                _ => throw new ArgumentOutOfRangeException()
            },
            directory: System.IO.Path.GetDirectoryName(Path) ?? string.Empty,
            name: System.IO.Path.GetFileName(Path)
        ));
    }

    public FileSystemEventArgs ToFileSystemEventArgsEventArgs(string folder = "")
    {
        return new(
            ChangeType switch
            {
                StowageChangeType.Created => WatcherChangeTypes.Created,
                StowageChangeType.Changed => WatcherChangeTypes.Changed,
                StowageChangeType.Deleted => WatcherChangeTypes.Deleted,
                _ => throw new ArgumentOutOfRangeException()
            },
            System.IO.Path.GetDirectoryName(folder + Path) ?? string.Empty,
            System.IO.Path.GetFileName(folder + Path));
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
        if (_runTask != null) return;
        _cts = new CancellationTokenSource();
        _runTask = Task.Run(() => WatchLoopAsync(interval, _cts.Token));
    }

    private async Task WatchLoopAsync(TimeSpan interval, CancellationToken ct)
    {
        using PeriodicTimer timer = new(interval);
        // Stowage gebruikt IOEntry in plaats van Blob
        await Scan(initial: true);

        while (await timer.WaitForNextTickAsync(ct))
        {
            try { await Scan(initial: false); }
            catch (Exception ex) { Console.WriteLine($"Fout: {ex.Message}"); }
        }
    }

    private async Task Scan(bool initial)
    {
        IReadOnlyCollection<IOEntry> entries = await _storage.Ls(_path, recurse: true);
        List<IOEntry> files = entries.Where(e => !e.Path.IsFolder).ToList();

        ConcurrentBag<string> foundPaths = [];

        Parallel.ForEach(files, entry =>
        {
            foundPaths.Add(entry.Path);

            if (!_snapshot.TryGetValue(entry.Path, out IOEntry? oldEntry))
            {
                _snapshot[entry.Path] = entry;
                if (initial) return;
                Created?.Invoke(new()
                {
                    Path = entry.Path.ToString(),
                    ChangeType = StowageChangeType.Created,
                    Entry = entry,
                    EventTimestamp = DateTime.UtcNow
                });
            }
            // Check op LastModification of Size (Stowage entries hebben deze eigenschappen ook)
            else if (entry.LastModificationTime > oldEntry.LastModificationTime || entry.Size != oldEntry.Size)
            {
                _snapshot[entry.Path] = entry;
                if (initial) return;
                Changed?.Invoke(new()
                {
                    Path = entry.Path.ToString(),
                    ChangeType = StowageChangeType.Changed,
                    Entry = entry,
                    EventTimestamp = DateTime.UtcNow
                });
            }
        });

        ICollection<string> snapshotKeys = _snapshot.Keys;
        HashSet<string> currentPathsSet = new(foundPaths);

        Parallel.ForEach(snapshotKeys, path =>
        {
            if (currentPathsSet.Contains(path)) return;
            if (!_snapshot.TryRemove(path, out IOEntry? oldEntry)) return;
            if (initial) return;
            Deleted?.Invoke(new()
            {
                Path = path,
                ChangeType = StowageChangeType.Deleted,
                Entry = null,
                EventTimestamp = DateTime.UtcNow
            });
        });
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _runTask?.Wait();
    }
}