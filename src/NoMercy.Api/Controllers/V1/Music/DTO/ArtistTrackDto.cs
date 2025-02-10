using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.V1.Music.DTO;
public record ArtistTrackDto
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
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("lyrics")] public Lyric[]? Lyrics { get; set; }
    [JsonProperty("album_id")] public Guid AlbumId { get; set; }
    [JsonProperty("album_name")] public string AlbumName { get; set; }

    [JsonProperty("album_track")] public IEnumerable<AlbumDto> Album { get; set; }
    [JsonProperty("artist_track")] public IEnumerable<ArtistDto> Artist { get; set; }

    public ArtistTrackDto(ArtistTrack artistTrack, string country)
    {
        Id = artistTrack.Track.Id;
        Name = artistTrack.Track.Name;
        Cover = artistTrack.Track.AlbumTrack.FirstOrDefault()?.Album.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Link = new($"/music/tracks/{artistTrack.Track.Id}", UriKind.Relative);
        Path = new Uri($"/{artistTrack.Track.FolderId}{artistTrack.Track.Folder}{artistTrack.Track.Filename}", UriKind.Relative).ToString();
        Type = "tracks";
        ColorPalette = artistTrack.Track.AlbumTrack.FirstOrDefault()?.Album.ColorPalette;
        Date = artistTrack.Track.Date;
        Disc = artistTrack.Track.DiscNumber;
        Track = artistTrack.Track.TrackNumber;
        Duration = artistTrack.Track.Duration;
        AlbumId = artistTrack.Track.AlbumTrack.First().AlbumId;
        AlbumName = artistTrack.Track.AlbumTrack.First().Album.Name;
        Favorite = artistTrack.Track.TrackUser.Any();
        Quality = artistTrack.Track.Quality;
        Lyrics = artistTrack.Track.Lyrics;

        Album = artistTrack.Track.AlbumTrack
            .DistinctBy(trackAlbum => trackAlbum.AlbumId)
            .Select(albumTrack => new AlbumDto(albumTrack, country));

        Artist = artistTrack.Track.ArtistTrack
            .Select(albumTrack => new ArtistDto(albumTrack, country));
    }


    public ArtistTrackDto(Track track, string? country = "US")
    {
        Id = track.Id;
        Name = track.Name;
        ColorPalette = track.AlbumTrack.First().Album.ColorPalette ?? track.ArtistTrack.First().Artist.ColorPalette;
        Cover = track.AlbumTrack.First().Album.Cover ?? track.ArtistTrack.First().Artist.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Path = new Uri($"/{track.FolderId}{track.Folder}{track.Filename}", UriKind.Relative).ToString();
        Type = "tracks";
        Date = track.UpdatedAt;
        Disc = track.DiscNumber;
        Track = track.TrackNumber;
        Duration = track.Duration;
        Favorite = track.TrackUser.Any();
        Quality = track.Quality;
        AlbumName = track.AlbumTrack.First().Album.Name;

        Album = track.AlbumTrack
            .DistinctBy(trackAlbum => trackAlbum.AlbumId)
            .Select(albumTrack => new AlbumDto(albumTrack, country!));

        Artist = track.ArtistTrack
            .DistinctBy(trackArtist => trackArtist.ArtistId)
            .Select(trackArtist => new ArtistDto(trackArtist, country!));
    }
}
