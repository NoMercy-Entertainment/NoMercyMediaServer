﻿#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models
{
    [PrimaryKey(nameof(FileId), nameof(MovieId))]
    public class FileMovie
    {
        
        [JsonProperty("file_id")] public Ulid FileId { get; set; }
        public virtual File File { get; set; }
        
        [JsonProperty("movie_id")] public int MovieId { get; set; }
        public virtual Movie Movie { get; set; }
        
        public FileMovie(Ulid fileId, int movieId)
        {
            FileId = fileId;
            MovieId = movieId;
        }
    }
}