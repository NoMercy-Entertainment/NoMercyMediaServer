using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Music;

namespace NoMercy.Api.DTOs.Music;

public record ArtistsResponseItemDto
{
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("disambiguation")] public string? Disambiguation { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("track_id")] public string? TrackId { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }

    [JsonProperty("tracks")] public int Tracks { get; set; }

    public ArtistsResponseItemDto(Artist artist)
    {
        ColorPalette = artist.ColorPalette;
        Cover = artist.Cover ?? artist.Images
            .FirstOrDefault()?.FilePath;
        Cover = !string.IsNullOrEmpty(Cover) 
            ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() 
            : null;
        Disambiguation = artist.Disambiguation;
        Description = artist.Description;
        Id = artist.Id;
        Name = artist.Name;
        Type = "artist";
        Link = new($"/music/artist/{Id}", UriKind.Relative);

        Tracks = artist.ArtistTrack
            .Select(artistTrack => artistTrack.Track)
            .Count();
    }

    public ArtistsResponseItemDto(Album album)
    {
        ColorPalette = album.ColorPalette;
        Cover = album.Cover ?? album.Images
            .FirstOrDefault()?.FilePath;
        Cover = !string.IsNullOrEmpty(Cover) 
            ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() 
            : null;
        Disambiguation = album.Disambiguation;
        Description = album.Description;
        Id = album.Id;
        Name = album.Name;
        Type = "artist";
        Link = new($"/music/artist/{Id}", UriKind.Relative);

        Tracks = album.AlbumTrack
            .Select(albumTrack => albumTrack.Track)
            .Count();
    }
}