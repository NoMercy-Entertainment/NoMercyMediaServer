using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.V1.Music.DTO;
public record AlbumDto
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

    public AlbumDto(AlbumArtist albumArtist, string country)
    {
        string? description = albumArtist.Album.Translations?
            .FirstOrDefault(translation => translation.Iso31661 == country)?
            .Description;

        Id = albumArtist.Album.Id;
        Name = albumArtist.Album.Name;
        Cover = albumArtist.Album.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Disambiguation = albumArtist.Album.Disambiguation;
        Link = new($"/music/album/{Id}", UriKind.Relative);
        Description = !string.IsNullOrEmpty(description)
            ? description
            : albumArtist.Album.Description;
        Type = "albums";
        ColorPalette = albumArtist.Album.ColorPalette;
        Tracks = albumArtist.Album.AlbumTrack?.Count ?? 0;
        Year = albumArtist.Album.Year;

        AlbumArtist = albumArtist.ArtistId;
    }

    public AlbumDto(AlbumTrack albumTrack, string country)
    {
        string? description = albumTrack.Album.Translations?
            .FirstOrDefault(translation => translation.Iso31661 == country)?
            .Description;

        Id = albumTrack.Album.Id;
        Name = albumTrack.Album.Name;
        Cover = albumTrack.Album.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Disambiguation = albumTrack.Album.Disambiguation;
        Link = new($"/music/album/{Id}", UriKind.Relative);
        Description = !string.IsNullOrEmpty(description)
            ? description
            : albumTrack.Album.Description;
        Type = "albums";
        ColorPalette = albumTrack.Album.ColorPalette;
        Year = albumTrack.Album.Year;

        using MediaContext mediaContext = new();
        int? tracks = mediaContext.Albums
            .Include(a => a.AlbumTrack)
            .ThenInclude(at => at.Track)
            .FirstOrDefault(a => a.Id == albumTrack.AlbumId)?.AlbumTrack
            .Count(at => at.Track.Folder != null);
        Tracks = tracks ?? 0;

        AlbumArtist = albumTrack.Album.AlbumArtist?.MaxBy(at => at.ArtistId)?.ArtistId;
    }

    public AlbumDto(Album album, string country)
    {
        string? description = album.Translations
            .FirstOrDefault(translation => translation.Iso31661 == country)?
            .Description;

        Id = album.Id;
        Name = album.Name;
        Cover = album.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Disambiguation = album.Disambiguation;
        Link = new($"/music/album/{Id}", UriKind.Relative);
        Description = !string.IsNullOrEmpty(description)
            ? description
            : album.Description;
        Type = "albums";
        ColorPalette = album.ColorPalette;
        Disambiguation = album.Disambiguation;
        Tracks = album.AlbumTrack.Count(at => at.Track.Folder != null);
        Year = album.Year;

        List<IGrouping<Guid, AlbumArtist>> artists = album.AlbumArtist
            .GroupBy(albumArtist => albumArtist.ArtistId)
            .OrderBy(artist => artist.Count())
            .ToList();

        int trackCount = album.Tracks;

        int? artistTrackCount = album.AlbumTrack?
            .Select(albumTrack => albumTrack.Track)
            .SelectMany(track => track.ArtistTrack)
            .Count();

        using MediaContext mediaContext = new();
        int? tracks = mediaContext.Albums
            .Include(a => a.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Track)
            .FirstOrDefault(a => a.Id == album.Id)?.AlbumTrack
            .Count(at => at.Track.Folder != null);

        Tracks = tracks ?? 0;

        bool isAlbumArtist = artistTrackCount >= trackCount * 0.45;

        AlbumArtist = isAlbumArtist
            ? artists.FirstOrDefault()?.Key
            : null;
    }
}
