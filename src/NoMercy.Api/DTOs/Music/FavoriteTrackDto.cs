using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Music;

public class FavoriteTrackDto
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

    [JsonProperty("album_track")] public IEnumerable<AlbumDto> Albums { get; set; }
    [JsonProperty("artist_track")] public IEnumerable<ArtistDto> Artists { get; set; }

    public FavoriteTrackDto(ArtistTrack artistTrack, string country)
    {
        Id = artistTrack.Track.Id;
        Name = artistTrack.Track.Name;
        Cover = artistTrack.Track.Cover is not null
            ? new Uri($"/images/music{artistTrack.Track.Cover}", UriKind.Relative).ToString()
            : null;
        Link = new($"/music/tracks/{Id}", UriKind.Relative);
        Type = "track";
        ColorPalette = artistTrack.Track.ColorPalette;
        Year = artistTrack.Track.Date.ParseYear();

        Albums = artistTrack.Track.AlbumTrack
            .Select(albumTrack => new AlbumDto(albumTrack, country));
        Artists = artistTrack.Track.ArtistTrack
            .Select(albumTrack => new ArtistDto(albumTrack, country));
    }
}