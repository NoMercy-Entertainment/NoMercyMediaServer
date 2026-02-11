using Newtonsoft.Json;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.DTOs.Music;

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
        Cover = !string.IsNullOrEmpty(album.Cover) ?
            new Uri($"/images/music{album.Cover}", UriKind.Relative).ToString() 
            : null;
        Disambiguation = album.Disambiguation;
        Description = album.Description;
        Favorite = album.AlbumUser.Count != 0;
        Id = album.Id;
        LibraryId = album.LibraryId;
        Name = album.Name;
        Link = new($"/music/album/{Id}", UriKind.Relative);
        Type = "album";

        Artists = album.AlbumArtist
            .DistinctBy(trackArtist => trackArtist.ArtistId)
            .Select(albumArtist => new ArtistDto(albumArtist, country!));

        Genres = album.AlbumMusicGenre.Select(musicGenre => new GenreDto(musicGenre));

        Images = album.Images.Select(image => new ImageDto(image));

        Tracks = album.AlbumTrack
            .Select(albumTrack => new AlbumTrackDto(albumTrack, country!));
    }
}