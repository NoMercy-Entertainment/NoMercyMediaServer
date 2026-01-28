using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.V1.Music.DTO;

public class ArtistDto
{
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("disambiguation")] public string? Disambiguation { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }
    [JsonProperty("type")] public string Type { get; set; }

    public ArtistDto(AlbumArtist albumArtist, string country)
    {
        string? description = albumArtist.Artist.Translations
            .FirstOrDefault(translation => translation.Iso31661 == country)?
            .Description;

        Image? img = albumArtist.Artist.Images.FirstOrDefault(image => image.Type == "background");

        Id = albumArtist.Artist.Id;
        Name = albumArtist.Artist.Name;
        Description = !string.IsNullOrEmpty(description)
            ? description
            : albumArtist.Artist.Description;
        Disambiguation = albumArtist.Artist.Disambiguation;
        Cover = albumArtist.Artist.Cover is not null
            ? new Uri($"/images/music{albumArtist.Artist.Cover}", UriKind.Relative).ToString()
            : null;
        Backdrop = img?.FilePath is not null
            ? new Uri($"/images/music{img.FilePath}", UriKind.Relative).ToString()
            : null;
        Link = new($"/music/artist/{Id}", UriKind.Relative);
        Type = "artist";
        Description = !string.IsNullOrEmpty(description)
            ? description
            : albumArtist.Album.Description;

        ColorPalette = albumArtist.Artist.ColorPalette;
    }

    public ArtistDto(ArtistTrack artistTrack, string country)
    {
        string? description = artistTrack.Artist.Translations
            .FirstOrDefault(translation => translation.Iso31661 == country)?
            .Description;
        Image? img = artistTrack.Artist.Images.FirstOrDefault(image => image.Type == "background");

        Id = artistTrack.Artist.Id;
        Name = artistTrack.Artist.Name;
        Description = !string.IsNullOrEmpty(description)
            ? description
            : artistTrack.Artist.Description;
        Disambiguation = artistTrack.Artist.Disambiguation;
        Cover = artistTrack.Artist.Cover is not null
            ? new Uri($"/images/music{artistTrack.Artist.Cover}", UriKind.Relative).ToString()
            : null;
        Backdrop = img?.FilePath is not null
            ? new Uri($"/images/music{img.FilePath}", UriKind.Relative).ToString()
            : null;
        Link = new($"/music/artist/{Id}", UriKind.Relative);
        Description = artistTrack.Artist.Description;
        Type = "artist";
        Disambiguation = artistTrack.Artist.Disambiguation;
        ColorPalette = artistTrack.Artist.ColorPalette;
    }
}