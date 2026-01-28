using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.V1.Music.DTO;

public record PlaylistTrackDto
{
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("path")] public string Path { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("date")] public DateTime? Date { get; set; }
    [JsonProperty("disc")] public int? Disc { get; set; }
    [JsonProperty("track")] public int? Track { get; set; }
    [JsonProperty("duration")] public string Duration { get; set; }
    [JsonProperty("favorite")] public bool Favorite { get; set; }
    [JsonProperty("quality")] public int? Quality { get; set; }
    [JsonProperty("type")] public string Type { get; set; }

    [JsonProperty("album_name")] public string? AlbumName { get; set; }
    // [JsonProperty("lyrics")] public Lyric[]? Lyrics { get; set; }

    [JsonProperty("album_track")] public List<AlbumDto> Album { get; set; }
    [JsonProperty("artist_track")] public List<ArtistDto> Artist { get; set; }

    public PlaylistTrackDto(ArtistTrack artistTrack, string country)
    {
        Image? img = artistTrack.Artist.Images.FirstOrDefault(image => image.Type == "background");
        Id = artistTrack.Track.Id;
        Name = artistTrack.Track.Name;
        Backdrop = img?.FilePath is not null
            ? new Uri($"/images/music{img?.FilePath}", UriKind.Relative).ToString()
            : null;
        Cover = artistTrack.Track.AlbumTrack.FirstOrDefault()?.Album.Cover ?? artistTrack.Track.Cover;
        Cover = Cover is not null
            ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString()
            : null;
        Path = new Uri($"/{artistTrack.Track.FolderId}{artistTrack.Track.Folder}{artistTrack.Track.Filename}",
            UriKind.Relative).ToString();
        Link = new($"/music/tracks/{artistTrack.Track.Id}", UriKind.Relative);

        ColorPalette = artistTrack.Track.AlbumTrack.FirstOrDefault()?.Album.ColorPalette;
        if (ColorPalette is not null) ColorPalette.Backdrop = img?.ColorPalette?.Image;
        Date = artistTrack.Track.Date;
        Disc = artistTrack.Track.DiscNumber;
        Track = artistTrack.Track.TrackNumber;
        Duration = artistTrack.Track.Duration;
        Favorite = artistTrack.Track.TrackUser.Count != 0;
        Quality = artistTrack.Track.Quality;
        // Lyrics = artistTrack.Track.Lyrics;
        Type = "track";
        AlbumName = artistTrack.Track.AlbumTrack?.FirstOrDefault()?.Album.Name;

        Album = artistTrack.Track.AlbumTrack!
            .DistinctBy(trackAlbum => trackAlbum.AlbumId)
            .Select(albumTrack => new AlbumDto(albumTrack, country))
            .ToList();

        Artist = artistTrack.Track.ArtistTrack
            .Where(at => at.TrackId == artistTrack.TrackId)
            .Select(at => new ArtistDto(at, country))
            .ToList();
    }

    public PlaylistTrackDto(PlaylistTrack trackTrack, string country)
    {
        Image? img = trackTrack.Track.AlbumTrack.FirstOrDefault()?.Album.AlbumArtist.FirstOrDefault()?.Artist.Images
            .FirstOrDefault(image => image.Type == "background");
        Id = trackTrack.Track.Id;
        Name = trackTrack.Track.Name;
        Backdrop = img?.FilePath is not null
            ? new Uri($"/images/music{img?.FilePath}", UriKind.Relative).ToString()
            : null;
        Cover = trackTrack.Track.AlbumTrack.FirstOrDefault()?.Album.Cover ?? trackTrack.Track.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Path = new Uri($"/{trackTrack.Track.FolderId}{trackTrack.Track.Folder}{trackTrack.Track.Filename}",
            UriKind.Relative).ToString();
        Link = new($"/music/tracks/{trackTrack.Track.Id}", UriKind.Relative);
        ColorPalette = trackTrack.Track.AlbumTrack.FirstOrDefault()?.Album.ColorPalette;
        if (ColorPalette is not null) ColorPalette.Backdrop = img?.ColorPalette?.Image;
        Date = trackTrack.Track.Date;
        Disc = trackTrack.Track.DiscNumber;
        Track = trackTrack.Track.TrackNumber;
        Duration = trackTrack.Track.Duration;
        Favorite = trackTrack.Track.TrackUser.Count != 0;
        Quality = trackTrack.Track.Quality;
        // Lyrics = trackTrack.Track.Lyrics;
        Type = "track";
        AlbumName = trackTrack.Track.AlbumTrack.FirstOrDefault()?.Album.Name;

        Album = trackTrack.Track.AlbumTrack
            .DistinctBy(trackAlbum => trackAlbum.AlbumId)
            .Select(albumTrack => new AlbumDto(albumTrack, country))
            .ToList();

        Artist = trackTrack.Track.ArtistTrack
            .Select(albumTrack => new ArtistDto(albumTrack, country))
            .ToList();
    }

    public PlaylistTrackDto(AlbumTrack artistTrack, string country)
    {
        Image? img = artistTrack.Track.AlbumTrack.FirstOrDefault()?.Album.Images
            .FirstOrDefault(image => image.Type == "background");
        Id = artistTrack.Track.Id;
        Name = artistTrack.Track.Name;
        Backdrop = img?.FilePath is not null
            ? new Uri($"/images/music{img?.FilePath}", UriKind.Relative).ToString()
            : null;
        Cover = artistTrack.Track.AlbumTrack.FirstOrDefault()?.Album.Cover ?? artistTrack.Track.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Path = new Uri($"/{artistTrack.Track.FolderId}{artistTrack.Track.Folder}{artistTrack.Track.Filename}",
            UriKind.Relative).ToString();
        Link = new($"/music/tracks/{artistTrack.Track.Id}", UriKind.Relative);

        ColorPalette = artistTrack.Track.AlbumTrack.FirstOrDefault()?.Album.ColorPalette;
        if (ColorPalette is not null) ColorPalette.Backdrop = img?.ColorPalette?.Image;
        Date = artistTrack.Track.Date;
        Disc = artistTrack.Track.DiscNumber;
        Track = artistTrack.Track.TrackNumber;
        Duration = artistTrack.Track.Duration;
        Favorite = artistTrack.Track.TrackUser.Count != 0;
        Quality = artistTrack.Track.Quality;
        // Lyrics = artistTrack.Track.Lyrics;
        Type = "track";
        AlbumName = artistTrack.Track.AlbumTrack.FirstOrDefault()?.Album.Name;

        Album = artistTrack.Track.AlbumTrack
            .DistinctBy(trackAlbum => trackAlbum.AlbumId)
            .Select(albumTrack => new AlbumDto(albumTrack, country))
            .ToList();

        Artist = artistTrack.Track.ArtistTrack
            .Select(albumTrack => new ArtistDto(albumTrack, country))
            .ToList();
    }

    public PlaylistTrackDto(MusicGenreTrack genreTrack, string country)
    {
        Image? img = genreTrack.Track.AlbumTrack.FirstOrDefault()?.Album.AlbumArtist.FirstOrDefault()?.Artist.Images
            .FirstOrDefault(image => image.Type == "background");
        Id = genreTrack.Track.Id;
        Name = genreTrack.Track.Name.ToTitleCase();
        Backdrop = img?.FilePath is not null
            ? new Uri($"/images/music{img?.FilePath}", UriKind.Relative).ToString()
            : null;
        Cover = genreTrack.Track.AlbumTrack.FirstOrDefault()?.Album.Cover ?? genreTrack.Track.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Path = new Uri($"/{genreTrack.Track.FolderId}{genreTrack.Track.Folder}{genreTrack.Track.Filename}",
            UriKind.Relative).ToString();
        Link = new($"/music/tracks/{genreTrack.Track.Id}", UriKind.Relative);
        ColorPalette = genreTrack.Track.AlbumTrack.FirstOrDefault()?.Album.ColorPalette;
        if (ColorPalette is not null) ColorPalette.Backdrop = img?.ColorPalette?.Image;
        Date = genreTrack.Track.Date;
        Disc = genreTrack.Track.DiscNumber;
        Track = genreTrack.Track.TrackNumber;
        Duration = genreTrack.Track.Duration;
        Favorite = genreTrack.Track.TrackUser.Count != 0;
        Quality = genreTrack.Track.Quality;
        // Lyrics = genreTrack.Track.Lyrics;
        Type = "track";
        AlbumName = genreTrack.Track.AlbumTrack.FirstOrDefault()?.Album.Name;

        Album = genreTrack.Track.AlbumTrack
            .DistinctBy(trackAlbum => trackAlbum.AlbumId)
            .Select(albumTrack => new AlbumDto(albumTrack, country))
            .ToList();

        Artist = genreTrack.Track.ArtistTrack
            .Select(artistTrack => new ArtistDto(artistTrack, country))
            .ToList();
    }
}