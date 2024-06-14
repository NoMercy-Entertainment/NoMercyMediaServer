﻿#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(CollectionId), nameof(LibraryId))]
public class CollectionLibrary
{
    [JsonProperty("collection_id")] public int CollectionId { get; set; }
    public Collection Collection { get; set; }

    [JsonProperty("library_id")] public Ulid LibraryId { get; set; }
    public Library Library { get; set; }

    public CollectionLibrary()
    {
    }

    public CollectionLibrary(int collectionId, Ulid libraryId)
    {
        CollectionId = collectionId;
        LibraryId = libraryId;
    }
}