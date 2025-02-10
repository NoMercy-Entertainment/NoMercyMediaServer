using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.V1.Music.DTO;
public record ArtistResponseItemDto
{
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("country")] public string? Country { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("disambiguation")] public string? Disambiguation { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("favorite")] public bool Favorite { get; set; }
    [JsonProperty("folder")] public string? Folder { get; set; }
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("library_id")] public Ulid? LibraryId { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("year")] public int? Year { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }

    [JsonProperty("playlists")] public IEnumerable<AlbumDto> Playlists { get; set; }
    [JsonProperty("tracks")] public IEnumerable<ArtistTrackDto> Tracks { get; set; }
    [JsonProperty("favorite_tracks")] public IEnumerable<ArtistTrackDto> FavoriteTracks { get; set; }
    [JsonProperty("images")] public IEnumerable<ImageDto> Images { get; set; }
    [JsonProperty("genres")] public IEnumerable<GenreDto> Genres { get; set; }
    [JsonProperty("albums")] public IEnumerable<AlbumDto> Albums { get; set; }
    [JsonProperty("featured")] public IEnumerable<AlbumDto> Featured { get; set; }

    public ArtistResponseItemDto(Artist artist, Guid userId, string? country = "US")
    {
        ColorPalette = artist.ColorPalette;
        Cover = artist.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Disambiguation = artist.Disambiguation;
        Description = artist.Description;
        Favorite = artist.ArtistUser.Any();
        Folder = artist.Folder;
        Id = artist.Id;
        LibraryId = artist.LibraryId;
        Name = artist.Name;
        Type = "artists";
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
            .Select(artistTrack => artistTrack.Track.AlbumTrack.First().Album)
            .GroupBy(album => album.Name.RemoveNonAlphaNumericCharacters())
            .Select(album => album.First())
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
            .GroupBy(artistTrack => artistTrack.AlbumName + artistTrack.Name)
            .Select(artistTrack => artistTrack.First())
            .OrderBy(artistTrack => artistTrack.AlbumName)
            .ThenBy(artistTrack => artistTrack.Disc)
            .ThenBy(artistTrack => artistTrack.Track);

        // TODO replace inner-query....
        MediaContext context = new();
        List<ArtistTrackDto> favorites = context.MusicPlays
            .AsNoTracking()
            .Where(musicPlay => musicPlay.UserId.Equals(userId))
            .Where(musicPlay => musicPlay.Track.ArtistTrack
                .Any(artistTrack => artistTrack.ArtistId == artist.Id))

            .Include(musicPlay => musicPlay.Track)
            .ThenInclude(track => track.TrackUser)

            .Include(musicPlay => musicPlay.Track)
            .ThenInclude(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            .ThenInclude(albumTrack => albumTrack.Translations)

            .Include(musicPlay => musicPlay.Track)
            .ThenInclude(track => track.ArtistTrack)
            .ThenInclude(albumTrack => albumTrack.Artist)
            .ThenInclude(album => album.Translations)
            .Select(artistTrack => new ArtistTrackDto(artistTrack.Track, country!))
            .ToList();

        FavoriteTracks = favorites.GroupBy(musicPlay => musicPlay.Id)
            .OrderByDescending(musicPlay => musicPlay.Count())
            .Select(musicPlay => musicPlay.First());
    }
}
