using NoMercy.Database.Models.Libraries;

namespace NoMercy.MediaProcessing.Files;

public class FileChangeGroup(WatcherChangeTypes type, Library library, string folderPath)
{
    public string FolderPath { get; set; } = folderPath;
    public string? FullPath { get; set; }
    public string? OldFullPath { get; set; }
    public Library Library { get; set; } = library;
    public WatcherChangeTypes ChangeType { get; set; } = type;
    public Timer? Timer { get; set; }
}
