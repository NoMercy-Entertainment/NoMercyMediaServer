using System.ComponentModel.DataAnnotations.Schema;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Api.Controllers.V1.Music.DTO;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.V1.Music;

[ApiController]
[ApiVersion(1.0)]
[Tags("Music")]
[Authorize]
[Route("api/v{version:apiVersion}/music")]
public class MusicController : BaseController
{
    [HttpGet]
    [Route("")]
    [Route("start")]
    public async Task<IActionResult> Index()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view music");

        MediaContext mediaContext = new();

        TopMusicDto? favoriteArtist = CarouselResponseItemDto.GetFavoriteArtist(mediaContext, userId);
        TopMusicDto? favoriteAlbum = CarouselResponseItemDto.GetFavoriteAlbum(mediaContext, userId);
        TopMusicDto? favoritePlaylist = CarouselResponseItemDto.GetFavoritePlaylist(mediaContext, userId);

        List<CarouselResponseItemDto> favoriteArtists = await CarouselResponseItemDto.GetFavoriteArtists(mediaContext, userId);
        List<CarouselResponseItemDto> favoriteAlbums = await CarouselResponseItemDto.GetFavoriteAlbums(mediaContext, userId);

        List<CarouselResponseItemDto> playlists = await CarouselResponseItemDto.GetPlaylists(mediaContext, userId);
        List<CarouselResponseItemDto> latestArtists = await CarouselResponseItemDto.GetLatestArtists(mediaContext);
        List<CarouselResponseItemDto> latestAlbums = await CarouselResponseItemDto.GetLatestAlbums(mediaContext);

        return Ok(new Render
        {
            Data = [

                new ComponentDto<dynamic>
                {
                    Component = "NMContainer",
                    Props =
                    {
                        Items = [
                            new()
                            {
                                Component = "NMMusicHomeCard",
                                Props =
                                {
                                    Title = "Most listened artist",
                                    Data = favoriteArtist
                                }
                            },
                            new()
                            {
                                Component = "NMMusicHomeCard",
                                Props =
                                {
                                    Title = "Most listened album",
                                    Data = favoriteAlbum
                                }
                            },
                            new()
                            {
                                Component = "NMMusicHomeCard",
                                Props =
                                {
                                    Title = "Most listened playlist",
                                    Data = favoritePlaylist
                                }
                            },
                        ]
                    }
                },

                new ComponentDto<CarouselResponseItemDto>
                {
                    Component = "NMCarousel",
                    Props =
                    {
                        Title = "Favorite Artists",
                        Items = favoriteArtists
                            .Select(item => new ComponentDto<CarouselResponseItemDto>
                            {
                                Component = "NMMusicCard",
                                Props =
                                {
                                    Data = item,
                                }
                            })
                    }
                },

                new ComponentDto<CarouselResponseItemDto>
                {
                    Component = "NMCarousel",
                    Props =
                    {
                        Title = "Favorite Albums",
                        Items = favoriteAlbums
                            .Select(item => new ComponentDto<CarouselResponseItemDto>
                            {
                                Component = "NMMusicCard",
                                Props =
                                {
                                    Data = item,
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
                        MoreLink = new("/music/playlists", UriKind.Relative),
                        Items = playlists
                            .Select(item => new ComponentDto<CarouselResponseItemDto>
                            {
                                Component = "NMMusicCard",
                                Props =
                                {
                                    Data = item,
                                }
                            })
                    }
                },

                new ComponentDto<CarouselResponseItemDto>
                {
                    Component = "NMCarousel",
                    Props =
                    {
                        Title = "Artists",
                        MoreLink = new("/music/artists", UriKind.Relative),
                        Items = latestArtists
                            .Select(item => new ComponentDto<CarouselResponseItemDto>
                            {
                                Component = "NMMusicCard",
                                Props =
                                {
                                    Data = item,
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
                        MoreLink = new("/music/albums", UriKind.Relative),
                        Items = latestAlbums
                            .Select(item => new ComponentDto<CarouselResponseItemDto>
                            {
                                Component = "NMMusicCard",
                                Props =
                                {
                                    Data = item,
                                }
                            })
                    }
                },
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
    public IActionResult Search([FromQuery] SearchQueryRequest request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to search music");

        string country = Country();
        
        string normalizedQuery = request.Query.NormalizeSearch();
        
        MediaContext mediaContext = new();

        // Step 1: Get IDs of artists, albums, playlists, and tracks that match the search query
        // Using ToList() to force the query to execute and load the data into memory
        // This is necessary to allow for the NormalizeSearch() method to work.
        List<Guid> artistIds = mediaContext.Artists
            .Select(artist => new { artist.Id, artist.Name })
            .ToList()
            .Where(artist => artist.Name.NormalizeSearch().Contains(normalizedQuery))
            .Select(track => track.Id)
            .ToList();

        List<Guid> albumIds = mediaContext.Albums
            .Select(album => new { album.Id, album.Name })
            .ToList()
            .Where(album => album.Name.NormalizeSearch().Contains(normalizedQuery))
            .Select(track => track.Id)
            .ToList();

        List<Guid> playlistIds = mediaContext.Playlists
            .Select(playlist => new { playlist.Id, playlist.Name })
            .ToList()
            .Where(playlist => playlist.Name.NormalizeSearch().Contains(normalizedQuery))
            .Select(track => track.Id)
            .ToList();

        List<Guid> trackIds = mediaContext.Tracks
            .Select(track => new { track.Id, track.Name })
            .ToList()
            .Where(track => track.Name.NormalizeSearch().Contains(normalizedQuery))
            .Select(track => track.Id)
            .ToList();

        // Step 2: Query full data using the IDs
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
        {
            foreach (Album? album in albums)
            {
                if (album.AlbumTrack.Count > 0)
                {
                    foreach (IEnumerable<Artist> artist in album.AlbumTrack.
                                 Select(albumTrack => albumTrack.Track.ArtistTrack
                                     .Select(artistTrack => artistTrack.Artist)).ToList())
                        artists.AddRange(artist);
                }
            }
        }

        if (playlists.Count > 0)
        {
            foreach (Playlist? playlist in playlists)
            {
                if (playlist.Tracks.Count > 0)
                {
                    foreach (IEnumerable<Artist> artist in playlist.Tracks
                                 .Select(playlistTrack => playlistTrack.Track.ArtistTrack
                                     .Select(artistTrack => artistTrack.Artist)).ToList())
                        artists.AddRange(artist);
                }
            }
        }

        if (songs.Count > 0)
        {
            foreach (Track? song in songs)
            {
                if (song.ArtistTrack.Count > 0)
                {
                    artists.AddRange(song.ArtistTrack.Select(artistTrack => artistTrack.Artist));
                }
                if (song.AlbumTrack.Count > 0)
                {
                    albums.AddRange(song.AlbumTrack.Select(albumTrack => albumTrack.Album));
                }
            }
        }

        Track? topTrack = songs.FirstOrDefault();
        Artist? topArtist = artists.FirstOrDefault();
        Album? topAlbum = albums.FirstOrDefault();
        
        string title = topTrack?.Name ?? topArtist?.Name ?? topAlbum?.Name ?? "Top Result";
        string? cover = topTrack?.Cover ?? topArtist?.Cover ?? topAlbum?.Cover;
        string type = topTrack != null ? "Track" : topArtist != null ? "Artist" : topAlbum != null ? "Album" : "Top Result";
        List<ArtistDto> artistsList = topTrack?.ArtistTrack.Select(artistTrack => new ArtistDto(artistTrack, country)).ToList() ?? new List<ArtistDto>();
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
            {"title", title},
            {"cover", cover is not null 
                ? new Uri($"/images/music{cover}", UriKind.Relative).ToString() 
                : null},
            {"link", link},
            {"type", type},
            {"artists", artistsList},
            {"width", "33.33333%"},
            { "track", topTrackItem is not null ? new ArtistTrackDto(topTrackItem) : null}
        };

        List<ArtistTrackDto> songResults = songs
            .Take(6)
            .Select(track => new ArtistTrackDto(track, country))
            .ToList();

        return Ok(new Render
        {
            Data = [
                songResults.Count > 0 ? new()
                {
                    Component = "NMContainer",
                    Props =
                    {
                        Items = [
                            new()
                            {
                                Component = "NMTopResultCard",
                                Props =
                                {
                                    Title = "Top Result",
                                    Data = topResult,
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
                            },
                        ]
                    }
                } : new ComponentDto<dynamic>(),

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
                                    Data = new(item),
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
                                Data = new(item),
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
                                Data = new(item),
                            }
                        })
                    }
                },
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
