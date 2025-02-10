
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(CollectionId), nameof(LibraryId))]
[Index(nameof(CollectionId))]
[Index(nameof(LibraryId))]
public class CollectionLibrary
{
    [JsonProperty("collection_id")] public int CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;

    [JsonProperty("library_id")] public Ulid LibraryId { get; set; }
    public Library Library { get; set; } = null!;

    public CollectionLibrary()
    {
    }

    public CollectionLibrary(int collectionId, Ulid libraryId)
    {
        CollectionId = collectionId;
        LibraryId = libraryId;
    }
}
