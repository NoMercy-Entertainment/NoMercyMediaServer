using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.DTOs.Music;

public class FeaturedDto
{
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("disambiguation")] public string? Disambiguation { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("tracks")] public int Tracks { get; set; }
    [JsonProperty("year")] public int? Year { get; set; }
    [JsonProperty("album_artist")] public Guid? AlbumArtist { get; set; }
    [JsonProperty("type")] public string Type { get; set; }

    public FeaturedDto(AlbumArtist albumArtist, string country)
    {
        string? description = albumArtist.Album.Translations
            .FirstOrDefault(translation => translation.Iso31661 == country)?
            .Description;

        Id = albumArtist.Album.Id;
        Name = albumArtist.Album.Name;
        Cover = albumArtist.Album.Cover is not null
            ? new Uri($"/images/music{albumArtist.Album.Cover}", UriKind.Relative).ToString()
            : null;
        Disambiguation = albumArtist.Album.Disambiguation;
        Link = new($"/music/album/{Id}", UriKind.Relative);
        Description = !string.IsNullOrEmpty(description)
            ? description
            : albumArtist.Album.Description;
        Type = "album";
        ColorPalette = albumArtist.Album.ColorPalette;
        Tracks = albumArtist.Album.AlbumTrack.Count;
        Year = albumArtist.Album.Year;

        AlbumArtist = albumArtist.ArtistId;
    }

    public FeaturedDto(Album album, string country)
    {
        string? description = album.Translations
            .FirstOrDefault(translation => translation.Iso31661 == country)?
            .Description;

        Id = album.Id;
        Name = album.Name;
        Disambiguation = album.Disambiguation;
        Cover = album.Cover is not null ? new Uri($"/images/music{album.Cover}", UriKind.Relative).ToString() : null;
        Link = new($"/music/artist/{Id}", UriKind.Relative);
        Type = "artist";
        Description = !string.IsNullOrEmpty(description)
            ? description
            : album.Description;

        ColorPalette = album.ColorPalette;
    }
}