using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.V1.Music.DTO;
public record AlbumTrackDto
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
    [JsonProperty("artist_track")] public IEnumerable<ArtistDto> Artist { get; set; }
    [JsonProperty("album_track")] public IEnumerable<AlbumDto> Album { get; set; }

    public AlbumTrackDto(AlbumTrack albumTrack, string country)
    {
        Id = albumTrack.Track.Id;
        Name = albumTrack.Track.Name;
        Cover = albumTrack.Album.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Path = new Uri($"/{albumTrack.Track.FolderId}{albumTrack.Track.Folder}{albumTrack.Track.Filename}", UriKind.Relative).ToString();
        Type = "tracks";
        ColorPalette = albumTrack.Album.ColorPalette;
        Date = albumTrack.Track.Date;
        Disc = albumTrack.Track.DiscNumber;
        Duration = albumTrack.Track.Duration;
        Favorite = albumTrack.Track.TrackUser.Count != 0;
        Quality = albumTrack.Track.Quality;
        Track = albumTrack.Track.TrackNumber;
        Lyrics = albumTrack.Track.Lyrics;
        Link = new($"/music/tracks/{Id}", UriKind.Relative);

        using MediaContext mediaContext = new();
        List<ArtistTrack> artists = mediaContext.ArtistTrack
            .Where(at => at.TrackId == albumTrack.TrackId)
            .Include(at => at.Artist)
            .ToList();

        Artist = artists
            .Select(artistTrack => new ArtistDto(artistTrack, country));

        Album = albumTrack.Track.AlbumTrack
            .Select(trackAlbum => new AlbumDto(trackAlbum, country));
    }
}
