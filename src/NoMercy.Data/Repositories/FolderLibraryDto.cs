using Newtonsoft.Json;
using NoMercy.Database.Models.Libraries;

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