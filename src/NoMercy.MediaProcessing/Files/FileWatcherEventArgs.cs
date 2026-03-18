using NoMercy.NmSystem.Extensions;

namespace NoMercy.MediaProcessing.Files;

public class FileWatcherEventArgs
{
    // ReSharper disable once MemberCanBePrivate.Global
    public FileSystemEventArgs FileSystemEventArgs { get; private set; }
    public ErrorEventArgs? ErrorEventArgs { get; set; }
    public WatcherChangeTypes ChangeType => FileSystemEventArgs.ChangeType;

    public string Root { get; set; }
    public string Path { get; set; }
    public string FullPath { get; set; }
    public string? OldFullPath { get; set; }
    public FileSystemWatcher? Sender { get; set; }

    public FileWatcherEventArgs(FileSystemWatcher? sender, FileSystemEventArgs fileSystemEventArgs)
    {
        FileSystemEventArgs = fileSystemEventArgs;
        Sender = sender;
        Root = (sender?.Path).OrEmpty();
        Path = System.IO.Path.GetDirectoryName(fileSystemEventArgs.FullPath).OrEmpty();
        FullPath = fileSystemEventArgs.FullPath;
        OldFullPath = (fileSystemEventArgs as RenamedEventArgs)?.OldFullPath;
    }
}