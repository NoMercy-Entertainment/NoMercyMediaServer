﻿#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models
{
    [PrimaryKey(nameof(AlbumId), nameof(LibraryId))]
    public class AlbumLibrary
    {
        [JsonProperty("album_id")] public Guid AlbumId { get; set; }
        public virtual Album Album { get; set; }

        [JsonProperty("library_id")] public Ulid LibraryId { get; set; }
        public virtual Library Library { get; set; }

        public AlbumLibrary()
        {
        }

        public AlbumLibrary(Guid albumId, Ulid libraryId)
        {
            AlbumId = albumId;
            LibraryId = libraryId;
        }
    }
}