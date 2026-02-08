using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
public class Playlist : ColorPaletteTimeStamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [MaxLength(4096)] [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("filename")] public string? Filename { get; set; }
    [JsonProperty("duration")] public string? Duration { get; set; }

    [JsonProperty("user_id")] public Guid UserId { get; set; }
    [JsonProperty("user")] public User User { get; set; } = null!;

    [JsonProperty("playlist_track")] public ICollection<PlaylistTrack> Tracks { get; set; } = [];
}