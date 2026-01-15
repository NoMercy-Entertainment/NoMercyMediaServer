using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
public class Playlist : ColorPaletteTimeStamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public required Guid Id { get; set; } = Guid.NewGuid();

    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("filename")] public string? Filename { get; set; }
    [JsonProperty("duration")] public string? Duration { get; set; }

    [JsonProperty("user_id")] public required Guid UserId { get; set; }
    public required User User { get; set; }

    [JsonProperty("playlist_track")] public ICollection<PlaylistTrack> Tracks { get; set; } = [];
}