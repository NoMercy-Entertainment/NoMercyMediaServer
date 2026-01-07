using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.V1.Media.DTO.Components;

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
            Name = aa.Artist.Name
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
            Name = at.Artist.Name
        });
        Albums = track.AlbumTrack.Select(at => new TopResultAlbum
        {
            Id = at.AlbumId.ToString(),
            Name = at.Album.Name
        });
        Track = new()
        {
            Id = track.Id.ToString(),
            Name = track.Name,
            Duration = track.Duration,
            Path = track.Filename
        };
    }
}

public record TopResultArtist
{
    [JsonProperty("id")] public string Id { get; set; } = null!;
    [JsonProperty("name")] public string Name { get; set; } = null!;
}

public record TopResultAlbum
{
    [JsonProperty("id")] public string Id { get; set; } = null!;
    [JsonProperty("name")] public string Name { get; set; } = null!;
}

public record TopResultTrack
{
    [JsonProperty("id")] public string Id { get; set; } = null!;
    [JsonProperty("name")] public string Name { get; set; } = null!;
    [JsonProperty("duration")] public string? Duration { get; set; }
    [JsonProperty("path")] public string? Path { get; set; }
}
