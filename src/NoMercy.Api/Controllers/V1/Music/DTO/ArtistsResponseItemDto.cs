using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.V1.Music.DTO;
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
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Disambiguation = artist.Disambiguation;
        Description = artist.Description;
        Id = artist.Id;
        Name = artist.Name;
        Type = "artists";
        Link = new($"/music/artist/{Id}", UriKind.Relative);

        Tracks = artist.ArtistTrack
            .Select(artistTrack => artistTrack.Track)
            .Count(artistTrack => artistTrack.Duration != null);
    }
}
