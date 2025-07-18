using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.V1.Music.DTO;

public record AlbumsResponseItemDto
{
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("disambiguation")] public string? Disambiguation { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("folder")] public string? Folder { get; set; }
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("track_id")] public string? TrackId { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }

    [JsonProperty("tracks")] public int Tracks { get; set; }

    public AlbumsResponseItemDto(Album album, string? country = "US")
    {
        string? description = album.Translations
            .FirstOrDefault(translation => translation.Iso31661 == country)?
            .Description;
        Image? img = album.Images.FirstOrDefault(image => image.Type == "background");

        Description = !string.IsNullOrEmpty(description)
            ? description
            : album.Description;

        Backdrop = img?.FilePath is not null
            ? new Uri($"/images/music{img.FilePath}", UriKind.Relative).ToString()
            : null;
        Cover = album.Cover is not null ? new Uri($"/images/music{album.Cover}", UriKind.Relative).ToString() : null;
        ColorPalette = album.ColorPalette;
        if (ColorPalette is not null) ColorPalette.Backdrop = img?.ColorPalette?.Image;
        Disambiguation = album.Disambiguation;
        Folder = album.Folder;
        Id = album.Id;
        Name = album.Name;
        Type = "albums";
        Link = new($"/music/album/{Id}", UriKind.Relative);

        Tracks = album.AlbumTrack
            .Select(albumTrack => albumTrack.Track)
            .Count(albumTrack => albumTrack.Duration != null);
    }
}