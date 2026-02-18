using Newtonsoft.Json;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Music;

namespace NoMercy.Api.DTOs.Media.Components;

/// <summary>
/// Data for NMTrackRow component - single track in a list.
/// </summary>
public record TrackRowData
{
    [JsonProperty("id")] public string Id { get; set; } = null!;
    [JsonProperty("name")] public string Name { get; set; } = null!;
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("path")] public string Path { get; set; } = null!;
    [JsonProperty("link")] public string Link { get; set; } = null!;
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("date")] public string? Date { get; set; }
    [JsonProperty("disc")] public int? Disc { get; set; }
    [JsonProperty("duration")] public string? Duration { get; set; }
    [JsonProperty("favorite")] public bool Favorite { get; set; }
    [JsonProperty("quality")] public int? Quality { get; set; }
    [JsonProperty("track")] public int? Track { get; set; }
    [JsonProperty("type")] public string Type { get; set; } = null!;
    [JsonProperty("lyrics")] public IEnumerable<LyricLine>? Lyrics { get; set; }
    [JsonProperty("album_id")] public string AlbumId { get; set; } = null!;
    [JsonProperty("album_name")] public string AlbumName { get; set; } = null!;
    [JsonProperty("album_track")] public IEnumerable<TrackArtist> AlbumTrack { get; set; } = [];
    [JsonProperty("artist_track")] public IEnumerable<TrackArtist> ArtistTrack { get; set; } = [];

    public TrackRowData()
    {
    }

    public TrackRowData(Track track, bool isFavorite = false)
    {
        Id = track.Id.ToString();
        Name = track.Name;
        Cover = track.Cover;
        Path = track.Filename ?? string.Empty;
        Link = $"/music/tracks/{track.Id}";
        ColorPalette = track.ColorPalette;
        Date = track.Date?.ToString("yyyy-MM-dd");
        Disc = track.DiscNumber;
        Duration = track.Duration;
        Favorite = isFavorite;
        Quality = track.Quality;
        Track = track.TrackNumber;
        Type = "track";
        AlbumId = track.AlbumTrack.FirstOrDefault()?.AlbumId.ToString() ?? string.Empty;
        AlbumName = track.AlbumTrack.FirstOrDefault()?.Album.Name ?? string.Empty;
        ArtistTrack = track.ArtistTrack.Select(at => new TrackArtist
        {
            Id = at.ArtistId.ToString(),
            Name = at.Artist.Name,
            Link = new($"/music/artist/{at.ArtistId}", UriKind.Relative),
            Type = "artist"
        });
    }

    public TrackRowData(Track track, string country)
    {
        Id = track.Id.ToString();
        Name = track.Name;
        ColorPalette = track.AlbumTrack.FirstOrDefault()?.Album.ColorPalette ?? track.ArtistTrack.FirstOrDefault()?.Artist.ColorPalette;
        string? cover = track.AlbumTrack.FirstOrDefault()?.Album.Cover ?? track.ArtistTrack.FirstOrDefault()?.Artist.Cover;
        Cover = cover is not null ? $"/images/music{cover}" : null;
        Path = $"/{track.FolderId}{track.Folder}{track.Filename}";
        Link = $"/music/tracks/{track.Id}";
        Date = track.UpdatedAt.ToString("yyyy-MM-dd");
        Disc = track.DiscNumber;
        Duration = track.Duration;
        Favorite = track.TrackUser.Count != 0;
        Quality = track.Quality;
        Track = track.TrackNumber;
        Type = "track";
        AlbumId = track.AlbumTrack.FirstOrDefault()?.AlbumId.ToString() ?? string.Empty;
        AlbumName = track.AlbumTrack.FirstOrDefault()?.Album.Name ?? string.Empty;
        ArtistTrack = track.ArtistTrack
            .DistinctBy(at => at.ArtistId)
            .Select(at => new TrackArtist
            {
                Id = at.ArtistId.ToString(),
                Name = at.Artist.Name,
                Link = new($"/music/artist/{at.ArtistId}", UriKind.Relative),
                Type = "artist"
            });
        AlbumTrack = track.AlbumTrack
            .DistinctBy(at => at.AlbumId)
            .Select(at => new TrackArtist
            {
                Id = at.AlbumId.ToString(),
                Name = at.Album.Name,
                Link = new($"/music/album/{at.AlbumId}", UriKind.Relative),
                Type = "album"
            });
    }

    public TrackRowData(SearchTrackCardDto track)
    {
        Id = track.Id.ToString();
        Name = track.Name;
        string? colorPaletteStr = track.AlbumColorPalette ?? track.ArtistColorPalette;
        ColorPalette = !string.IsNullOrEmpty(colorPaletteStr)
            ? JsonConvert.DeserializeObject<IColorPalettes>(colorPaletteStr)
            : null;
        string? cover = track.AlbumCover ?? track.ArtistCover;
        Cover = cover is not null ? $"/images/music{cover}" : null;
        Path = $"/{track.FolderId}{track.Folder}{track.Filename}";
        Link = $"/music/tracks/{track.Id}";
        Date = track.UpdatedAt.ToString("yyyy-MM-dd");
        Disc = track.DiscNumber;
        Duration = track.Duration;
        Favorite = track.Favorite;
        Quality = track.Quality;
        Track = track.TrackNumber;
        Type = "track";
        AlbumId = track.AlbumId ?? string.Empty;
        AlbumName = track.AlbumName ?? string.Empty;
        ArtistTrack = track.Artists
            .Select(at => new TrackArtist
            {
                Id = at.Id.ToString(),
                Name = at.Name,
                Link = new($"/music/artist/{at.Id}", UriKind.Relative),
                Type = "artist"
            });
        AlbumTrack = track.Albums
            .Select(at => new TrackArtist
            {
                Id = at.Id.ToString(),
                Name = at.Name,
                Link = new($"/music/album/{at.Id}", UriKind.Relative),
                Type = "album"
            });
    }
}

public record LyricLine
{
    [JsonProperty("time")] public double Time { get; set; }
    [JsonProperty("text")] public string Text { get; set; } = null!;
    [JsonProperty("link")] public Uri Link { get; set; } = null!;
    [JsonProperty("type")] public string Type { get; set; } = null!;
}

public record TrackArtist
{
    [JsonProperty("id")] public string Id { get; set; } = null!;
    [JsonProperty("name")] public string Name { get; set; } = null!;
    [JsonProperty("link")] public Uri Link { get; set; } = null!;
    [JsonProperty("type")] public string Type { get; set; } = null!;
}
