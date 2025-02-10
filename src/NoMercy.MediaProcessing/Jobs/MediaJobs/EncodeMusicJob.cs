using System.Collections.Concurrent;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.NmSystem;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

public class EncodeMusicJob : AbstractEncoderJob
{
    public override string QueueName => "encoder";
    public override int Priority => 4;

    public string Status { get; set; } = "pending";
    
    public override async Task Handle()
    {
        await using MediaContext context = new();
        await using QueueContext queueContext = new();
        JobDispatcher jobDispatcher = new();

        await using LibraryRepository libraryRepository = new(context);
        FileRepository fileRepository = new(context);
        FileManager fileManager = new(fileRepository, jobDispatcher);

        Folder? folder = await libraryRepository.GetLibraryFolder(FolderId);
        if (folder is null) return;
        
        EncoderProfile? profile = folder.EncoderProfileFolder
            .Select(e => e.EncoderProfile)
            .FirstOrDefault();
        
        if (profile is null) return;
        
        MediaFolderExtend mediaFolder = new()
        {
            Path = folder.Path,
            Name = Path.GetDirectoryName(folder.Path) ?? string.Empty,
            Created = Directory.GetCreationTime(folder.Path),
            Modified = Directory.GetLastWriteTime(folder.Path),
            Accessed = Directory.GetLastAccessTime(folder.Path),
            Type = "folder"
        };
        
        // get all files in the folder that are not encoded
        await using MediaScan mediaScan = new();
        ConcurrentBag<MediaFolderExtend>? files = await mediaScan
            .DisableRegexFilter()
            .EnableFileListing()
            .Process(folder.Path);

        string rawAlbumName = Path.GetDirectoryName(folder.Path) ?? string.Empty;
        
    }
}