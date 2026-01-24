using System.ComponentModel.DataAnnotations.Schema;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.Media.DTO.Components;
using NoMercy.Api.Controllers.V1.Music.DTO;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags("Media Search")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/search")]
public class SearchController : BaseController
{
    private readonly MusicRepository _musicRepository;
    private readonly MediaContext _mediaContext;

    public SearchController(MusicRepository musicService, MediaContext mediaContext)
    {
        _musicRepository = musicService;
        _mediaContext = mediaContext;
    }
    
    [HttpGet("music")]
    public async Task<IActionResult> SearchMusic([FromQuery] SearchQueryRequest request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to perform searches");

        string country = Country();

        string normalizedQuery = request.Query.NormalizeSearch();

        // Step 1: Get IDs using MusicRepository search methods in parallel
        Task<List<Guid>> artistIdsTask = Task.Run(() => _musicRepository.SearchArtistIds(normalizedQuery));
        Task<List<Guid>> albumIdsTask = Task.Run(() => _musicRepository.SearchAlbumIds(normalizedQuery));
        Task<List<Guid>> playlistIdsTask = Task.Run(() => _musicRepository.SearchPlaylistIds(normalizedQuery));
        Task<List<Guid>> trackIdsTask = Task.Run(() => _musicRepository.SearchTrackIds(normalizedQuery));

        await Task.WhenAll(artistIdsTask, albumIdsTask, playlistIdsTask, trackIdsTask);

        List<Guid> artistIds = artistIdsTask.Result;
        List<Guid> albumIds = albumIdsTask.Result;
        List<Guid> playlistIds = playlistIdsTask.Result;
        List<Guid> trackIds = trackIdsTask.Result;

        // Step 2: Query full data using the IDs in parallel - each task needs its own MediaContext for thread safety
        Task<List<Artist>> artistsTask = Task.Run(() =>
        {
            MediaContext context = new();
            return context.Artists
                .Where(artist => artistIds.Contains(artist.Id))
                .Include(artist => artist.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Track)
                .Include(artist => artist.AlbumArtist)
                .ThenInclude(albumArtist => albumArtist.Album)
                .ToList();
        });

        Task<List<Album>> albumsTask = Task.Run(() =>
        {
            MediaContext context = new();
            return context.Albums
                .Where(album => albumIds.Contains(album.Id))
                .Include(album => album.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Track)
                .ThenInclude(track => track.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Artist)
                .Include(album => album.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Track)
                .ThenInclude(song => song.TrackUser)
                .ToList();
        });

        Task<List<Playlist>> playlistsTask = Task.Run(() =>
        {
            MediaContext context = new();
            return context.Playlists
                .Where(playlist => playlistIds.Contains(playlist.Id))
                .Include(playlist => playlist.Tracks)
                .ThenInclude(playlistTrack => playlistTrack.Track)
                .ThenInclude(song => song.TrackUser)
                .ToList();
        });

        Task<List<Track>> songsTask = Task.Run(() =>
        {
            MediaContext context = new();
            return context.Tracks
                .Where(track => trackIds.Contains(track.Id))
                .Include(track => track.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Artist)
                .Include(track => track.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Album)
                .Include(track => track.PlaylistTrack)
                .ThenInclude(playlistTrack => playlistTrack.Playlist)
                .Include(track => track.TrackUser)
                .ToList();
        });

        await Task.WhenAll(artistsTask, albumsTask, playlistsTask, songsTask);

        List<Artist> artists = artistsTask.Result;
        List<Album> albums = albumsTask.Result;
        List<Playlist> playlists = playlistsTask.Result;
        List<Track> songs = songsTask.Result;

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
                        .WithProperties(new()
                        {
                            { "paddingTop", 0 },
                            { "paddingBottom", 0 },
                            { "paddingStart", 0 },
                            { "paddingEnd", 0 }
                        })
                        .WithTitle("Tracks".Localize())
                        .WithItems(songResults
                            .Select(track => Component
                                .TrackRow(track)
                                .WithProperties(new()
                                {
                                    { "paddingTop", 0 },
                                    { "paddingBottom", 0 },
                                    { "paddingStart", 0 },
                                    { "paddingEnd", 0 }
                                })
                                .WithDisplayList(songResults)))),
            
            Component.Carousel()
                .WithId("artists")
                .WithTitle("Artist".Localize())
                .WithItems(artists
                    .GroupBy(artist => artist.Id)
                    .Select(group => group.First())
                    .Select(item => Component.MusicCard(new ArtistsResponseItemDto(item)))),
            Component.Carousel()
                .WithId("albums")
                .WithTitle("Albums".Localize())
                .WithItems(albums
                    .GroupBy(album => album.Id)
                    .Select(group => group.First())
                    .Select(item => Component.MusicCard(new ArtistsResponseItemDto(item)))),
            Component.Carousel()
                .WithId("playlists")
                .WithTitle("Playlists".Localize())
                .WithItems(playlists
                    .GroupBy(playlist => playlist.Id)
                    .Select(group => group.First())
                    .Select(item => Component.MusicCard(new PlaylistResponseItemDto(item))))
            ));
    }

    [HttpGet("music/tv")]
    public async Task<IActionResult> SearchTvMusic([FromQuery] SearchQueryRequest request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to perform searches");
        
        string normalizedQuery = request.Query.NormalizeSearch();
        string country = Country();
        Guid userId = User.UserId();

        // Step 1: Get IDs using MusicRepository search methods in parallel
        Task<List<Guid>> artistIdsTask = Task.Run(() => _musicRepository.SearchArtistIds(normalizedQuery));
        Task<List<Guid>> albumIdsTask = Task.Run(() => _musicRepository.SearchAlbumIds(normalizedQuery));
        Task<List<Guid>> playlistIdsTask = Task.Run(() => _musicRepository.SearchPlaylistIds(normalizedQuery));
        Task<List<Guid>> trackIdsTask = Task.Run(() => _musicRepository.SearchTrackIds(normalizedQuery));

        await Task.WhenAll(artistIdsTask, albumIdsTask, playlistIdsTask, trackIdsTask);

        List<Guid> artistIds = artistIdsTask.Result;
        List<Guid> albumIds = albumIdsTask.Result;
        List<Guid> playlistIds = playlistIdsTask.Result;
        List<Guid> trackIds = trackIdsTask.Result;

        // Step 2: Query full data using the IDs in parallel - each task needs its own MediaContext for thread safety
        Task<List<Artist>> artistsTask = Task.Run(async () =>
        {
            MediaContext context = new();
            return await context.Artists
                .Where(artist => artistIds.Contains(artist.Id))
                .Include(artist => artist.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Track)
                .Include(artist => artist.AlbumArtist)
                .ThenInclude(albumArtist => albumArtist.Album)
                .ToListAsync();
        });

        Task<List<Album>> albumsTask = Task.Run(async () =>
        {
            MediaContext context = new();
            return await context.Albums
                .Where(album => albumIds.Contains(album.Id))
                .Include(album => album.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Track)
                .ThenInclude(track => track.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Artist)
                .Include(album => album.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Track)
                .ThenInclude(song => song.TrackUser)
                .ToListAsync();
        });

        Task<List<Playlist>> playlistsTask = Task.Run(async () =>
        {
            MediaContext context = new();
            return await context.Playlists
                .Where(playlist => playlistIds.Contains(playlist.Id))
                .Include(playlist => playlist.Tracks)
                .ThenInclude(playlistTrack => playlistTrack.Track)
                .ThenInclude(song => song.TrackUser)
                .ToListAsync();
        });

        Task<List<Track>> songsTask = Task.Run(async () =>
        {
            MediaContext context = new();
            return await context.Tracks
                .Where(track => trackIds.Contains(track.Id))
                .Include(track => track.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Artist)
                .Include(track => track.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Album)
                .Include(track => track.PlaylistTrack)
                .ThenInclude(playlistTrack => playlistTrack.Playlist)
                .Include(track => track.TrackUser)
                .ToListAsync();
        });

        await Task.WhenAll(artistsTask, albumsTask, playlistsTask, songsTask);

        List<Artist> artists = artistsTask.Result;
        List<Album> albums = albumsTask.Result;
        List<Playlist> playlists = playlistsTask.Result;
        List<Track> songs = songsTask.Result;

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
        
        List<ComponentEnvelope> musicCards =
        [
            ..artists
                .GroupBy(artist => artist.Id)
                .Select(group => group.First())
                .OrderBy(artist => artist.Name)
                .Select(item => Component.MusicCard(new ArtistsResponseItemDto(item))),
            ..albums
                .GroupBy(album => album.Id)
                .Select(group => group.First())
                .OrderBy(album => album.Name)
                .Select(item => Component.MusicCard(new AlbumsResponseItemDto(item)))
        ];

        return Ok(ComponentResponse.From(
            Component.Grid()
                .WithId("tv-music-search")
                .WithProperties(new()
                {
                    { "columns", 4 },
                    { "spacing", 16 }
                })
                .WithItems(musicCards)
                .Build()));
    }

    [HttpGet("video")]
    public async Task<IActionResult> SearchVideo([FromQuery] SearchQueryRequest request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to perform searches");
        
        string country = Country();

        string normalizedQuery = request.Query.NormalizeSearch();

        // Run TV and movie queries in parallel
        Task<List<Tv>> tvsTask = Task.Run(async () =>
        {
            MediaContext context = new();
            return await context.Tvs
                .Where(tv => tv.Title.ToLower().Contains(normalizedQuery))
                .ToListAsync();
        });

        Task<List<Movie>> moviesTask = Task.Run(async () =>
        {
            MediaContext context = new();
            return await context.Movies
                .Where(movie => movie.Title.ToLower().Contains(normalizedQuery))
                .ToListAsync();
        });

        await Task.WhenAll(tvsTask, moviesTask);

        List<Tv> tvs = tvsTask.Result;
        List<Movie> movies = moviesTask.Result;

        List<CardData> cardItems = tvs.Concat<dynamic>(movies)
            .Cast<dynamic>()
            .OrderBy(item => item is Tv tv ? tv.Title : ((Movie)item).Title)
            .Select(item => new CardData(item as dynamic, country))
            .ToList();

        ComponentEnvelope response = Component.Grid()
            .WithItems(cardItems.Select(item => Component.Card()
                .WithData(item)
                .Build()))
            .Build();

        return Ok(ComponentResponse.From(response));
    }

    [HttpGet("video/tv")]
    public async Task<IActionResult> SearchTvVideo([FromQuery] SearchQueryRequest request)
    {

        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to perform searches");

        string country = Country();

        string normalizedQuery = request.Query.NormalizeSearch();

        // Run TV and movie queries in parallel
        Task<List<Tv>> tvsTask = Task.Run(async () =>
        {
            MediaContext context = new();
            return await context.Tvs
                .Where(tv => tv.Title.ToLower().Contains(normalizedQuery))
                .ToListAsync();
        });

        Task<List<Movie>> moviesTask = Task.Run(async () =>
        {
            MediaContext context = new();
            return await context.Movies
                .Where(movie => movie.Title.ToLower().Contains(normalizedQuery))
                .ToListAsync();
        });

        await Task.WhenAll(tvsTask, moviesTask);

        List<Tv> tvs = tvsTask.Result;
        List<Movie> movies = moviesTask.Result;

        List<CardData> cardItems = tvs.Concat<dynamic>(movies)
            .OrderBy(item => item is Tv tv ? tv.Title : ((Movie)item).Title)
            .Select(item => new CardData(item, country))
            .ToList();

        ComponentEnvelope response = Component.Grid()
            .WithItems(cardItems.Select(item => Component.Card()
                .WithData(item)
                .Build()))
            .Build();

        return Ok(ComponentResponse.From(response));
    }

    [NotMapped]
    public class SearchQueryRequest
    {
        [JsonProperty("query")] public string Query { get; set; } = string.Empty;
        [JsonProperty("type")] public string? Type { get; set; }
    }
}