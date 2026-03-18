using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Music;

namespace NoMercy.Api.DTOs.Music;

public record GenreTrackDto
{
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("path")] public string Path { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("date")] public DateTime? Date { get; set; }
    [JsonProperty("disc")] public int? Disc { get; set; }
    [JsonProperty("duration")] public string? Duration { get; set; }
    [JsonProperty("favorite")] public bool Favorite { get; set; }
    [JsonProperty("quality")] public int? Quality { get; set; }
    [JsonProperty("track")] public int? Track { get; set; }
    [JsonProperty("lyrics")] public Lyric[]? Lyrics { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("artist_track")] public IEnumerable<ArtistDto> Artists { get; set; } = [];
    [JsonProperty("album_track")] public IEnumerable<AlbumDto> Albums { get; set; } = [];

    public GenreTrackDto(MusicGenreTrack genreTrack, string country)
    {
        Id = genreTrack.Track.Id;
        Name = genreTrack.Track.Name;
        Cover = genreTrack.Track.Cover is not null
            ? new Uri($"/images/music{genreTrack.Track.Cover}", UriKind.Relative).ToString()
            : null;
        Path = new Uri($"/{genreTrack.Track.FolderId}{genreTrack.Track.Folder}{genreTrack.Track.Filename}",
            UriKind.Relative).ToString();
        Type = "track";
        ColorPalette = genreTrack.Track.ColorPalette;
        Date = genreTrack.Track.Date;
        Disc = genreTrack.Track.DiscNumber;
        Duration = genreTrack.Track.Duration;
        Favorite = genreTrack.Track.TrackUser.Count != 0;
        Quality = genreTrack.Track.Quality;
        Track = genreTrack.Track.TrackNumber;
        Lyrics = genreTrack.Track.Lyrics;
        Link = new($"/music/tracks/{Id}", UriKind.Relative);

        Artists = genreTrack.Track.ArtistTrack
            .Select(artistTrack => new ArtistDto(artistTrack, country));

        Albums = genreTrack.Track.AlbumTrack
            .Select(album => new AlbumDto(album.Album, country!))
            .GroupBy(album => album.Id)
            .Select(album => album.First())
            .OrderBy(artistTrack => artistTrack.Year);
    }
}