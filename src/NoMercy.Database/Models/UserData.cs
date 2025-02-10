
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(VideoFileId), nameof(UserId), IsUnique = true)]
[Index(nameof(UserId))]
[Index(nameof(MovieId))]
[Index(nameof(TvId))]
[Index(nameof(CollectionId))]
[Index(nameof(SpecialId))]
[Index(nameof(VideoFileId))]
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
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;

    [JsonProperty("user_id")] public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    [JsonProperty("movie_id")] public int? MovieId { get; set; }
    public Movie? Movie { get; set; }

    [JsonProperty("episode_id")] public int? TvId { get; set; }
    public Tv? Tv { get; set; }

    [JsonProperty("collection_id")] public int? CollectionId { get; set; }
    public Collection? Collection { get; set; }

    [JsonProperty("special_id")] public Ulid? SpecialId { get; set; }
    public Special? Special { get; set; }

    [JsonProperty("video_file_id")] public Ulid VideoFileId { get; set; }
    public VideoFile VideoFile { get; set; } = null!;
}
