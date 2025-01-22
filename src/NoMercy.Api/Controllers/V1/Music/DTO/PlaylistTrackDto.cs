using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.V1.Music.DTO;
public record PlaylistTrackDto
{
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("path")] public string Path { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("date")] public DateTime? Date { get; set; }
    [JsonProperty("disc")] public int? Disc { get; set; }
    [JsonProperty("track")] public int? Track { get; set; }
    [JsonProperty("duration")] public string? Duration { get; set; }
    [JsonProperty("favorite")] public bool Favorite { get; set; }
    [JsonProperty("quality")] public int? Quality { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("lyrics")] public Lyric[]? Lyrics { get; set; }

    [JsonProperty("album_track")] public IEnumerable<AlbumDto> Album { get; set; }
    [JsonProperty("artist_track")] public IEnumerable<ArtistDto> Artist { get; set; }

    public PlaylistTrackDto(ArtistTrack artistTrack, string country)
    {
        Id = artistTrack.Track.Id;
        Name = artistTrack.Track.Name;
        Cover = artistTrack.Track.AlbumTrack.FirstOrDefault()?.Album.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Path = new Uri($"/{artistTrack.Track.FolderId}{artistTrack.Track.Folder}{artistTrack.Track.Filename}", UriKind.Relative).ToString();
        Link = new($"/music/artist/{artistTrack.ArtistId}", UriKind.Relative);
        
        ColorPalette = artistTrack.Track.AlbumTrack.FirstOrDefault()?.Album.ColorPalette;
        Date = artistTrack.Track.Date;
        Disc = artistTrack.Track.DiscNumber;
        Track = artistTrack.Track.TrackNumber;
        Duration = artistTrack.Track.Duration;
        Favorite = artistTrack.Track.TrackUser.Any();
        Quality = artistTrack.Track.Quality;
        Lyrics = artistTrack.Track.Lyrics;
        Type = "tracks";

        Album = artistTrack.Track.AlbumTrack
            .DistinctBy(trackAlbum => trackAlbum.AlbumId)
            .Select(albumTrack => new AlbumDto(albumTrack, country));

        using MediaContext mediaContext = new();
        List<ArtistTrack> artists = mediaContext.ArtistTrack
            .Where(at => at.TrackId == artistTrack.TrackId)
            .Include(at => at.Artist)
            .ToList();

        Artist = artists
            .Select(albumTrack => new ArtistDto(albumTrack, country));
    }

    public PlaylistTrackDto(PlaylistTrack trackTrack, string country)
    {
        Id = trackTrack.Track.Id;
        Name = trackTrack.Track.Name;
        Cover = trackTrack.Track.AlbumTrack.FirstOrDefault()?.Album.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Path = new Uri($"/{trackTrack.Track.FolderId}{trackTrack.Track.Folder}{trackTrack.Track.Filename}", UriKind.Relative).ToString();
        ColorPalette = trackTrack.Track.AlbumTrack.FirstOrDefault()?.Album.ColorPalette;
        Date = trackTrack.Track.Date;
        Disc = trackTrack.Track.DiscNumber;
        Track = trackTrack.Track.TrackNumber;
        Duration = trackTrack.Track.Duration;
        Favorite = trackTrack.Track.TrackUser.Any();
        Quality = trackTrack.Track.Quality;
        Lyrics = trackTrack.Track.Lyrics;
        Type = "tracks";

        Album = trackTrack.Track.AlbumTrack
            .DistinctBy(trackAlbum => trackAlbum.AlbumId)
            .Select(albumTrack => new AlbumDto(albumTrack, country));

        using MediaContext mediaContext = new();
        List<ArtistTrack> artists = mediaContext.ArtistTrack
            .Where(at => at.TrackId == trackTrack.TrackId)
            .Include(at => at.Artist)
            .ToList();

        Artist = artists
            .Select(albumTrack => new ArtistDto(albumTrack, country));
    }
}
