using Newtonsoft.Json;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Music;

public record ArtistResponseItemDto
{
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("country")] public string? Country { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("disambiguation")] public string? Disambiguation { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("favorite")] public bool Favorite { get; set; }
    [JsonProperty("folder")] public string? Folder { get; set; }
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("library_id")] public Ulid? LibraryId { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("year")] public int? Year { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; } = null!;

    [JsonProperty("playlists")] public IEnumerable<AlbumDto> Playlists { get; set; } = [];
    [JsonProperty("tracks")] public IEnumerable<ArtistTrackDto> Tracks { get; set; } = [];
    [JsonProperty("favorite_tracks")] public List<FavoriteTrackDto> FavoriteTracks { get; set; } = [];
    [JsonProperty("images")] public IEnumerable<ImageDto> Images { get; set; } = [];
    [JsonProperty("genres")] public IEnumerable<GenreDto> Genres { get; set; } = [];
    [JsonProperty("albums")] public IEnumerable<AlbumDto> Albums { get; set; } = [];
    [JsonProperty("featured")] public List<AlbumDto> Featured { get; set; } = [];

    public ArtistResponseItemDto(Artist artist, Guid userId, string? country = "US")
    {
        string? description = artist.Translations
            .FirstOrDefault(translation => translation.Iso31661 == country)?.Description ?? artist.Description;

        Image? thumb = artist.Images.OrderByDescending(i => i.VoteAverage).FirstOrDefault(i => i.Type == "thumb");
        Image? background = artist.Images.FirstOrDefault(image => image.Type == "background");

        Backdrop = background?.FilePath is not null
            ? new Uri($"/images/music{background.FilePath}", UriKind.Relative).ToString()
            : null;

        IColorPalettes? palette = artist.ColorPalette ?? thumb?.ColorPalette;

        Cover = artist.Cover ?? thumb?.FilePath;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;

        ColorPalette = palette;
        Disambiguation = artist.Disambiguation;
        Description = description;
        Favorite = artist.ArtistUser.Count != 0;
        Folder = artist.Folder;
        Id = artist.Id;
        LibraryId = artist.LibraryId;
        Name = artist.Name;
        Type = "artist";
        Link = new($"/music/artist/{Id}", UriKind.Relative);

        Genres = artist.ArtistMusicGenre
            .Select(artistMusicGenre => new GenreDto(artistMusicGenre));

        Images = artist.Images.Select(image => new ImageDto(image));

        Albums = artist.AlbumArtist
            .Select(album => new AlbumDto(album, country!))
            .GroupBy(album => album.Id)
            .Select(album => album.First())
            .OrderBy(artistTrack => artistTrack.Year);

        Featured = artist.ArtistTrack
            .Select(artistTrack => artistTrack.Track.AlbumTrack.FirstOrDefault()?.Album)
            .Where(album => album != null)
            .GroupBy(album => album!.Name.RemoveNonAlphaNumericCharacters())
            .Select(album => album.First()!)
            .OrderBy(album => album.Year)
            .Where(album => Albums.All(albumDto => albumDto.Id != album.Id))
            .Select(album => new AlbumDto(album, country!))
            .OrderBy(artistTrack => artistTrack.Year)
            .ToList();

        Playlists = artist.AlbumArtist
            .DistinctBy(albumArtist => albumArtist.AlbumId)
            .Where(album => album.Album.AlbumUser.Any(user => user.UserId.Equals(userId)))
            .Select(trackAlbum => new AlbumDto(trackAlbum, country!))
            .OrderBy(album => album.Year);

        Tracks = artist.ArtistTrack
            .Select(artistTrack => new ArtistTrackDto(artistTrack, country!))
            // .GroupBy(artistTrack => artistTrack.AlbumName + artistTrack.Name)
            // .Select(artistTrack => artistTrack.First())
            .DistinctBy(artistTrack => artistTrack.Id)
            .OrderBy(artistTrack => artistTrack.AlbumName)
            .ThenBy(artistTrack => artistTrack.Disc)
            .ThenBy(artistTrack => artistTrack.Track);

        FavoriteTracks = artist.ArtistTrack
            .Where(artistTrack => artistTrack.Track.MusicPlays.Count > 0)
            .Select(artistTrack => new FavoriteTrackDto(artistTrack, country!))
            .ToList();
    }
}