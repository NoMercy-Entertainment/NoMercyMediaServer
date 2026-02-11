using System.ComponentModel.DataAnnotations.Schema;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Data.Repositories;
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
using NoMercy.Helpers;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using CarouselResponseItemDto = NoMercy.Data.Repositories.CarouselResponseItemDto;

namespace NoMercy.Api.Controllers.V1.Music;

[ApiController]
[ApiVersion(1.0)]
[Tags("Music")]
[Authorize]
[Route("api/v{version:apiVersion}/music")]
public class MusicController : BaseController
{
    private readonly MusicRepository _musicRepository;
    private readonly MediaContext _mediaContext;

    public MusicController(MusicRepository musicService, MediaContext mediaContext)
    {
        _musicRepository = musicService;
        _mediaContext = mediaContext;
    }

    [HttpGet]
    [Route("")]
    [Route("start")]
    public async Task<IActionResult> Index([FromQuery] PageRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view music");

        // Run queries sequentially - DbContext is not thread-safe so parallel execution causes errors
        TopMusicDto? favoriteArtist = (await _musicRepository.GetFavoriteArtistAsync(userId))
            .Select(artistTrack => new TopMusicDto(artistTrack))
            .GroupBy(a => a.Name)
            .MaxBy(g => g.Count())?
            .FirstOrDefault();

        TopMusicDto? favoriteAlbum = (await _musicRepository.GetFavoriteAlbumAsync(userId))
            .Select(albumTrack => new TopMusicDto(albumTrack))
            .GroupBy(a => a.Name)
            .MaxBy(g => g.Count())?
            .FirstOrDefault();

        TopMusicDto? favoritePlaylist = (await _musicRepository.GetFavoritePlaylistAsync(userId))
            .Select(musicPlay => new TopMusicDto(musicPlay))
            .GroupBy(a => a.Name)
            .MaxBy(g => g.Count())?
            .FirstOrDefault();

        List<CarouselResponseItemDto> favoriteArtists = await _musicRepository.GetFavoriteArtists(userId)
            .Select(artistUser => new CarouselResponseItemDto(artistUser))
            .Take(36)
            .ToListAsync();

        List<CarouselResponseItemDto> favoriteAlbums = await _musicRepository.GetFavoriteAlbums(userId)
            .Select(artistUser => new CarouselResponseItemDto(artistUser))
            .Take(36)
            .ToListAsync();

        List<CarouselResponseItemDto> playlists = await _musicRepository.GetCarouselPlaylistsAsync(userId);

        List<CarouselResponseItemDto> latestArtists = await _musicRepository.GetLatestArtists()
            .Select(artist => new CarouselResponseItemDto(artist))
            .Take(36)
            .ToListAsync();

        List<CarouselResponseItemDto> latestGenres = await _musicRepository.GetLatestGenres()
            .Select(genre => new CarouselResponseItemDto(genre))
            .Take(36)
            .ToListAsync();

        List<CarouselResponseItemDto> latestAlbums = await _musicRepository.GetLatestAlbums()
            .Select(artist => new CarouselResponseItemDto(artist))
            .Take(36)
            .ToListAsync();

        List<ComponentEnvelope> items = [];
        List<ComponentEnvelope> items2 = [];

        // Add favorite home cards
        if (favoriteArtist is not null && request.Version != "lolomo")
            items2.Add(Component.MusicHomeCard(new(favoriteArtist))
                .WithId("favorite-artist")
                .WithTitle("Most listened artist".Localize()));

        if (favoriteAlbum is not null && request.Version != "lolomo")
            items2.Add(Component.MusicHomeCard(new(favoriteAlbum))
                .WithId("favorite-album")
                .WithTitle("Most listened album".Localize()));

        if (favoritePlaylist is not null && request.Version != "lolomo")
            items2.Add(Component.MusicHomeCard(new(favoritePlaylist))
                .WithId("favorite-playlist")
                .WithTitle("Most listened playlist".Localize()));
        
        items.Add(Component.Container().WithItems(items2));
        
        // Add carousels
        items.Add(Component.Carousel()
            .WithId("favorite-artists")
            .WithTitle("Favorite Artists".Localize())
            .WithNavigation("", "favorite-albums")
            .WithItems(favoriteArtists.Select(item => Component.MusicCard(new MusicCardData(item)))));

        items.Add(Component.Carousel()
            .WithId("favorite-albums")
            .WithTitle("Favorite Albums".Localize())
            .WithNavigation("favorite-artists", "playlists")
            .WithItems(favoriteAlbums.Select(item => Component.MusicCard(new MusicCardData(item)))));

        items.Add(Component.Carousel()
            .WithId("playlists")
            .WithTitle("Playlists".Localize())
            .WithMoreLink("/music/playlists")
            .WithNavigation("favorite-albums", "artists")
            .WithItems(playlists.Select(item => Component.MusicCard(new MusicCardData(item)))));

        items.Add(Component.Carousel()
            .WithId("artists")
            .WithTitle("Artists".Localize())
            .WithMoreLink("/music/artists/_")
            .WithNavigation("playlists", "albums")
            .WithItems(latestArtists.Select(item => Component.MusicCard(new MusicCardData(item)))));

        items.Add(Component.Carousel()
            .WithId("albums")
            .WithTitle("Albums".Localize())
            .WithMoreLink("/music/albums/_")
            .WithNavigation("artists", "genres")
            .WithItems(latestAlbums.Select(item => Component.MusicCard(new MusicCardData(item)))));

        items.Add(Component.Carousel()
            .WithId("genres")
            .WithTitle("Genres".Localize())
            .WithMoreLink("/music/genres/letter/_")
            .WithNavigation("albums")
            .WithItems(latestGenres.Select(item => Component.MusicCard(new MusicCardData(item)))));

        return Ok(ComponentResponse.From(items));
    }
    
    [HttpPost]
    [Route("start/favorites")]
    public async Task<IActionResult> Favorites()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view music");

        TopMusicDto? favoriteArtist = (await _musicRepository.GetFavoriteArtistAsync(userId))
            .Select(artistTrack => new TopMusicDto(artistTrack))
            .GroupBy(a => a.Name)
            .MaxBy(g => g.Count())?
            .FirstOrDefault();

        TopMusicDto? favoriteAlbum = (await _musicRepository.GetFavoriteAlbumAsync(userId))
            .Select(albumTrack => new TopMusicDto(albumTrack))
            .GroupBy(a => a.Name)
            .MaxBy(g => g.Count())?
            .FirstOrDefault();

        TopMusicDto? favoritePlaylist = (await _musicRepository.GetFavoritePlaylistAsync(userId))
            .Select(musicPlay => new TopMusicDto(musicPlay))
            .GroupBy(a => a.Name)
            .MaxBy(g => g.Count())?
            .FirstOrDefault();

        List<ComponentEnvelope> favoriteItems = [];
        if (favoriteArtist is not null)
            favoriteItems.Add(Component.MusicHomeCard(new(favoriteArtist))
                .WithTitle("Most listened artist".Localize()));
        if (favoriteAlbum is not null)
            favoriteItems.Add(Component.MusicHomeCard(new(favoriteAlbum))
                .WithTitle("Most listened album".Localize()));
        if (favoritePlaylist is not null)
            favoriteItems.Add(Component.MusicHomeCard(new(favoritePlaylist))
                .WithTitle("Most listened playlist".Localize()));

        return Ok(ComponentResponse.From(
            Component.Container()
                .WithId("favorites")
                .WithNavigation("favorites", "favorite-artists")
                .WithUpdate("pageLoad", "/music/start/favorites")
                .WithItems(favoriteItems)));
    }

    [HttpPost]
    [Route("start/favorite-artists")]
    public async Task<IActionResult> FavoriteArtists([FromBody] CardRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view music");

        List<CarouselResponseItemDto> favoriteArtists = await _musicRepository.GetFavoriteArtists(userId)
            .Select(artistUser => new CarouselResponseItemDto(artistUser))
            .Take(36)
            .ToListAsync();

        return Ok(ComponentResponse.From(
            Component.Carousel()
                .WithId("favorite-artists")
                .WithNavigation("favorite-albums", "favorite-albums")
                .WithTitle("Favorite Artists".Localize())
                .WithUpdate("pageLoad", "/music/start/favorite-artists")
                .WithReplacing(request.ReplaceId)
                .WithItems(favoriteArtists.Select(item => Component.MusicCard(new MusicCardData(item))))));
    }

    [HttpPost]
    [Route("start/favorite-albums")]
    public async Task<IActionResult> FavoriteAlbums([FromBody] CardRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view music");

        List<CarouselResponseItemDto> favoriteAlbums = await _musicRepository.GetFavoriteAlbums(userId)
            .Select(artistUser => new CarouselResponseItemDto(artistUser))
            .Take(36)
            .ToListAsync();

        return Ok(ComponentResponse.From(
            Component.Carousel()
                .WithId("favorite-albums")
                .WithNavigation("favorite-artists", "playlists")
                .WithTitle("Favorite Albums".Localize())
                .WithUpdate("pageLoad", "/music/start/favorite-albums")
                .WithReplacing(request.ReplaceId)
                .WithItems(favoriteAlbums.Select(item => Component.MusicCard(new MusicCardData(item))))));
    }

    [HttpPost]
    [Route("start/playlists")]
    public async Task<IActionResult> Playlists([FromBody] CardRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view music");

        List<CarouselResponseItemDto> playlists = await _musicRepository.GetCarouselPlaylistsAsync(userId);
        
        return Ok(ComponentResponse.From(
            Component.Carousel()
                .WithId("playlists")
                .WithNavigation("favorite-albums", "artists")
                .WithTitle("Playlists".Localize())
                .WithMoreLink(new Uri("/music/start/playlists", UriKind.Relative))
                .WithUpdate("pageLoad", "/music/start/playlists")
                .WithReplacing(request.ReplaceId)
                .WithItems(playlists.Select(item => Component.MusicCard(new MusicCardData(item))))));
    }

    [NotMapped]
    public class SearchQueryRequest
    {
        [JsonProperty("query")] public string Query { get; set; } = string.Empty;
        [JsonProperty("type")] public string? Type { get; set; }
    }

    [HttpGet]
    [Route("search")]
    public async Task<IActionResult> Search([FromQuery] SearchQueryRequest request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to search music");

        string country = Country();

        string normalizedQuery = request.Query.NormalizeSearch();

        // Step 1: Get IDs using MusicRepository search methods
        List<Guid> artistIds = await _musicRepository.SearchArtistIdsAsync(normalizedQuery);
        List<Guid> albumIds = await _musicRepository.SearchAlbumIdsAsync(normalizedQuery);
        List<Guid> playlistIds = await _musicRepository.SearchPlaylistIdsAsync(normalizedQuery);
        List<Guid> trackIds = await _musicRepository.SearchTrackIdsAsync(normalizedQuery);
        // Step 2: Query full data using the IDs
        List<Artist> artists = _mediaContext.Artists
            .Where(artist => artistIds.Contains(artist.Id))
            .Include(artist => artist.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Track)
            .Include(artist => artist.AlbumArtist)
            .ThenInclude(albumArtist => albumArtist.Album)
            .ToList();

        List<Album> albums = _mediaContext.Albums
            .Where(album => albumIds.Contains(album.Id))
            .Include(album => album.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Track)
            .ThenInclude(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .Include(album => album.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Track)
            .ThenInclude(song => song.TrackUser)
            .ToList();

        List<Playlist> playlists = _mediaContext.Playlists
            .Where(playlist => playlistIds.Contains(playlist.Id))
            .Include(playlist => playlist.Tracks)
            .ThenInclude(playlistTrack => playlistTrack.Track)
            .ThenInclude(song => song.TrackUser)
            .ToList();

        List<Track> songs = _mediaContext.Tracks
            .Where(track => trackIds.Contains(track.Id))
            .Include(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .Include(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            .Include(track => track.PlaylistTrack)
            .ThenInclude(playlistTrack => playlistTrack.Playlist)
            .Include(track => track.TrackUser)
            .ToList();

        if (artists.Count == 0 && albums.Count == 0 && playlists.Count == 0 && songs.Count == 0)
            return NotFoundResponse("No results found");

        if (albums.Count > 0)
            foreach (Album album in albums)
                if (album.AlbumTrack.Count > 0)
                    foreach (IEnumerable<Artist> artist in album.AlbumTrack.Select(albumTrack => albumTrack.Track
                                 .ArtistTrack
                                 .Select(artistTrack => artistTrack.Artist)).ToList())
                        artists.AddRange(artist);

        if (playlists.Count > 0)
            foreach (Playlist playlist in playlists)
                if (playlist.Tracks.Count > 0)
                    foreach (IEnumerable<Artist> artist in playlist.Tracks
                                 .Select(playlistTrack => playlistTrack.Track.ArtistTrack
                                     .Select(artistTrack => artistTrack.Artist)).ToList())
                        artists.AddRange(artist);

        if (songs.Count > 0)
            foreach (Track song in songs)
            {
                if (song.ArtistTrack.Count > 0)
                    artists.AddRange(song.ArtistTrack.Select(artistTrack => artistTrack.Artist));
                if (song.AlbumTrack.Count > 0) albums.AddRange(song.AlbumTrack.Select(albumTrack => albumTrack.Album));
            }

        Track? topTrack = songs.FirstOrDefault();
        Artist? topArtist = artists.FirstOrDefault();
        Album? topAlbum = albums.FirstOrDefault();

        // Build TopResultCardData from the first match
        TopResultCardData? topResultData = topTrack != null ? new(topTrack)
            : topArtist != null ? new(topArtist)
            : topAlbum != null ? new TopResultCardData(topAlbum)
            : null;

        List<TrackRowData> songResults = songs
            .Take(6)
            .Select(track => new TrackRowData(track, country))
            .ToList();

        return Ok(ComponentResponse.From(
            Component.Container()
                .WithId("search-results")
                .WithItems(
                    Component.TopResultCard(topResultData!)
                        .WithId("top-result")
                        .WithTitle("Top Result".Localize())
                        .Build(),
                    Component.List()
                        .WithId("tracks")
                        .WithTitle("Tracks".Localize())
                        .WithItems(songResults.Select(track =>
                            Component.TrackRow(track)
                                .WithDisplayList(songResults)))
                    )
                .Build(),
            Component.Carousel()
                .WithId("artists")
                .WithTitle("Artist".Localize())
                .WithItems(artists
                    .GroupBy(artist => artist.Id)
                    .Select(group => group.First())
                    .Select(item => Component.MusicCard(new MusicCardData(item))))
                .Build(),
            Component.Carousel()
                .WithId("albums")
                .WithTitle("Albums".Localize())
                .WithItems(albums
                    .GroupBy(album => album.Id)
                    .Select(group => group.First())
                    .Select(item => Component.MusicCard(new MusicCardData(item))))
                .Build(),
            Component.Carousel()
                .WithId("playlists")
                .WithTitle("Playlists".Localize())
                .WithItems(playlists
                    .GroupBy(playlist => playlist.Id)
                    .Select(group => group.First())
                    .Select(item => Component.MusicCard(new MusicCardData(item)))))
        );
    }

    [HttpPost]
    [Route("search/{query}/{Type}")]
    public IActionResult TypeSearch(string query, string type)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to search music");

        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }

}