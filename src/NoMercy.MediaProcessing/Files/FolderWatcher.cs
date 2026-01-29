using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;
using Storage.Net;
using Storage.Net.Blobs;

namespace NoMercy.MediaProcessing.Files;

public class FolderWatcher : IDisposable
{
    private static readonly List<FileSystemWatcher> Watchers = [];
    private static FolderWatcher? _instance;

    public event Action<FileWatcherEventArgs>? OnChanged;
    public event Action<FileWatcherEventArgs>? OnCreated;
    public event Action<FileWatcherEventArgs>? OnRenamed;
    public event Action<FileWatcherEventArgs>? OnDeleted;
    public event Action<FileWatcherEventArgs>? OnError;

    public List<Action> Watch(List<string> paths)
    {
        _instance ??= this;
        return WatchFolders(paths);
    }

    private List<Action> WatchFolders(List<string> foldersToWatch)
    {
        List<Action> disposers = [];
        disposers.AddRange(from folder in foldersToWatch where Directory.Exists(folder) select CreateWatcher(folder));

        return disposers;
    }

    private static Action CreateWatcher(string folder)
    {
        folder = Path.GetFullPath(folder);
        if (IsNetworkPath(folder))
        {
            IBlobStorage? storage = StorageFactory.Blobs.DirectoryFiles(folder);
            if (storage == null)
            {
                Logger.System($"Failed to create Storage for network folder: {folder}", LogEventLevel.Error);
                return () => { };
            }
            StorageWatcher watcher = new (storage);
            watcher.Changed += (e) =>
            {
                if (_instance == null) return;
                _onFileChanged(_instance, e.ToFileSystemEventArgsEventArgs());
            };
            watcher.Created += (e) =>
            {
                if (_instance == null) return;
                _onFileCreated(_instance, e.ToFileSystemEventArgsEventArgs());
            };
            watcher.Deleted += (e) =>
            {
                if (_instance == null) return;
                _onFileDeleted(_instance, e.ToFileSystemEventArgsEventArgs());
            };
            Logger.System($"Polling network folder: {folder}");
            watcher.Start(TimeSpan.FromSeconds(10));
            return () => { watcher.Dispose(); };
        }
        else
        {
            FileSystemWatcher watcher = new();
            watcher.Path = folder;
            watcher.EnableRaisingEvents = true;
            watcher.IncludeSubdirectories = true;
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

    public void Dispose()
    {
        foreach (FileSystemWatcher watcher in Watchers) watcher.Dispose();
    }
}