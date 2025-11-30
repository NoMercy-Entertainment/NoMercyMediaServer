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
    public async Task<IActionResult> Index()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view music");

        TopMusicDto? favoriteArtist = _musicRepository.GetFavoriteArtist(_mediaContext, userId)
            .AsEnumerable()
            .Select(artistTrack => new TopMusicDto(artistTrack))
            .GroupBy(a => a.Name)
            .MaxBy(g => g.Count())?
            .FirstOrDefault();

        TopMusicDto? favoriteAlbum = _musicRepository.GetFavoriteAlbum(_mediaContext, userId)
            .AsEnumerable()
            .Select(albumTrack => new TopMusicDto(albumTrack))
            .GroupBy(a => a.Name)
            .MaxBy(g => g.Count())?
            .FirstOrDefault();

        TopMusicDto? favoritePlaylist = _musicRepository.GetFavoritePlaylist(_mediaContext, userId)
            .AsEnumerable()
            .Select(musicPlay => new TopMusicDto(musicPlay))
            .GroupBy(a => a.Name)
            .MaxBy(g => g.Count())?
            .FirstOrDefault();

        List<CarouselResponseItemDto> favoriteArtists = await _musicRepository.GetFavoriteArtists(_mediaContext, userId)
            .Select(artistUser => new CarouselResponseItemDto(artistUser))
            .Take(36)
            .ToListAsync();

        List<CarouselResponseItemDto> favoriteAlbums = _musicRepository.GetFavoriteAlbums(_mediaContext, userId)
            .AsEnumerable()
            .Select(artistUser => new CarouselResponseItemDto(artistUser))
            .Take(36)
            .ToList();

        List<CarouselResponseItemDto> playlists = (await _musicRepository.GetPlaylists(_mediaContext, userId))
            .ToList();

        List<CarouselResponseItemDto> latestArtists = await _musicRepository.GetLatestArtists(_mediaContext)
            .Select(artist => new CarouselResponseItemDto(artist))
            .Take(36)
            .ToListAsync();

        List<CarouselResponseItemDto> latestAlbums = await _musicRepository.GetLatestAlbums(_mediaContext)
            .Select(artist => new CarouselResponseItemDto(artist))
            .Take(36)
            .ToListAsync();

        List<ComponentDto<dynamic>> topResults = [];

        if (favoriteArtist is not null)
            topResults.Add(new ComponentBuilder<dynamic>()
                .WithComponent("NMMusicHomeCard")
                .WithProps((props, _) => props
                    .WithId("favorite-artist")
                    .WithTitle("Most listened artist".Localize())
                    .WithData(favoriteArtist))
                .Build());

        if (favoriteAlbum is not null)
            topResults.Add(new ComponentBuilder<dynamic>()
                .WithComponent("NMMusicHomeCard")
                .WithProps((props, _) => props
                    .WithId("favorite-album")
                    .WithTitle("Most listened album".Localize())
                    .WithData(favoriteAlbum))
                .Build());

        if (favoritePlaylist is not null)
            topResults.Add(new ComponentBuilder<dynamic>()
                .WithComponent("NMMusicHomeCard")
                .WithProps((props, _) => props
                    .WithId("favorite-playlist")
                    .WithTitle("Most listened artist".Localize())
                    .WithData(favoritePlaylist))
                .Build());


        return Ok(new Render
        {
            Data =
            [
                new ComponentBuilder<dynamic>()
                    .WithComponent("NMContainer")
                    .WithUpdate("pageLoad", "/music/start/favorites")
                    .WithProps((props, _) => props
                        .WithId("favorites")
                        .WithNextId("favorite-artists")
                        .WithItems(topResults))
                    .Build(),
                
                new ComponentBuilder<CarouselResponseItemDto>()
                    .WithComponent("NMCarousel")
                    .WithUpdate("pageLoad", "/music/start/favorite-artists")
                    .WithProps((props, _) => props
                        .WithId("favorite-artists")
                        .WithPreviousId("favorite-albums")
                        .WithNextId("favorite-albums")
                        .WithTitle("Favorite Artists".Localize())
                        .WithItems(favoriteArtists
                            .Select(item => new ComponentDto<CarouselResponseItemDto>
                            {
                                Component = "NMMusicCard",
                                Props =
                                {
                                    Data = item
                                }
                            })))
                    .Build(),

                new ComponentBuilder<CarouselResponseItemDto>()
                    .WithComponent("NMCarousel")
                    .WithUpdate("pageLoad", "/music/start/favorite-albums")
                    .WithProps((props, _) => props
                        .WithId("favorite-albums")
                        .WithPreviousId("favorite-artists")
                        .WithNextId("playlists")
                        .WithTitle("Favorite Albums".Localize())
                        .WithItems(favoriteAlbums
                            .Select(item => new ComponentDto<CarouselResponseItemDto>
                            {
                                Component = "NMMusicCard",
                                Props =
                                {
                                    Data = item
                                }
                            })))
                    .Build(),

                new ComponentBuilder<CarouselResponseItemDto>()
                    .WithComponent("NMCarousel")
                    .WithUpdate("pageLoad", "/music/start/playlists")
                    .WithProps((props, _) => props
                        .WithId("playlists")
                        .WithPreviousId("favorite-albums")
                        .WithNextId("artists")
                        .WithTitle("Playlists".Localize())
                        .WithMoreLink(new("/music/playlists", UriKind.Relative))
                        .WithItems(playlists
                            .Select(item => new ComponentDto<CarouselResponseItemDto>
                            {
                                Component = "NMMusicCard",
                                Props =
                                {
                                    Data = item
                                }
                            })))
                    .Build(),

                new ComponentBuilder<CarouselResponseItemDto>()
                    .WithComponent("NMCarousel")
                    .WithUpdate("pageLoad", "/music/start/artists")
                    .WithProps((props, _) => props
                        .WithId("artists")
                        .WithPreviousId("playlists")
                        .WithNextId("albums")
                        .WithTitle("Artists".Localize())
                        .WithMoreLink(new("/music/artists/A", UriKind.Relative))
                        .WithItems(latestArtists
                            .Select(item => new ComponentDto<CarouselResponseItemDto>
                            {
                                Component = "NMMusicCard",
                                Props =
                                {
                                    Data = item
                                }
                            })))
                    .Build(),

                new ComponentBuilder<CarouselResponseItemDto>()
                    .WithComponent("NMCarousel")
                    .WithUpdate("pageLoad", "/music/start/albums")
                    .WithProps((props, _) => props
                        .WithId("albums")
                        .WithTitle("Albums".Localize())
                        .WithMoreLink(new("/music/albums/A", UriKind.Relative))
                        .WithItems(latestAlbums
                            .Select(item => new ComponentDto<CarouselResponseItemDto>
                            {
                                Component = "NMMusicCard",
                                Props =
                                {
                                    Data = item
                                }
                            })))
                    .Build()
            ]
        });
    }
    
    [HttpPost]
    [Route("start/favorites")]
    public async Task<IActionResult> Favorites()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view music");

        TopMusicDto? favoriteArtist = _musicRepository.GetFavoriteArtist(_mediaContext, userId)
            .AsEnumerable()
            .Select(artistTrack => new TopMusicDto(artistTrack))
            .GroupBy(a => a.Name)
            .MaxBy(g => g.Count())?
            .FirstOrDefault();

        TopMusicDto? favoriteAlbum = _musicRepository.GetFavoriteAlbum(_mediaContext, userId)
            .AsEnumerable()
            .Select(albumTrack => new TopMusicDto(albumTrack))
            .GroupBy(a => a.Name)
            .MaxBy(g => g.Count())?
            .FirstOrDefault();

        TopMusicDto? favoritePlaylist = _musicRepository.GetFavoritePlaylist(_mediaContext, userId)
            .AsEnumerable()
            .Select(musicPlay => new TopMusicDto(musicPlay))
            .GroupBy(a => a.Name)
            .MaxBy(g => g.Count())?
            .FirstOrDefault();

        return Ok(new Render
        {
            Data =
            [
                new ComponentBuilder<dynamic>()
                    .WithComponent("NMContainer")
                    .WithUpdate("pageLoad", "/music/start/favorites")
                    .WithProps((props, _) => props
                        .WithId("favorites")
                        .WithNextId("favorite-artists")
                        .WithItems([
                            favoriteArtist is not null
                                ? new ComponentBuilder<dynamic>()
                                    .WithComponent("NMMusicHomeCard")
                                    .WithProps((p, _) => p
                                        .WithId("favorite-artist")
                                        .WithTitle("Most listened artist".Localize())
                                        .WithData(favoriteArtist))
                                    .Build()
                                : new(),
                            favoriteAlbum is not null
                                ? new ComponentBuilder<dynamic>()
                                    .WithComponent("NMMusicHomeCard")
                                    .WithProps((p, _) => p
                                        .WithId("favorite-album")
                                        .WithTitle("Most listened album".Localize())
                                        .WithData(favoriteAlbum))
                                    .Build()
                                : new(),
                            favoritePlaylist is not null
                                ? new ComponentBuilder<dynamic>()
                                    .WithComponent("NMMusicHomeCard")
                                    .WithProps((p, _) => p
                                        .WithId("favorite-playlist")
                                        .WithTitle("Most listened artist".Localize())
                                        .WithData(favoritePlaylist))
                                    .Build()
                                : new()
                        ]))
                    .Build()
            ]
        });
    }

    [HttpPost]
    [Route("start/favorite-artists")]
    public async Task<IActionResult> FavoriteArtists([FromBody] CardRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view music");

        List<CarouselResponseItemDto> favoriteArtists = await _musicRepository.GetFavoriteArtists(_mediaContext, userId)
            .Select(artistUser => new CarouselResponseItemDto(artistUser))
            .Take(36)
            .ToListAsync();

        return Ok(new Render
        {
            Data =
            [
                new ComponentBuilder<CarouselResponseItemDto>()
                    .WithComponent("NMCarousel")
                    .WithUpdate("pageLoad", "/music/start/favorite-artists")
                    .WithReplacing(request.ReplaceId)
                    .WithProps((props, _) => props
                        .WithId("favorite-artists")
                        .WithPreviousId("favorite-albums")
                        .WithNextId("favorite-albums")
                        .WithTitle("Favorite Artists".Localize())
                        .WithItems(favoriteArtists
                            .Select(item => new ComponentDto<CarouselResponseItemDto>
                            {
                                Component = "NMMusicCard",
                                Props =
                                {
                                    Data = item
                                }
                            })))
                    .Build()
            ]
        });
    }

    [HttpPost]
    [Route("start/favorite-albums")]
    public async Task<IActionResult> FavoriteAlbums([FromBody] CardRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view music");

        List<CarouselResponseItemDto> favoriteAlbums = _musicRepository.GetFavoriteAlbums(_mediaContext, userId)
            .AsEnumerable()
            .Select(artistUser => new CarouselResponseItemDto(artistUser))
            .Take(36)
            .ToList();

        return Ok(new Render()
        {
            Data =
            [
                new ComponentBuilder<CarouselResponseItemDto>()
                    .WithComponent("NMCarousel")
                    .WithUpdate("pageLoad", "/music/start/favorite-albums")
                    .WithReplacing(request.ReplaceId)
                    .WithProps((props, _) => props
                        .WithId("favorite-albums")
                        .WithPreviousId("favorite-artists")
                        .WithNextId("playlists")
                        .WithTitle("Favorite Albums".Localize())
                        // .WithMoreLink(null)
                        .WithItems(favoriteAlbums
                            .Select(item => new ComponentDto<CarouselResponseItemDto>
                            {
                                Component = "NMMusicCard",
                                Props =
                                {
                                    Data = item
                                }
                            })))
                    .Build()
            ]
        });
    }

    [HttpPost]
    [Route("start/playlists")]
    public async Task<IActionResult> Playlists([FromBody] CardRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view music");

        List<CarouselResponseItemDto> playlists = (await _musicRepository.GetPlaylists(_mediaContext, userId))
            .ToList();

        return Ok(new Render
        {
            Data =
            [
                new ComponentBuilder<CarouselResponseItemDto>()
                    .WithComponent("NMCarousel")
                    .WithUpdate("pageLoad", "/music/start/playlists")
                    .WithReplacing(request.ReplaceId)
                    .WithProps((props, _) => props
                        .WithId("playlists")
                        .WithPreviousId("favorite-albums")
                        .WithNextId("artists")
                        .WithTitle("Playlists".Localize())
                        .WithMoreLink(new("/music/start/playlists", UriKind.Relative))
                        .WithItems(playlists
                            .Select(item => new ComponentDto<CarouselResponseItemDto>
                            {
                                Component = "NMMusicCard",
                                Props =
                                {
                                    Data = item
                                }
                            })))
                    .Build()
            ]
        });
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

    [HttpPost]
    [Route("coverimage")]
    public IActionResult CoverImage()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view cover images");

        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }

    [HttpPost]
    [Route("images")]
    public IActionResult Images()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view images");

        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }
}