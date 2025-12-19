using System.ComponentModel.DataAnnotations.Schema;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Api.Controllers.V1.Music.DTO;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.NmSystem.Extensions;
using CarouselResponseItemDto = NoMercy.Api.Controllers.V1.Media.DTO.CarouselResponseItemDto;

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

        // Step 1: Get IDs using MusicRepository search methods
        List<Guid> artistIds = _musicRepository.SearchArtistIds(_mediaContext, normalizedQuery);
        List<Guid> albumIds = _musicRepository.SearchAlbumIds(_mediaContext, normalizedQuery);
        List<Guid> playlistIds = _musicRepository.SearchPlaylistIds(_mediaContext, normalizedQuery);
        List<Guid> trackIds = _musicRepository.SearchTrackIds(_mediaContext, normalizedQuery);
        // Step 2: Query full data using the IDs
        MediaContext mediaContext = new();
        List<Artist> artists = mediaContext.Artists
            .Where(artist => artistIds.Contains(artist.Id))
            .Include(artist => artist.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Track)
            .Include(artist => artist.AlbumArtist)
            .ThenInclude(albumArtist => albumArtist.Album)
            .ToList();

        List<Album> albums = mediaContext.Albums
            .Where(album => albumIds.Contains(album.Id))
            .Include(album => album.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Track)
            .ThenInclude(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .Include(album => album.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Track)
            .ThenInclude(song => song.TrackUser)
            .ToList();

        List<Playlist> playlists = mediaContext.Playlists
            .Where(playlist => playlistIds.Contains(playlist.Id))
            .Include(playlist => playlist.Tracks)
            .ThenInclude(playlistTrack => playlistTrack.Track)
            .ThenInclude(song => song.TrackUser)
            .ToList();

        List<Track> songs = mediaContext.Tracks
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

        Guid id = topTrack?.Id ?? topArtist?.Id ?? topAlbum?.Id ?? Guid.Empty;
        string title = topTrack?.Name ?? topArtist?.Name ?? topAlbum?.Name ?? "Top Result";
        string? cover = topTrack?.Cover ?? topArtist?.Cover ?? topAlbum?.Cover;
        string type = topTrack != null ? "Track" :
            topArtist != null ? "Artist" :
            topAlbum != null ? "Albums" : "Top Result";
        List<ArtistDto> artistsList =
            topTrack?.ArtistTrack.Select(artistTrack => new ArtistDto(artistTrack, country)).ToList() ??
            [];
        Track? topTrackItem = topTrack?.ArtistTrack.FirstOrDefault()?.Track;
        if (topTrackItem != null)
            topTrackItem = topArtist?.ArtistTrack.FirstOrDefault()?.Track;
        if (topTrackItem != null)
            topTrackItem = topAlbum?.AlbumTrack.FirstOrDefault()?.Track;

        Uri link = new("/", UriKind.Relative);
        if (topTrack != null)
            link = new($"/music/tracks/{topTrack.Id}", UriKind.Relative);
        else if (topArtist != null)
            link = new($"/music/artist/{topArtist.Id}", UriKind.Relative);
        else if (topAlbum != null)
            link = new($"/music/album/{topAlbum.Id}", UriKind.Relative);

        Dictionary<string, object?> topResult = new()
        {
            { "id", id },
            { "title", title },
            {
                "cover", cover is not null
                    ? new Uri($"/images/music{cover}", UriKind.Relative).ToString()
                    : null
            },
            { "link", link },
            { "type", type },
            { "artists", artistsList },
            { "width", "33.33333%" },
            { "track", topTrackItem is not null ? new ArtistTrackDto(topTrackItem) : null }
        };

        List<ArtistTrackDto> songResults = songs
            .Take(6)
            .Select(track => new ArtistTrackDto(track, country))
            .ToList();

        return Ok(new Render
        {
            Data =
            [
                new ComponentDto<dynamic>
                {
                    Component = "NMContainer",
                    Props =
                    {
                        Items =
                        [
                            new()
                            {
                                Component = "NMTopResultCard",
                                Props =
                                {
                                    Title = "Top Result",
                                    Data = topResult
                                }
                            },
                            new()
                            {
                                Component = "NMList",
                                Props =
                                {
                                    Title = "Tracks",
                                    Items = songResults.Select(track => new ComponentDto<dynamic>
                                    {
                                        Component = "NMTrackRow",
                                        Props =
                                        {
                                            Data = track,
                                            DisplayList = songResults
                                        }
                                    })
                                }
                            }
                        ]
                    }
                },

                new ComponentDto<CarouselResponseItemDto>
                {
                    Component = "NMCarousel",
                    Props =
                    {
                        Title = "Artists",
                        Items = artists
                            .GroupBy(artist => artist.Id)
                            .Select(group => group.First())
                            .Select(item => new ComponentDto<CarouselResponseItemDto>
                            {
                                Component = "NMMusicCard",
                                Props =
                                {
                                    Data = new(item)
                                }
                            })
                    }
                },
                new ComponentDto<CarouselResponseItemDto>
                {
                    Component = "NMCarousel",
                    Props =
                    {
                        Title = "Albums",
                        Items = albums
                            .GroupBy(album => album.Id)
                            .Select(group => group.First())
                            .Select(item => new ComponentDto<CarouselResponseItemDto>
                            {
                                Component = "NMMusicCard",
                                Props =
                                {
                                    Data = new(item)
                                }
                            })
                    }
                },
                new ComponentDto<CarouselResponseItemDto>
                {
                    Component = "NMCarousel",
                    Props =
                    {
                        Title = "Playlists",
                        Items = playlists
                            .GroupBy(playlist => playlist.Id)
                            .Select(group => group.First())
                            .Select(item => new ComponentDto<CarouselResponseItemDto>
                            {
                                Component = "NMMusicCard",
                                Props =
                                {
                                    Data = new(item)
                                }
                            })
                    }
                }
            ]
        });
    }

    [HttpGet("music/tv")]
    public async Task<IActionResult> SearchTvMusic([FromQuery] SearchQueryRequest request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to perform searches");
        
        string normalizedQuery = request.Query.NormalizeSearch();
        string country = Country();
        Guid userId = User.UserId();

        // Step 1: Get IDs using MusicRepository search methods
        List<Guid> artistIds = _musicRepository.SearchArtistIds(_mediaContext, normalizedQuery);
        List<Guid> albumIds = _musicRepository.SearchAlbumIds(_mediaContext, normalizedQuery);
        List<Guid> playlistIds = _musicRepository.SearchPlaylistIds(_mediaContext, normalizedQuery);
        List<Guid> trackIds = _musicRepository.SearchTrackIds(_mediaContext, normalizedQuery);
        // Step 2: Query full data using the IDs
        MediaContext mediaContext = new();
        List<Artist> artists = await mediaContext.Artists
            .Where(artist => artistIds.Contains(artist.Id))
            .Include(artist => artist.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Track)
            .Include(artist => artist.AlbumArtist)
            .ThenInclude(albumArtist => albumArtist.Album)
            .ToListAsync();

        List<Album> albums = await mediaContext.Albums
            .Where(album => albumIds.Contains(album.Id))
            .Include(album => album.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Track)
            .ThenInclude(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .Include(album => album.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Track)
            .ThenInclude(song => song.TrackUser)
            .ToListAsync();

        List<Playlist> playlists = await mediaContext.Playlists
            .Where(playlist => playlistIds.Contains(playlist.Id))
            .Include(playlist => playlist.Tracks)
            .ThenInclude(playlistTrack => playlistTrack.Track)
            .ThenInclude(song => song.TrackUser)
            .ToListAsync();

        List<Track> songs = await mediaContext.Tracks
            .Where(track => trackIds.Contains(track.Id))
            .Include(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .Include(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            .Include(track => track.PlaylistTrack)
            .ThenInclude(playlistTrack => playlistTrack.Playlist)
            .Include(track => track.TrackUser)
            .ToListAsync();

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
        
        return Ok(new Render
        {
            Data =
            [
                new ComponentBuilder<MusicSearchResponseItemDto>()
                    .WithComponent("NMGrid")
                    .WithProps((props, _) => props
                        .WithProperties(new()
                        {
                            { "columns", 4 },
                            { "spacing", 16 }
                        })
                        .WithItems([
                            ..artists
                                .GroupBy(album => album.Id)
                                .Select(group => group.First())
                                .OrderBy(artist => artist.Name)
                                .Select(item =>
                                    new ComponentBuilder<MusicSearchResponseItemDto>()
                                        .WithComponent("NMMusicCard")
                                        .WithProps((p, _) => p
                                            .WithData(new(item))
                                            .WithWatch())
                                        .Build()),
                            
                            ..albums
                                .GroupBy(album => album.Id)
                                .Select(group => group.First())
                                .OrderBy(artist => artist.Name)
                                .Select(item =>
                                    new ComponentBuilder<MusicSearchResponseItemDto>()
                                        .WithComponent("NMMusicCard")
                                        .WithProps((p, _) => p
                                            .WithData(new(item))
                                            .WithWatch())
                                        .Build())
                            
                            // ..songs
                            //     .GroupBy(playlist => playlist.Id)
                            //     .Select(group => group.First())
                            //     .Select(item => new ComponentDto<TracksResponseItemDto>
                            //     {
                            //         Component = "NMMusicCard",
                            //         Props =
                            //         {
                            //             Data = new(item, country)
                            //         }
                            //     }),
                            //
                            // ..playlists
                            //     .GroupBy(playlist => playlist.Id)
                            //     .Select(group => group.First())
                            //     .Select(item => new ComponentDto<MusicPlaylistResponseItemDto>
                            //     {
                            //         Component = "NMMusicCard",
                            //         Props =
                            //         {
                            //             Data = new(item)
                            //         }
                            //     }),
                        ])
                ).Build()
                ]
        });
    }

    [HttpGet("video")]
    public async Task<IActionResult> SearchVideo([FromQuery] SearchQueryRequest request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to perform searches");

        return Ok(new Render
        {
            Data =
            [
                new ComponentDto<dynamic>
                {
                    Component = "NMContainer",
                    Props =
                    {
                        Items =
                        [
                        ]
                    }
                }
            ]
        });
    }

    [HttpGet("video/tv")]
    public async Task<IActionResult> SearchTvVideo([FromQuery] SearchQueryRequest request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to perform searches");

        return Ok(new Render
        {
            Data =
            [
                new ComponentDto<dynamic>
                {
                    Component = "NMContainer",
                    Props =
                    {
                        Items =
                        [
                        ]
                    }
                }
            ]
        });
    }

    [NotMapped]
    public class SearchQueryRequest
    {
        [JsonProperty("query")] public string Query { get; set; } = string.Empty;
        [JsonProperty("type")] public string? Type { get; set; }
    }
}