using NoMercy.NmSystem;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Files;
public class FolderWatcher : IDisposable
{
    private static readonly List<FileSystemWatcher> Watchers = new();
    private static FolderWatcher? _instance;

    public event Action<FileWatcherEventArgs>? OnChanged;
    public event Action<FileWatcherEventArgs>? OnCreated;
    public event Action<FileWatcherEventArgs>? OnRenamed;
    public event Action<FileWatcherEventArgs>? OnDeleted;
    public event Action<FileWatcherEventArgs>? OnError;

    public bool IncludeSubdirectories = true;

    public List<Action> Watch(List<string> paths)
    {
        _instance ??= this;
        return WatchFolders(paths);
    }

    private List<Action> WatchFolders(List<string> foldersToWatch)
    {
        List<Action> disposers = new();
        foreach (string folder in foldersToWatch)
            if (Directory.Exists(folder))
                disposers.Add(CreateWatcher(folder));

        return disposers;
    }

    public Action CreateWatcher(string folder)
    {
        folder = Path.GetFullPath(folder);
        FileSystemWatcher watcher = new();
        watcher.Path = folder;
        watcher.EnableRaisingEvents = false;
        watcher.IncludeSubdirectories = IncludeSubdirectories;
        watcher.NotifyFilter =
            // NotifyFilters.Attributes |
           // NotifyFilters.CreationTime |
           // NotifyFilters.DirectoryName |
           // NotifyFilters.FileName |
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

        return () =>
        {
            watcher.Dispose();
        };
    }

    private static string _prevChanged = "";

    private static void _onFileChanged(object sender, FileSystemEventArgs e)
    {
        string current = e.FullPath + DateTime.Now.ToString("HHmmssddMMyyyy");

        if (e.ChangeType != WatcherChangeTypes.Changed || _prevChanged == current) return;
        _prevChanged = current;

        _instance?.OnChanged?.Invoke(new FileWatcherEventArgs(sender as FileSystemWatcher, e));

        Logger.System($"File Changed: {e.FullPath}", LogEventLevel.Verbose);
    }

    private static string _prevCreated = "";

    private static void _onFileCreated(object sender, FileSystemEventArgs e)
    {
        string current = e.FullPath + DateTime.Now.ToString("HHmmssddMMyyyy");

        if (e.ChangeType != WatcherChangeTypes.Created || _prevCreated == current) return;
        _prevCreated = current;

        _instance?.OnCreated?.Invoke(new FileWatcherEventArgs(sender as FileSystemWatcher, e));

        Logger.System($"File Created: {e.FullPath}", LogEventLevel.Verbose);
    }

    private static string _prevDeleted = "";

    private static void _onFileDeleted(object sender, FileSystemEventArgs e)
    {
        string current = e.FullPath + DateTime.Now.ToString("HHmmssddMMyyyy");

        if (e.ChangeType != WatcherChangeTypes.Deleted || _prevDeleted == current) return;
        _prevDeleted = current;

        _instance?.OnDeleted?.Invoke(new FileWatcherEventArgs(sender as FileSystemWatcher, e));

        Logger.System($"File Deleted: {e.FullPath}", LogEventLevel.Verbose);
    }

    private static string _prevRenamed = "";

    private static void _onFileRenamed(object sender, RenamedEventArgs e)
    {
        string current = e.FullPath + DateTime.Now.ToString("HHmmssddMMyyyy");

        if (e.ChangeType != WatcherChangeTypes.Renamed || _prevRenamed == current) return;
        _prevRenamed = current;

        _instance?.OnRenamed?.Invoke(new FileWatcherEventArgs(sender as FileSystemWatcher, e));

        Logger.System($"File Renamed from {e.OldFullPath} to {e.FullPath}", LogEventLevel.Verbose);
    }

    private static void _onError(object sender, ErrorEventArgs e)
    {
        FileWatcherEventArgs? fileWatcherEventArgs = new(sender as FileSystemWatcher,
            new FileSystemEventArgs(WatcherChangeTypes.All, "", ""));

        fileWatcherEventArgs.ErrorEventArgs = e;

        _instance?.OnError?.Invoke(fileWatcherEventArgs);

        // Logger.System($"FolderWatcher error:  {e.GetException().Message}", LogEventLevel.Error);
    }

    public void Dispose()
    {
        foreach (FileSystemWatcher watcher in Watchers) watcher.Dispose();
    }
}