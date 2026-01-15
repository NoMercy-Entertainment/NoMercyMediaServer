using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.V1.Music.DTO;

public record AlbumDto
{
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("disambiguation")] public string? Disambiguation { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }

    [JsonProperty("description")] public string? Description { get; set; }

    // [JsonProperty("tracks")] public IEnumerable<Track> Tracks { get; set; }
    [JsonProperty("year")] public int? Year { get; set; }
    [JsonProperty("album_artist")] public Guid? AlbumArtist { get; set; }
    [JsonProperty("type")] public string Type { get; set; }

    public AlbumDto(AlbumArtist albumArtist, string country)
    {
        string? description = albumArtist.Album.Translations
            .FirstOrDefault(translation => translation.Iso31661 == country)?
            .Description;

        Image? img = albumArtist.Artist.Images.FirstOrDefault(image => image.Type == "background");

        Id = albumArtist.Album.Id;
        Name = albumArtist.Album.Name;
        Cover = albumArtist.Album.Cover is not null
            ? new Uri($"/images/music{albumArtist.Album.Cover}", UriKind.Relative).ToString()
            : null;
        Backdrop = img?.FilePath is not null
            ? new Uri($"/images/music{img.FilePath}", UriKind.Relative).ToString()
            : null;
        Disambiguation = albumArtist.Album.Disambiguation;
        Link = new($"/music/album/{Id}", UriKind.Relative);
        Description = !string.IsNullOrEmpty(description)
            ? description
            : albumArtist.Album.Description;
        Type = "album";
        ColorPalette = albumArtist.Album.ColorPalette;
        // Tracks = albumArtist.Albums.AlbumTrack.Select(a => a.Track);
        Year = albumArtist.Album.Year;

        AlbumArtist = albumArtist.ArtistId;
    }


    public AlbumDto(AlbumTrack albumTrack, string country)
    {
        string? description = albumTrack.Album.Translations
            .FirstOrDefault(translation => translation.Iso31661 == country)?
            .Description;

        Image? img = albumTrack.Album.AlbumArtist.FirstOrDefault()?.Album.Images
            .FirstOrDefault(image => image.Type == "background");
        Id = albumTrack.Album.Id;
        Name = albumTrack.Album.Name;
        Cover = albumTrack.Album.Cover is not null
            ? new Uri($"/images/music{albumTrack.Album.Cover}", UriKind.Relative).ToString()
            : null;
        Backdrop = img?.FilePath is not null
            ? new Uri($"/images/music{img.FilePath}", UriKind.Relative).ToString()
            : null;
        Disambiguation = albumTrack.Album.Disambiguation;
        Link = new($"/music/album/{Id}", UriKind.Relative);
        Description = !string.IsNullOrEmpty(description)
            ? description
            : albumTrack.Album.Description;
        Type = "album";
        ColorPalette = albumTrack.Album.ColorPalette;
        Year = albumTrack.Album.Year;

        // using MediaContext mediaContext = new();
        // Tracks =  albumTrack.Albums.AlbumTrack.Select(a => a.Track);

        AlbumArtist = albumTrack.Album.AlbumArtist.MaxBy(at => at.ArtistId)?.ArtistId;
    }

    public AlbumDto(Album album, string country)
    {
        string? description = album.Translations
            .FirstOrDefault(translation => translation.Iso31661 == country)?
            .Description;
        Image? img = album.AlbumArtist.FirstOrDefault()?.Artist.Images
            .FirstOrDefault(image => image.Type == "background");

        Id = album.Id;
        Name = album.Name;
        Cover = album.Cover is not null ? new Uri($"/images/music{album.Cover}", UriKind.Relative).ToString() : null;
        Backdrop = img?.FilePath is not null
            ? new Uri($"/images/music{img.FilePath}", UriKind.Relative).ToString()
            : null;
        Disambiguation = album.Disambiguation;
        Link = new($"/music/album/{Id}", UriKind.Relative);
        Description = !string.IsNullOrEmpty(description)
            ? description
            : album.Description;
        Type = "album";
        ColorPalette = album.ColorPalette;
        Disambiguation = album.Disambiguation;
        // Tracks = album.AlbumTrack.Select(a => a.Track);
        Year = album.Year;

        List<IGrouping<Guid, AlbumArtist>> artists = album.AlbumArtist
            .GroupBy(albumArtist => albumArtist.ArtistId)
            .OrderBy(artist => artist.Count())
            .ToList();

        int trackCount = album.Tracks;

        int? artistTrackCount = album.AlbumTrack
            .Select(albumTrack => albumTrack.Track)
            .SelectMany(track => track.ArtistTrack)
            .Count();

        bool isAlbumArtist = artistTrackCount >= trackCount * 0.45;

        AlbumArtist = isAlbumArtist
            ? artists.FirstOrDefault()?.Key
            : null;
    }
}