using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace NoMercy.MediaProcessing.Files;

public class FolderWatcher : IDisposable
{
    private static readonly List<FileSystemWatcher> Watchers = [];
    private static readonly List<IDisposable> PollingWatchers = [];
    private static FolderWatcher? _instance;

    public event Action<FileWatcherEventArgs>? OnChanged;
    public event Action<FileWatcherEventArgs>? OnCreated;
    public event Action<FileWatcherEventArgs>? OnRenamed;
    public event Action<FileWatcherEventArgs>? OnDeleted;
    public event Action<FileWatcherEventArgs>? OnError;

    public bool IncludeSubdirectories = true;

    private static readonly TimeSpan DefaultPollingInterval = TimeSpan.FromSeconds(5);

    public List<Action> Watch(List<string> paths)
    {
        _instance ??= this;
        return WatchFolders(paths);
    }

    private List<Action> WatchFolders(List<string> foldersToWatch)
    {
        List<Action> disposers = [];
        foreach (string folder in foldersToWatch)
            if (Directory.Exists(folder))
                disposers.Add(CreateWatcher(folder));

        return disposers;
    }

    public Action CreateWatcher(string folder)
    {
        folder = Path.GetFullPath(folder);
        if (IsNetworkPath(folder))
        {
            Logger.System($"Polling network folder: {folder}");
            PollingFolderWatcher pollingWatcher = new (folder, this, DefaultPollingInterval);
            PollingWatchers.Add(pollingWatcher);
            return () => { pollingWatcher.Dispose(); };
        }
        else
        {
            FileSystemWatcher watcher = new();
            watcher.Path = folder;
            watcher.EnableRaisingEvents = true;
            watcher.IncludeSubdirectories = IncludeSubdirectories;
            watcher.NotifyFilter =
                // NotifyFilters.Attributes |
                // NotifyFilters.CreationTime |
                NotifyFilters.DirectoryName |
                NotifyFilters.FileName |
                // NotifyFilters.LastAccess |
                NotifyFilters.LastWrite
                // NotifyFilters.Security |
                // NotifyFilters.Size
                ;
            watcher.InternalBufferSize = 64 * 1024;

            watcher.Filter = "*.*";
            watcher.Changed -= _onFileChanged;
            watcher.Created -= _onFileCreated;
            watcher.Deleted -= _onFileDeleted;
            watcher.Renamed -= _onFileRenamed;
            watcher.Error -= _onError;

            watcher.Changed += _onFileChanged;
            watcher.Created += _onFileCreated;
            watcher.Deleted += _onFileDeleted;
            watcher.Renamed += _onFileRenamed;
            watcher.Error += _onError;

            watcher.EnableRaisingEvents = true;

            Watchers.Add(watcher);

            Logger.System($"Watching folder: {folder}");

            return () => { watcher.Dispose(); };
        }
    }

    private static bool IsNetworkPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (path.StartsWith(@"\\")) return true; // UNC path
        string? drive = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(drive)) return false;
        try
        {
            DriveInfo driveInfo = new (drive);
            return driveInfo.DriveType == DriveType.Network;
        }
        catch { return false; }
    }

    private static string _prevChanged = "";

    private static void _onFileChanged(object sender, FileSystemEventArgs e)
    {
        string current = e.FullPath + DateTime.Now.ToString("HHmmssddMMyyyy");

        if (e.ChangeType != WatcherChangeTypes.Changed || _prevChanged == current) return;
        _prevChanged = current;

        _instance?.OnChanged?.Invoke(new(sender as FileSystemWatcher, e));

        Logger.System($"File Changed: {e.FullPath}", LogEventLevel.Verbose);
    }

    private static string _prevCreated = "";

    private static void _onFileCreated(object sender, FileSystemEventArgs e)
    {
        string current = e.FullPath + DateTime.Now.ToString("HHmmssddMMyyyy");

        if (e.ChangeType != WatcherChangeTypes.Created || _prevCreated == current) return;
        _prevCreated = current;

        _instance?.OnCreated?.Invoke(new(sender as FileSystemWatcher, e));

        Logger.System($"File Created: {e.FullPath}", LogEventLevel.Verbose);
    }

    private static string _prevDeleted = "";

    private static void _onFileDeleted(object sender, FileSystemEventArgs e)
    {
        string current = e.FullPath + DateTime.Now.ToString("HHmmssddMMyyyy");

        if (e.ChangeType != WatcherChangeTypes.Deleted || _prevDeleted == current) return;
        _prevDeleted = current;

        _instance?.OnDeleted?.Invoke(new(sender as FileSystemWatcher, e));

        Logger.System($"File Deleted: {e.FullPath}", LogEventLevel.Verbose);
    }

    private static string _prevRenamed = "";

    private static void _onFileRenamed(object sender, RenamedEventArgs e)
    {
        string current = e.FullPath + DateTime.Now.ToString("HHmmssddMMyyyy");

        if (e.ChangeType != WatcherChangeTypes.Renamed || _prevRenamed == current) return;
        _prevRenamed = current;

        _instance?.OnRenamed?.Invoke(new(sender as FileSystemWatcher, e));

        Logger.System($"File Renamed from {e.OldFullPath} to {e.FullPath}", LogEventLevel.Verbose);
    }

    private static void _onError(object sender, ErrorEventArgs e)
    {
        FileWatcherEventArgs fileWatcherEventArgs = new(sender as FileSystemWatcher,
            new(WatcherChangeTypes.All, "", ""));

        fileWatcherEventArgs.ErrorEventArgs = e;

        _instance?.OnError?.Invoke(fileWatcherEventArgs);

        Logger.System($"FolderWatcher error:  {e.GetException().Message}", LogEventLevel.Error);
    }

    // Polling-based watcher for network folders
    private class PollingFolderWatcher : IDisposable
    {
        private readonly string _root;
        private readonly FolderWatcher _parent;
        private readonly TimeSpan _interval;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _thread;
        private readonly ConcurrentDictionary<string, FileMetadata> _knownFiles = new();

        public PollingFolderWatcher(string root, FolderWatcher parent, TimeSpan interval)
        {
            _root = root;
            _parent = parent;
            _interval = interval;
            ScanAllFiles();
            _thread = Task.Run(PollLoop);
        }

        private async Task PollLoop()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    ScanAllFiles();
                    await Task.Delay(_interval);
                }
            }
            catch (Exception ex)
            {
                FileWatcherEventArgs args = new (null, new FileSystemEventArgs(WatcherChangeTypes.All, _root, ""))
                {
                    ErrorEventArgs = new ErrorEventArgs(ex)
                };
                _parent.OnError?.Invoke(args);
            }
        }

        private void ScanAllFiles()
        {
            ConcurrentDictionary<string, FileMetadata> currentFiles = new (StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
                {
                    if (file.EndsWith(".ts") || file.EndsWith(".part"))
                        continue; // Skip temporary files
                    try
                    {
                        FileInfo info = new (file);
                        currentFiles[file] = new (info.LastWriteTimeUtc, info.Length);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, LogEventLevel.Error);
                    }
                    
                }
            }
            catch (Exception ex)
            {
                FileWatcherEventArgs args = new (null, new (WatcherChangeTypes.All, _root, ""))
                {
                    ErrorEventArgs = new (ex)
                };
                _parent.OnError?.Invoke(args);
                return;
            }
            
            foreach (KeyValuePair<string, FileMetadata> kvp in currentFiles)
            {
                if (!_knownFiles.TryGetValue(kvp.Key, out FileMetadata? oldMeta))
                {
                    // Created
                    _parent.OnCreated?.Invoke(new (null, new (WatcherChangeTypes.Created, Path.GetDirectoryName(kvp.Key)!, Path.GetFileName(kvp.Key))));
                }
                else if (!oldMeta.Equals(kvp.Value))
                {
                    // Changed
                    _parent.OnChanged?.Invoke(new (null, new (WatcherChangeTypes.Changed, Path.GetDirectoryName(kvp.Key)!, Path.GetFileName(kvp.Key))));
                }
            }
            
            foreach (KeyValuePair<string, FileMetadata> kvp in _knownFiles)
            {
                if (!currentFiles.ContainsKey(kvp.Key))
                {
                    _parent.OnDeleted?.Invoke(new (null, new (WatcherChangeTypes.Deleted, Path.GetDirectoryName(kvp.Key)!, Path.GetFileName(kvp.Key))));
                }
            }
            // TODO: Renames are not directly detectable via polling
            _knownFiles.Clear();
            foreach (KeyValuePair<string, FileMetadata> kvp in currentFiles)
                _knownFiles[kvp.Key] = kvp.Value;
        }

        public void Dispose()
        {
            _thread.Dispose();
            _cts.Cancel();
        }

        private record FileMetadata(DateTime LastWrite, long Length);
    }

    public void Dispose()
    {
        foreach (FileSystemWatcher watcher in Watchers) watcher.Dispose();
        foreach (var polling in PollingWatchers) polling.Dispose();
    }
}