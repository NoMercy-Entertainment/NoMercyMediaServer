using System.Collections.Concurrent;
using Storage.Net;
using Storage.Net.Blobs;

namespace NoMercy.MediaProcessing.Files;

public enum StorageChangeType { Created, Changed, Deleted }

public class StorageWatcherEventArgs : EventArgs
{
    public string FullPath { get; init; } = string.Empty;
    public StorageChangeType ChangeType { get; init; }
    public Blob? Blob { get; init; } // Is null bij 'Deleted'
    public DateTime EventTimestamp { get; init; } = DateTime.UtcNow;
    
    public FileWatcherEventArgs ToFileWatcherEventArgs()
    {
        return new (null, new (
            changeType: ChangeType switch
            {
                StorageChangeType.Created => WatcherChangeTypes.Created,
                StorageChangeType.Changed => WatcherChangeTypes.Changed,
                StorageChangeType.Deleted => WatcherChangeTypes.Deleted,
                _ => throw new ArgumentOutOfRangeException()
            },
            directory: Path.GetDirectoryName(FullPath) ?? string.Empty,
            name: Path.GetFileName(FullPath)
        ));
    }
    public FileSystemEventArgs ToFileSystemEventArgsEventArgs()
    {
        return new (
            ChangeType switch
            {
                StorageChangeType.Created => WatcherChangeTypes.Created,
                StorageChangeType.Changed => WatcherChangeTypes.Changed,
                StorageChangeType.Deleted => WatcherChangeTypes.Deleted,
                _ => throw new ArgumentOutOfRangeException()
            },
            Path.GetDirectoryName(FullPath) ?? string.Empty,
            Path.GetFileName(FullPath));
    }
}

internal class StorageWatcher
{
    private readonly IBlobStorage _storage;
    private readonly string _path;
    private readonly ConcurrentDictionary<string, Blob> _snapshot = new();
    private CancellationTokenSource? _cts;
    private Task? _runTask;

    public event Action<StorageWatcherEventArgs>? Changed;
    public event Action<StorageWatcherEventArgs>? Created;
    public event Action<StorageWatcherEventArgs>? Deleted;

    public StorageWatcher(IBlobStorage storage, string path = "/")
    {
        _storage = storage;
        _path = path;
    }

    public void Start(TimeSpan interval)
    {
        if (_runTask != null) return;
        _cts = new ();
        _runTask = Task.Run(() => WatchLoopAsync(interval, _cts.Token));
    }

    private async Task WatchLoopAsync(TimeSpan interval, CancellationToken ct)
    {
        using PeriodicTimer timer = new (interval);
        await Scan(initial: true);

        while (await timer.WaitForNextTickAsync(ct))
        {
            try { await Scan(initial: false); }
            catch (Exception ex) { Console.WriteLine($"Fout: {ex.Message}"); }
        }
    }

    private async Task Scan(bool initial)
    {
        IReadOnlyCollection<Blob> currentBlobs = await _storage.ListAsync(new ListOptions { FolderPath = _path, Recurse = true });
        ConcurrentBag<string> foundPaths = [];
        
        Parallel.ForEach(currentBlobs.Where(b => b.IsFile), blob =>
        {
            foundPaths.Add(blob.FullPath);

            if (!_snapshot.TryGetValue(blob.FullPath, out Blob? oldBlob))
            {
                _snapshot[blob.FullPath] = blob;
                if (!initial)
                {
                    Created?.Invoke(CreateArgs(blob, StorageChangeType.Created));
                }
            }
            else if (blob.LastModificationTime > oldBlob.LastModificationTime || blob.Size != oldBlob.Size)
            {
                _snapshot[blob.FullPath] = blob;
                if (!initial)
                {
                    Changed?.Invoke(CreateArgs(blob, StorageChangeType.Changed));
                }
            }
        });
        
        List<string> snapshotKeys = _snapshot.Keys.ToList();
        HashSet<string> currentPathsSet = new (foundPaths); 

        Parallel.ForEach(snapshotKeys, path =>
        {
            if (currentPathsSet.Contains(path)) return;
            if (!_snapshot.TryRemove(path, out _)) return;
            if (initial) return;
            Deleted?.Invoke(new()
            {
                FullPath = path,
                ChangeType = StorageChangeType.Deleted,
                Blob = null
            });
        });
    }

    private static StorageWatcherEventArgs CreateArgs(Blob blob, StorageChangeType type) => new()
    {
        FullPath = blob.FullPath,
        ChangeType = type,
        Blob = blob
    };
    
    public void Dispose()
    {
        _cts?.Cancel();
        _runTask?.Wait();
    }
}