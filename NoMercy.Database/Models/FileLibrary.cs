﻿#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models
{
    [PrimaryKey(nameof(FileId), nameof(LibraryId))]
    public class FileLibrary
    {
        [JsonProperty("file_id")] public Ulid FileId { get; set; }
        public virtual File File { get; set; }

        [JsonProperty("library_id")] public Ulid LibraryId { get; set; }
        public virtual Library Library { get; set; }

        public FileLibrary(Ulid fileId, Ulid libraryId)
        {
            FileId = fileId;
            LibraryId = libraryId;
        }
    }
}