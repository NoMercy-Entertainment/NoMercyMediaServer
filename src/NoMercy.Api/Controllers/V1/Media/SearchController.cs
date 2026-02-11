using System.ComponentModel.DataAnnotations.Schema;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Api.DTOs.Music;
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
using NoMercy.Helpers.Extensions;
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
    private readonly IDbContextFactory<MediaContext> _contextFactory;

    public SearchController(MusicRepository musicService, IDbContextFactory<MediaContext> contextFactory)
    {
        _musicRepository = musicService;
        _contextFactory = contextFactory;
    }

    [HttpGet("music")]
    [ResponseCache(NoStore = true)]
    public async Task<IActionResult> SearchMusic([FromQuery] SearchQueryRequest request, CancellationToken ct = default)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to perform searches");

        string country = Country();

        string normalizedQuery = request.Query.NormalizeSearch();

        // Step 1: Get IDs sequentially (MusicRepository uses a single scoped DbContext, not thread-safe)
        List<Guid> artistIds = await _musicRepository.SearchArtistIdsAsync(normalizedQuery, ct);
        List<Guid> albumIds = await _musicRepository.SearchAlbumIdsAsync(normalizedQuery, ct);
        List<Guid> playlistIds = await _musicRepository.SearchPlaylistIdsAsync(normalizedQuery, ct);
        List<Guid> trackIds = await _musicRepository.SearchTrackIdsAsync(normalizedQuery, ct);

        // Step 2: Query full data using the IDs in parallel - each task needs its own MediaContext for thread safety
        Task<List<Artist>> artistsTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await context.Artists
                .Where(artist => artistIds.Contains(artist.Id))
                .Include(artist => artist.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Track)
                .Include(artist => artist.AlbumArtist)
                .ThenInclude(albumArtist => albumArtist.Album)
                .ToListAsync(ct);
        });

        Task<List<Album>> albumsTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await context.Albums
                .Where(album => albumIds.Contains(album.Id))
                .Include(album => album.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Track)
                .ThenInclude(track => track.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Artist)
                .Include(album => album.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Track)
                .ThenInclude(song => song.TrackUser)
                .ToListAsync(ct);
        });

        Task<List<Playlist>> playlistsTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await context.Playlists
                .Where(playlist => playlistIds.Contains(playlist.Id))
                .Include(playlist => playlist.Tracks)
                .ThenInclude(playlistTrack => playlistTrack.Track)
                .ThenInclude(song => song.TrackUser)
                .ToListAsync(ct);
        });

        Task<List<Track>> songsTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await context.Tracks
                .Where(track => trackIds.Contains(track.Id))
                .Include(track => track.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Artist)
                .Include(track => track.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Album)
                .Include(track => track.PlaylistTrack)
                .ThenInclude(playlistTrack => playlistTrack.Playlist)
                .Include(track => track.TrackUser)
                .ToListAsync(ct);
        });

        await Task.WhenAll(artistsTask, albumsTask, playlistsTask, songsTask);

        List<Artist> artists = await artistsTask;
        List<Album> albums = await albumsTask;
        List<Playlist> playlists = await playlistsTask;
        List<Track> songs = await songsTask;

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
    public async Task<IActionResult> SearchTvMusic([FromQuery] SearchQueryRequest request, CancellationToken ct = default)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to perform searches");

        string normalizedQuery = request.Query.NormalizeSearch();
        string country = Country();
        Guid userId = User.UserId();

        // Step 1: Get IDs sequentially (MusicRepository uses a single scoped DbContext, not thread-safe)
        List<Guid> artistIds = await _musicRepository.SearchArtistIdsAsync(normalizedQuery, ct);
        List<Guid> albumIds = await _musicRepository.SearchAlbumIdsAsync(normalizedQuery, ct);
        List<Guid> playlistIds = await _musicRepository.SearchPlaylistIdsAsync(normalizedQuery, ct);
        List<Guid> trackIds = await _musicRepository.SearchTrackIdsAsync(normalizedQuery, ct);

        // Step 2: Query full data using the IDs in parallel - each task needs its own MediaContext for thread safety
        Task<List<Artist>> artistsTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await context.Artists
                .Where(artist => artistIds.Contains(artist.Id))
                .Include(artist => artist.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Track)
                .Include(artist => artist.AlbumArtist)
                .ThenInclude(albumArtist => albumArtist.Album)
                .ToListAsync(ct);
        });

        Task<List<Album>> albumsTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await context.Albums
                .Where(album => albumIds.Contains(album.Id))
                .Include(album => album.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Track)
                .ThenInclude(track => track.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Artist)
                .Include(album => album.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Track)
                .ThenInclude(song => song.TrackUser)
                .ToListAsync(ct);
        });

        Task<List<Playlist>> playlistsTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await context.Playlists
                .Where(playlist => playlistIds.Contains(playlist.Id))
                .Include(playlist => playlist.Tracks)
                .ThenInclude(playlistTrack => playlistTrack.Track)
                .ThenInclude(song => song.TrackUser)
                .ToListAsync(ct);
        });

        Task<List<Track>> songsTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await context.Tracks
                .Where(track => trackIds.Contains(track.Id))
                .Include(track => track.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Artist)
                .Include(track => track.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Album)
                .Include(track => track.PlaylistTrack)
                .ThenInclude(playlistTrack => playlistTrack.Playlist)
                .Include(track => track.TrackUser)
                .ToListAsync(ct);
        });

        await Task.WhenAll(artistsTask, albumsTask, playlistsTask, songsTask);

        List<Artist> artists = await artistsTask;
        List<Album> albums = await albumsTask;
        List<Playlist> playlists = await playlistsTask;
        List<Track> songs = await songsTask;

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
    [ResponseCache(NoStore = true)]
    public async Task<IActionResult> SearchVideo([FromQuery] SearchQueryRequest request, CancellationToken ct = default)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to perform searches");

        string country = Country();

        string normalizedQuery = request.Query.NormalizeSearch();

        // Run TV and movie queries in parallel
        Task<List<Tv>> tvsTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await context.Tvs
                .Where(tv => tv.Title.ToLower().Contains(normalizedQuery))
                .ToListAsync(ct);
        });

        Task<List<Movie>> moviesTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await context.Movies
                .Where(movie => movie.Title.ToLower().Contains(normalizedQuery))
                .ToListAsync(ct);
        });

        await Task.WhenAll(tvsTask, moviesTask);

        List<Tv> tvs = await tvsTask;
        List<Movie> movies = await moviesTask;

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
    public async Task<IActionResult> SearchTvVideo([FromQuery] SearchQueryRequest request, CancellationToken ct = default)
    {

        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to perform searches");

        string country = Country();

        string normalizedQuery = request.Query.NormalizeSearch();

        // Run TV and movie queries in parallel
        Task<List<Tv>> tvsTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await context.Tvs
                .Where(tv => tv.Title.ToLower().Contains(normalizedQuery))
                .ToListAsync(ct);
        });

        Task<List<Movie>> moviesTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await context.Movies
                .Where(movie => movie.Title.ToLower().Contains(normalizedQuery))
                .ToListAsync(ct);
        });

        await Task.WhenAll(tvsTask, moviesTask);

        List<Tv> tvs = await tvsTask;
        List<Movie> movies = await moviesTask;

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
