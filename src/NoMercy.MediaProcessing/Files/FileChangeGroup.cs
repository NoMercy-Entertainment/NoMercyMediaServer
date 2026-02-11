using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;

namespace NoMercy.MediaProcessing.Files;

public class FileChangeGroup(WatcherChangeTypes type, Library library, string folderPath)
{
    public string FolderPath { get; set; } = folderPath;
    public Library Library { get; set; } = library;
    public WatcherChangeTypes ChangeType { get; set; } = type;
    public Timer? Timer { get; set; }
}