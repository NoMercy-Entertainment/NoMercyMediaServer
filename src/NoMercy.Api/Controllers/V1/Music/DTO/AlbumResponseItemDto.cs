using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.V1.Music.DTO;
public record AlbumResponseItemDto
{
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("disambiguation")] public string? Disambiguation { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("country")] public string? Country { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("favorite")] public bool Favorite { get; set; }
    [JsonProperty("library_id")] public Ulid? LibraryId { get; set; }
    [JsonProperty("year")] public int? Year { get; set; }
    [JsonProperty("artists")] public IEnumerable<ArtistDto> Artists { get; set; }
    [JsonProperty("tracks")] public IEnumerable<AlbumTrackDto> Tracks { get; set; }
    [JsonProperty("images")] public IEnumerable<ImageDto> Images { get; set; }
    [JsonProperty("genres")] public IEnumerable<GenreDto> Genres { get; set; }
    [JsonProperty("type")] public string Type { get; set; }

    public AlbumResponseItemDto(Album album, string? country = "US")
    {
        ColorPalette = album.ColorPalette;
        Cover = album.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Disambiguation = album.Disambiguation;
        Description = album.Description;
        Favorite = album.AlbumUser.Count != 0;
        Id = album.Id;
        LibraryId = album.LibraryId;
        Name = album.Name;
        Link = new($"/music/album/{Id}", UriKind.Relative);
        Type = "albums";
        // using MediaContext mediaContext = new();
        // List<AlbumTrack> artists = mediaContext.AlbumTrack
        //     .AsNoTracking()
        //     .Where(at => at.TrackId == album.Id)
        //     .Include(at => at.Track)
        //     .ThenInclude(track => track.ArtistTrack)
        //     .ThenInclude(artistTrack => artistTrack.Artist)
        //     .ThenInclude(artist => artist.Translations)
        //     .ToList() ?? [];

        // Artists = artists
        //     .SelectMany(albumTrack => albumTrack.Track.ArtistTrack)
        //     .Select(albumTrack => new ArtistDto(albumTrack, country!));

        Artists = album.AlbumArtist
            .DistinctBy(trackArtist => trackArtist.ArtistId)
            .Select(albumArtist => new ArtistDto(albumArtist, country!));

        Genres = album.AlbumMusicGenre.Select(musicGenre => new GenreDto(musicGenre));

        Images = album.Images.Select(image => new ImageDto(image));

        Tracks = album.AlbumTrack
            .Select(albumTrack => new AlbumTrackDto(albumTrack, country!));
    }
}
