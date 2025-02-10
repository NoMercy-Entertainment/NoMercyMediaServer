using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.V1.Music.DTO;
public record ArtistDto
{
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("disambiguation")] public string? Disambiguation { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }
    [JsonProperty("type")] public string Type { get; set; }

    public ArtistDto(AlbumArtist albumArtist, string country)
    {
        string? description = albumArtist.Artist.Translations
            .FirstOrDefault(translation => translation.Iso31661 == country)?
            .Description;

        Id = albumArtist.Artist.Id;
        Name = albumArtist.Artist.Name;
        Disambiguation = albumArtist.Artist.Disambiguation;
        Cover = albumArtist.Artist.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Link = new($"/music/artist/{Id}", UriKind.Relative);
        Type = "artists";
        Description = !string.IsNullOrEmpty(description)
            ? description
            : albumArtist.Album.Description;

        ColorPalette = albumArtist.Artist.ColorPalette;
    }

    public ArtistDto(ArtistTrack artistTrack, string country)
    {
        Id = artistTrack.Artist.Id;
        Name = artistTrack.Artist.Name;
        Disambiguation = artistTrack.Artist.Disambiguation;
        Cover = artistTrack.Artist.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Link = new($"/music/artist/{Id}", UriKind.Relative);
        Description = artistTrack.Artist.Description;
        Type = "artists";
        Disambiguation = artistTrack.Artist.Disambiguation;
        ColorPalette = artistTrack.Artist.ColorPalette;
    }
}
