
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(FolderId), nameof(LibraryId))]
[Index(nameof(FolderId))]
[Index(nameof(LibraryId))]
public class FolderLibrary
{
    [JsonProperty("folder_id")] public Ulid FolderId { get; set; }
    public Folder Folder { get; set; } = null!;

    [JsonProperty("library_id")] public Ulid LibraryId { get; set; }
    public Library Library { get; set; } = null!;

    public FolderLibrary(Ulid folderId, Ulid libraryId)
    {
        FolderId = folderId;
        LibraryId = libraryId;
    }

    public FolderLibrary()
    {
    }
}
