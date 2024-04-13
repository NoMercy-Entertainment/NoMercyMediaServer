﻿#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.


using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models
{
    [PrimaryKey(nameof(Id))]
    [Index(nameof(VideoFileId), nameof(UserId), IsUnique = true)]
    public class UserData : Timestamps
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        [JsonProperty("id")]
        public Ulid Id { get; set; } = Ulid.NewUlid();

        [JsonProperty("name")] public int? Rating { get; set; }

        [JsonProperty("last_played_date")] public string? LastPlayedDate { get; set; }
        [JsonProperty("audio")] public string? Audio { get; set; }
        [JsonProperty("subtitle")] public string? Subtitle { get; set; }
        [JsonProperty("subtitle_type")] public string? SubtitleType { get; set; }
        [JsonProperty("time")] public int? Time { get; set; }
        [JsonProperty("type")] public string Type { get; set; }

        [JsonProperty("user_id")] public Guid UserId { get; set; }
        public virtual User User { get; set; }

        [JsonProperty("movie_id")] public int? MovieId { get; set; }
        public virtual Movie? Movie { get; set; }

        [JsonProperty("episode_id")] public int? TvId { get; set; }
        public virtual Tv? Tv { get; set; }
        
        [JsonProperty("collection_id")] public int? CollectionId { get; set; }
        public virtual Collection? Collection { get; set; }

        [JsonProperty("special_id")] public Ulid? SpecialId { get; set; }
        public virtual Special? Special { get; set; }

        [JsonProperty("video_file_id")] public Ulid? VideoFileId { get; set; }
        public virtual VideoFile VideoFile { get; set; }
    }
}