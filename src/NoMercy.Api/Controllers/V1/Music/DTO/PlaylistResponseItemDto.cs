using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.V1.Music.DTO;
public record PlaylistResponseItemDto
{
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("created_at")] public DateTime CreatedAt { get; set; }
    [JsonProperty("updated_at")] public DateTime UpdatedAt { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("tracks")] public ICollection<PlaylistTrack> Tracks { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }

    public PlaylistResponseItemDto(Playlist playlist)
    {
        Id = playlist.Id;
        Name = playlist.Name;
        Description = playlist.Description;
        Cover = playlist.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        ColorPalette = playlist.ColorPalette;
        CreatedAt = playlist.CreatedAt;
        UpdatedAt = playlist.UpdatedAt;
        Tracks = playlist.Tracks;
        Type = "playlists";
        Link = new($"/music/playlists/{Id}", UriKind.Relative);
    }
}
