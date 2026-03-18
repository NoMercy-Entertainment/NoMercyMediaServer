using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;
using Stowage;

namespace NoMercy.MediaProcessing.Files;

public class FolderWatcher : IDisposable
{
    private static readonly List<IDisposable> Watchers = [];
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
        return !IsNetworkPath(folder) ? StartFileSystemWatcher(folder) : StartNetworkFileWatcher(folder);
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

    private static Action StartNetworkFileWatcher(string folder)
    {
        IFileStorage storage;
        if (folder.StartsWith("s3://", StringComparison.OrdinalIgnoreCase) ||
            folder.StartsWith("gs://", StringComparison.OrdinalIgnoreCase) ||
            folder.StartsWith("az://", StringComparison.OrdinalIgnoreCase))
        {
            storage = Stowage.Files.Of.ConnectionString(folder);
        }
        else
        {
            storage = Stowage.Files.Of.ConnectionString("disk://path="+folder);
        }

        StowageWatcher stowageWatcher = new (storage);
        stowageWatcher.Changed += (e) =>
        {
            _onFileChanged(_instance!, e.ToFileSystemEventArgsEventArgs(folder));
        };
        stowageWatcher.Created += (e) =>
        {
            _onFileCreated(_instance!, e.ToFileSystemEventArgsEventArgs(folder));
        };
        stowageWatcher.Deleted += (e) =>
        {
            _onFileDeleted(_instance!, e.ToFileSystemEventArgsEventArgs(folder));
        };
        stowageWatcher.Watch(TimeSpan.FromMinutes(1));
        
        Watchers.Add(stowageWatcher);
        
        Logger.System($"Watching folder: {folder}");
        
        return () => { stowageWatcher.Dispose(); };
    }

    private static Action StartFileSystemWatcher(string folder)
    {
        FileSystemWatcher fileSystemWatcher = new();
        fileSystemWatcher.Path = folder;
        fileSystemWatcher.EnableRaisingEvents = true;
        fileSystemWatcher.IncludeSubdirectories = true;
        fileSystemWatcher.NotifyFilter =
            // NotifyFilters.Attributes |
            // NotifyFilters.CreationTime |
            NotifyFilters.DirectoryName |
            NotifyFilters.FileName |
            // NotifyFilters.LastAccess |
            NotifyFilters.LastWrite
            // NotifyFilters.Security |
            // NotifyFilters.Size
            ;
        fileSystemWatcher.InternalBufferSize = 64 * 1024;

        fileSystemWatcher.Filter = "*.*";
        fileSystemWatcher.Changed -= _onFileChanged;
        fileSystemWatcher.Created -= _onFileCreated;
        fileSystemWatcher.Deleted -= _onFileDeleted;
        fileSystemWatcher.Renamed -= _onFileRenamed;
        fileSystemWatcher.Error -= _onError;

        fileSystemWatcher.Changed += _onFileChanged;
        fileSystemWatcher.Created += _onFileCreated;
        fileSystemWatcher.Deleted += _onFileDeleted;
        fileSystemWatcher.Renamed += _onFileRenamed;
        fileSystemWatcher.Error += _onError;

        fileSystemWatcher.EnableRaisingEvents = true;

        Watchers.Add(fileSystemWatcher);

        Logger.System($"Watching folder: {folder}");

        return () => { fileSystemWatcher.Dispose(); };
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
        foreach (IDisposable watcher in Watchers) watcher.Dispose();
    }
}