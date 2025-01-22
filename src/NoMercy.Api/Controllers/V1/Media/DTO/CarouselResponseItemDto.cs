using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record CarouselResponseItemDto
{
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("disambiguation")] public string? Disambiguation { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("favorite")] public bool Favorite { get; set; }
    [JsonProperty("folder")] public string? Folder { get; set; }
    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("library_id")] public Ulid? LibraryId { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("track_id")] public string? TrackId { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }

    [JsonProperty("tracks")] public int Tracks { get; set; }

    public static readonly Func<MediaContext, Guid, Task<List<CarouselResponseItemDto>>> GetPlaylists =
        (mediaContext, userId) => mediaContext.Playlists
            .Where(playlist => playlist.UserId.Equals(userId))
            .Where(playlist => playlist.Tracks
                .Any(artistTrack => artistTrack.Track.Duration != null))
            .Select(playlist => new CarouselResponseItemDto(playlist))
            .Take(36)
            .ToListAsync();

    public static readonly Func<MediaContext, Guid, Task<List<CarouselResponseItemDto>>> GetLatestAlbums =
        (mediaContext, userId) => mediaContext.Albums
            .Where(album => album.Cover != null && album.AlbumTrack.Count > 0)
            .Where(album => album.AlbumTrack
                .Any(artistTrack => artistTrack.Track.Duration != null))
            .Include(album => album.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Track)
            .OrderByDescending(album => album.CreatedAt)
            .Select(album => new CarouselResponseItemDto(album))
            .Take(36)
            .ToListAsync();

    public static readonly Func<MediaContext, Guid, Task<List<CarouselResponseItemDto>>> GetLatestArtists =
        (mediaContext, userId) => mediaContext.Artists
            .Where(artist => artist.Cover != null && artist.ArtistTrack.Count > 0)
            .Where(artist => artist.ArtistTrack
                .Any(artistTrack => artistTrack.Track.Duration != null))
            .Include(artist => artist.Images
                .Where(image => image.Type == "thumb")
                .OrderByDescending(image => image.VoteCount)
            )
            .Include(artist => artist.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Track)
            .OrderByDescending(artist => artist.CreatedAt)
            .Select(artist => new CarouselResponseItemDto(artist))
            .Take(36)
            .ToListAsync();

    public static readonly Func<MediaContext, Guid, Task<List<CarouselResponseItemDto>>> GetFavoriteArtists =
        (mediaContext, userId) => mediaContext.ArtistUser
            .Where(artistUser => artistUser.UserId.Equals(userId))
            .Include(albumUser => albumUser.Artist)
            .ThenInclude(artist => artist.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Track)
            .Include(albumUser => albumUser.Artist)
                .ThenInclude(artist => artist.Images
                    .Where(image => image.Type == "thumb")
                    .OrderByDescending(image => image.VoteCount)
                )
            .Select(artistUser => new CarouselResponseItemDto(artistUser))
            .Take(36)
            .ToListAsync();

    public static readonly Func<MediaContext, Guid, Task<List<CarouselResponseItemDto>>> GetFavoriteAlbums =
        (mediaContext, userId) => mediaContext.AlbumUser
            .Where(albumUser => albumUser.UserId.Equals(userId))
            .Include(albumUser => albumUser.Album)
            .ThenInclude(album => album.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Track)
            .Select(albumUser => new CarouselResponseItemDto(albumUser))
            .Take(36)
            .ToListAsync();

    public static readonly Func<MediaContext, Guid, TopMusicDto?> GetFavoriteArtist =
        (mediaContext, userId) => mediaContext.MusicPlays
            .Where(musicPlay => musicPlay.UserId.Equals(userId))
            .Include(musicPlay => musicPlay.Track)
            .ThenInclude(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .ThenInclude(musicPlay => musicPlay.Images
                .Where(image => image.Type == "thumb")
                .OrderByDescending(image => image.VoteCount)
            )
            .SelectMany(p => p.Track.ArtistTrack)
            .Select(artistTrack => new TopMusicDto(artistTrack))
            .AsEnumerable()
            .GroupBy(a => a.Name)
            .MaxBy(g => g.Count())?
            .FirstOrDefault();

    public static readonly Func<MediaContext, Guid, TopMusicDto?> GetFavoriteAlbum =
        (mediaContext, userId) => mediaContext.MusicPlays
            .Where(musicPlay => musicPlay.UserId.Equals(userId))
            .Include(musicPlay => musicPlay.Track)
            .ThenInclude(track => track.AlbumTrack)
            .ThenInclude(artistTrack => artistTrack.Album)
            .SelectMany(p => p.Track.AlbumTrack)
            .Select(albumTrack => new TopMusicDto(albumTrack))
            .AsEnumerable()
            .GroupBy(a => a.Name)
            .MaxBy(g => g.Count())?
            .FirstOrDefault();

    public static readonly Func<MediaContext, Guid, TopMusicDto?> GetFavoritePlaylist =
        (mediaContext, userId) => mediaContext.MusicPlays
            .Where(musicPlay => musicPlay.UserId.Equals(userId))
            .Include(musicPlay => musicPlay.Track)
            .ThenInclude(track => track.PlaylistTrack)
            .ThenInclude(artistTrack => artistTrack.Playlist)
            .SelectMany(p => p.Track.PlaylistTrack)
            .Select(musicPlay => new TopMusicDto(musicPlay))
            .AsEnumerable()
            .GroupBy(a => a.Name)
            .MaxBy(g => g.Count())?
            .FirstOrDefault();

    public CarouselResponseItemDto(Artist artist)
    {
        ColorPalette = artist.ColorPalette;
        Cover = artist.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Disambiguation = artist.Disambiguation;
        Description = artist.Description;
        Folder = artist.Folder ?? "";
        Id = artist.Id.ToString();
        LibraryId = artist.LibraryId ?? Ulid.Empty;
        Name = artist.Name;
        Type = "artists";
        Link = new($"/music/artist/{Id}", UriKind.Relative);

        Tracks = artist.ArtistTrack
            .Where(artistTrack => artistTrack.Track.Duration != null)
            .DistinctBy(artistTrack => artistTrack.Track.Name.ToLower())
            .Count();
    }

    public CarouselResponseItemDto(Album album)
    {
        ColorPalette = album.ColorPalette;
        Cover = album.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Disambiguation = album.Disambiguation;
        Description = album.Description;
        Folder = album.Folder ?? "";
        Id = album.Id.ToString();
        LibraryId = album.LibraryId ?? Ulid.Empty;
        Name = album.Name;
        Type = "albums";
        Link = new($"/music/album/{Id}", UriKind.Relative);

        Tracks = album.AlbumTrack
            .Where(albumTrack => albumTrack.Track.Duration != null)
            .DistinctBy(albumTrack => albumTrack.Track.Name.ToLower())
            .Count();
    }

    public CarouselResponseItemDto(ArtistUser playlist)
    {
        ColorPalette = playlist.Artist.ColorPalette;
        Cover = playlist.Artist.Cover ?? playlist.Artist.Images
            .FirstOrDefault()?.FilePath;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Disambiguation = playlist.Artist.Disambiguation;
        Description = playlist.Artist.Description;
        Folder = playlist.Artist.Folder ?? "";
        Id = playlist.Artist.Id.ToString();
        LibraryId = playlist.Artist.LibraryId ?? Ulid.Empty;
        Name = playlist.Artist.Name;
        Type = "artists";
        Link = new($"/music/artist/{Id}", UriKind.Relative);

        Tracks = playlist.Artist.ArtistTrack
            .Where(artistTrack => artistTrack.Track.Duration != null)
            .DistinctBy(artistTrack => artistTrack.Track.Name.ToLower())
            .Count();
    }

    public CarouselResponseItemDto(AlbumUser playlist)
    {
        ColorPalette = playlist.Album.ColorPalette;
        Cover = playlist.Album.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Disambiguation = playlist.Album.Disambiguation;
        Description = playlist.Album.Description;
        Folder = playlist.Album.Folder ?? "";
        Id = playlist.Album.Id.ToString();
        LibraryId = playlist.Album.LibraryId ?? Ulid.Empty;
        Name = playlist.Album.Name;
        Type = "albums";
        Link = new($"/music/album/{Id}", UriKind.Relative);

        Tracks = playlist.Album.AlbumTrack
            .Where(albumTrack => albumTrack.Track.Duration != null)
            .DistinctBy(albumTrack => albumTrack.Track.Name.ToLower())
            .Count();
    }

    public CarouselResponseItemDto(Playlist playlist)
    {
        ColorPalette = playlist.ColorPalette;
        Cover = playlist.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Description = playlist.Description;
        Id = playlist.Id.ToString();
        Name = playlist.Name;
        Type = "playlists";
        Link = new($"/music/playlist/{Id}", UriKind.Relative);

        Tracks = playlist.Tracks
            .Where(playlistTrack => playlistTrack.Track.Duration != null)
            .DistinctBy(playlistTrack => playlistTrack.Track.Name.ToLower())
            .Count();
    }

    public CarouselResponseItemDto(Track track)
    {
        ColorPalette = track.ColorPalette;
        Cover = track.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Folder = track.Folder ?? "";
        Id = track.Id.ToString();
        Name = track.Name;
        Type = "tracks";
        Link = new($"/music/track/{Id}", UriKind.Relative);
    }
}
