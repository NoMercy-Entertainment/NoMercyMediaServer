using Newtonsoft.Json;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Music;

namespace NoMercy.Api.DTOs.Media.Components;

/// <summary>
/// Data for NMTopResultCard component - search top result.
/// </summary>
public record TopResultCardData
{
    [JsonProperty("id")] public string Id { get; set; } = null!;
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = null!;
    [JsonProperty("link")] public string Link { get; set; } = null!;
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("artists")] public IEnumerable<TopResultArtist> Artists { get; set; } = [];
    [JsonProperty("albums")] public IEnumerable<TopResultAlbum> Albums { get; set; } = [];
    [JsonProperty("track")] public TopResultTrack? Track { get; set; }

    public TopResultCardData()
    {
    }

    public TopResultCardData(Artist artist)
    {
        Id = artist.Id.ToString();
        Title = artist.Name;
        Type = "artist";
        Link = $"/music/artist/{artist.Id}";
        Cover = artist.Cover;
        ColorPalette = artist.ColorPalette;
        Link = $"/music/album/{artist.Id}";
        Type = "album";
    }

    public TopResultCardData(Album album)
    {
        Id = album.Id.ToString();
        Title = album.Name;
        Type = "album";
        Link = $"/music/album/{album.Id}";
        Cover = album.Cover;
        ColorPalette = album.ColorPalette;
        Artists = album.AlbumArtist.Select(aa => new TopResultArtist
        {
            Id = aa.ArtistId.ToString(),
            Name = aa.Artist.Name,
            Link = new($"/music/artist/{aa.ArtistId}", UriKind.Relative),
            Type = "artist"
        });
    }

    public TopResultCardData(Track track)
    {
        Id = track.Id.ToString();
        Title = track.Name;
        Type = "track";
        Link = $"/music/tracks/{track.Id}";
        Cover = track.Cover;
        ColorPalette = track.ColorPalette;
        Artists = track.ArtistTrack.Select(at => new TopResultArtist
        {
            Id = at.ArtistId.ToString(),
            Name = at.Artist.Name,
            Link = new($"/music/artist/{at.ArtistId}", UriKind.Relative),
            Type = "artist"
        });
        Albums = track.AlbumTrack.Select(at => new TopResultAlbum
        {
            Id = at.AlbumId.ToString(),
            Name = at.Album.Name,
            Link = new($"/music/album/{at.AlbumId}", UriKind.Relative),
            Type = "album"
        });
        Track = new()
        {
            Id = track.Id.ToString(),
            Name = track.Name,
            Duration = track.Duration,
            Path = $"/{track.FolderId}{track.Folder}{track.Filename}",
            Link = new($"/music/tracks/{track.Id}", UriKind.Relative),
            Type = "track",
            Disc = track.DiscNumber,
            Track = track.TrackNumber,
            Quality = track.Quality,
            Artists = track.ArtistTrack.Select(at => new TopResultArtist
            {
                Id = at.ArtistId.ToString(),
                Name = at.Artist.Name,
                Link = new($"/music/artist/{at.ArtistId}", UriKind.Relative),
                Type = "artist"
            }),
            Albums = track.AlbumTrack.Select(at => new TopResultAlbum
            {
                Id = at.AlbumId.ToString(),
                Name = at.Album.Name,
                Link = new($"/music/album/{at.AlbumId}", UriKind.Relative),
                Type = "album"
            })
        };
    }

    public TopResultCardData(SearchTrackCardDto track)
    {
        Id = track.Id.ToString();
        Title = track.Name;
        Type = "track";
        Link = $"/music/tracks/{track.Id}";
        string? cover = track.AlbumCover ?? track.ArtistCover;
        Cover = cover is not null ? $"/images/music{cover}" : null;
        string? colorPaletteStr = track.AlbumColorPalette ?? track.ArtistColorPalette;
        ColorPalette = !string.IsNullOrEmpty(colorPaletteStr)
            ? JsonConvert.DeserializeObject<IColorPalettes>(colorPaletteStr)
            : null;
        Artists = track.Artists.Select(at => new TopResultArtist
        {
            Id = at.Id.ToString(),
            Name = at.Name,
            Link = new($"/music/artist/{at.Id}", UriKind.Relative),
            Type = "artist"
        });
        Albums = track.Albums.Select(at => new TopResultAlbum
        {
            Id = at.Id.ToString(),
            Name = at.Name,
            Link = new($"/music/album/{at.Id}", UriKind.Relative),
            Type = "album"
        });
        Track = new()
        {
            Id = track.Id.ToString(),
            Name = track.Name,
            Duration = track.Duration,
            Path = $"/{track.FolderId}{track.Folder}{track.Filename}",
            Link = new($"/music/tracks/{track.Id}", UriKind.Relative),
            Type = "track",
            Disc = track.DiscNumber,
            Track = track.TrackNumber,
            Quality = track.Quality,
            Artists = track.Artists.Select(at => new TopResultArtist
            {
                Id = at.Id.ToString(),
                Name = at.Name,
                Link = new($"/music/artist/{at.Id}", UriKind.Relative),
                Type = "artist"
            }),
            Albums = track.Albums.Select(at => new TopResultAlbum
            {
                Id = at.Id.ToString(),
                Name = at.Name,
                Link = new($"/music/album/{at.Id}", UriKind.Relative),
                Type = "album"
            })
        };
    }

    public TopResultCardData(ArtistCardDto artist)
    {
        Id = artist.Id.ToString();
        Title = artist.Name;
        Type = "artist";
        Link = $"/music/artist/{artist.Id}";
        Cover = artist.Cover is not null ? $"/images/music{artist.Cover}" : null;
        ColorPalette = !string.IsNullOrEmpty(artist.ColorPalette)
            ? JsonConvert.DeserializeObject<IColorPalettes>(artist.ColorPalette)
            : null;
    }

    public TopResultCardData(AlbumCardDto album)
    {
        Id = album.Id.ToString();
        Title = album.Name;
        Type = "album";
        Link = $"/music/album/{album.Id}";
        Cover = album.Cover is not null ? $"/images/music{album.Cover}" : null;
        ColorPalette = !string.IsNullOrEmpty(album.ColorPalette)
            ? JsonConvert.DeserializeObject<IColorPalettes>(album.ColorPalette)
            : null;
    }
}

public record TopResultArtist
{
    [JsonProperty("id")] public string Id { get; set; } = null!;
    [JsonProperty("name")] public string Name { get; set; } = null!;
    [JsonProperty("link")] public Uri Link { get; set; } = null!;
    [JsonProperty("type")] public string Type { get; set; } = null!;
}

public record TopResultAlbum
{
    [JsonProperty("id")] public string Id { get; set; } = null!;
    [JsonProperty("name")] public string Name { get; set; } = null!;
    [JsonProperty("link")] public Uri Link { get; set; } = null!;
    [JsonProperty("type")] public string Type { get; set; } = null!;
}

public record TopResultTrack
{
    [JsonProperty("id")] public string Id { get; set; } = null!;
    [JsonProperty("name")] public string Name { get; set; } = null!;
    [JsonProperty("duration")] public string? Duration { get; set; }
    [JsonProperty("path")] public string? Path { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; } = null!;
    [JsonProperty("type")] public string Type { get; set; } = null!;
    [JsonProperty("disc")] public int Disc { get; set; }
    [JsonProperty("track")] public int Track { get; set; }
    [JsonProperty("quality")] public int? Quality { get; set; }
    [JsonProperty("artist_track")] public IEnumerable<TopResultArtist> Artists { get; set; } = [];
    [JsonProperty("album_track")] public IEnumerable<TopResultAlbum> Albums { get; set; } = [];
}
