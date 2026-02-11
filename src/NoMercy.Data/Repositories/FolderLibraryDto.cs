using Newtonsoft.Json;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;

namespace NoMercy.Data.Repositories;

public class FolderLibraryDto
{
    [JsonProperty("folder_id")] public Ulid FolderId { get; set; }
    [JsonProperty("library_id")] public Ulid LibraryId { get; set; }
    [JsonProperty("folder")] public FolderDto Folder { get; set; } = new();

    public FolderLibraryDto()
    {
        
    }
    
    public FolderLibraryDto(ICollection<FolderLibrary> folderFolderLibraries)
    {
        if (folderFolderLibraries.Count == 0)
            throw new ArgumentException("The collection must contain at least one FolderLibrary.", nameof(folderFolderLibraries));

        FolderId = folderFolderLibraries.First().FolderId;
        LibraryId = folderFolderLibraries.First().LibraryId;
        Folder = new(folderFolderLibraries.First().Folder);
    }

}