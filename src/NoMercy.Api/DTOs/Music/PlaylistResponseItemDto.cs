using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Music;

namespace NoMercy.Api.DTOs.Music;

public record PlaylistResponseItemDto
{
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("country")] public string? Country { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("favorite")] public bool Favorite { get; set; }
    [JsonProperty("library_id")] public Ulid? LibraryId { get; set; }
    [JsonProperty("year")] public int? Year { get; set; }
    [JsonProperty("artists")] public IEnumerable<ArtistDto> Artists { get; set; }
    [JsonProperty("tracks")] public IEnumerable<PlaylistTrackDto> Tracks { get; set; }
    [JsonProperty("type")] public string Type { get; set; }

    public PlaylistResponseItemDto(Playlist playlist, string? country = "US")
    {
        ColorPalette = playlist.ColorPalette;
        Cover = !string.IsNullOrEmpty(playlist.Cover) ?
            new Uri($"/images/music{playlist.Cover}", UriKind.Relative).ToString() 
            : null;
        Description = playlist.Description;
        Id = playlist.Id;
        Name = playlist.Name;
        Link = new($"/music/playlists/{Id}", UriKind.Relative);
        Type = "playlist";
        Artists = [];

        Tracks = playlist.Tracks
            .Select(albumTrack => new PlaylistTrackDto(albumTrack, country!));
    }
}