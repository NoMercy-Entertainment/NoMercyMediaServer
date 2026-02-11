using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.DTOs.Music;

public record MusicPlaylistResponseItemDto
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

    public MusicPlaylistResponseItemDto(Playlist playlist)
    {
        Id = playlist.Id;
        Name = playlist.Name;
        Description = playlist.Description;
        Cover = playlist.Cover is not null
            ? new Uri($"/images/music{playlist.Cover}", UriKind.Relative).ToString()
            : null;
        ColorPalette = playlist.ColorPalette;
        CreatedAt = playlist.CreatedAt;
        Tracks = playlist.Tracks;
        Type = "playlist";
        Link = new($"/music/playlists/{Id}", UriKind.Relative);
    }
}