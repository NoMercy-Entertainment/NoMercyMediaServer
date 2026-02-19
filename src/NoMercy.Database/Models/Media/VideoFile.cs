using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.Media;

[PrimaryKey(nameof(Id))]
[Index(nameof(Filename), IsUnique = true)]
[Index(nameof(EpisodeId))]
[Index(nameof(MovieId))]
[Index(nameof(Folder))]
[Index(nameof(Quality))]
[Index(nameof(Duration))]
[Index(nameof(MovieId), nameof(Folder))]
[Index(nameof(EpisodeId), nameof(Folder))]
public class VideoFile : VideoTracks
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [JsonProperty("duration")] public string? Duration { get; set; }
    [JsonProperty("filename")] public string Filename { get; set; } = string.Empty;
    [JsonProperty("folder")] public string? Folder { get; set; }
    [JsonProperty("host_folder")] public string HostFolder { get; set; } = string.Empty;
    [JsonProperty("languages")] public string Languages { get; set; } = string.Empty;
    [JsonProperty("quality")] public string Quality { get; set; } = string.Empty;
    [JsonProperty("share")] public string Share { get; set; } = string.Empty;
    [JsonProperty("subtitles")] public string? Subtitles { get; set; }
    [JsonProperty("chapters")] public string? Chapters { get; set; }

    [JsonProperty("episode_id")] public int? EpisodeId { get; set; }
    public Episode? Episode { get; set; }

    [JsonProperty("movie_id")] public int? MovieId { get; set; }
    public Movie? Movie { get; set; }

    [JsonProperty("metadata_id")] public Ulid? MetadataId { get; set; }
    public Metadata? Metadata { get; set; }

    [JsonProperty("user_data")] public ICollection<UserData> UserData { get; set; } = [];
}