using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.DTOs.Music;

public record TracksResponseItemDto
{
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("country")] public string? Country { get; set; }
    [JsonProperty("cover")] public Uri? Cover { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("favorite")] public bool Favorite { get; set; }
    [JsonProperty("folder")] public string? Folder { get; set; }
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("library_id")] public Ulid? LibraryId { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("year")] public int? Year { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; } = null!;

    [JsonProperty("artists")] public List<ArtistDto> Artists { get; set; } = [];
    [JsonProperty("albums")] public List<AlbumDto> Albums { get; set; } = [];
    [JsonProperty("tracks")] public List<ArtistTrackDto> Tracks { get; set; } = [];

    public TracksResponseItemDto()
    {
        //
    }

    public TracksResponseItemDto(Track track, string country)
    {
        Id = track.Id;
        Name = track.Name;
        Cover = track.Cover is not null ? new Uri($"/images/music{track.Cover}", UriKind.Relative) : null;
        Link = new($"/music/tracks/{track.Id}", UriKind.Relative);

        ColorPalette = track.ColorPalette;
        Favorite = track.TrackUser.Count != 0;
        Type = "favorites";

        Artists = track.ArtistTrack
            .Select(trackArtist => new ArtistDto(trackArtist, country))
            .ToList();

        Albums = track.AlbumTrack
            .Select(albumTrack => new AlbumDto(albumTrack, country))
            .ToList();

        Tracks = track.ArtistTrack
            .Select(albumTrack => new ArtistTrackDto(albumTrack, country))
            .ToList();
    }
}