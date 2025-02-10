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
using NoMercy.Networking;

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

        MediaContext mediaContext = new();
        List<Artist> artists = mediaContext.Artists
            .Where(artist => artist.Name.ToLower().Contains(request.Query.ToLower()))
            .Include(artist => artist.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Track)
            .ThenInclude(song => song.TrackUser)
            .Include(artist => artist.AlbumArtist)
            .ThenInclude(albumArtist => albumArtist.Album)
            .ToList();

        List<Album> albums = mediaContext.Albums
            .Where(album => album.Name.ToLower().Contains(request.Query.ToLower()))
            .Include(album => album.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Track)
            .ThenInclude(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .Include(album => album.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Track)
            .ThenInclude(song => song.TrackUser)
            .ToList();

        List<Playlist> playlists = mediaContext.Playlists
            .Where(playlist => playlist.Name.ToLower().Contains(request.Query.ToLower()))
            .Include(playlist => playlist.Tracks)
            .ThenInclude(playlistTrack => playlistTrack.Track)
            .ThenInclude(song => song.TrackUser)
            .ToList();

        List<Track> songs = mediaContext.Tracks
            .Where(song => song.Name.ToLower().Contains(request.Query.ToLower()))
            .Include(song => song.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .Include(song => song.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            .Include(song => song.PlaylistTrack)
            .ThenInclude(playlistTrack => playlistTrack.Playlist)
            .Include(song => song.TrackUser)
            .ToList();

        if (artists.Count == 0 && albums.Count == 0 && playlists.Count == 0 && songs.Count == 0)
            return NotFoundResponse("No results found");

        // if (albums.Count > 0)
        // {
        //     foreach (var album in albums)
        //     {
        //         if (album.AlbumTrack.Count > 0)
        //         {
        //             foreach (IEnumerable<Artist> artist in album.AlbumTrack.
        //                          Select(albumTrack => albumTrack.Track.ArtistTrack
        //                              .Select(artistTrack => artistTrack.Artist)).ToList())
        //                 artists.AddRange(artist);
        //         }
        //     }
        // }

        // if (playlists.Count > 0)
        // {
        //     foreach (var playlist in playlists)
        //     {
        //         if (playlist.Tracks.Count > 0)
        //         {
        //             foreach (IEnumerable<Artist> artist in playlist.Tracks
        //                          .Select(playlistTrack => playlistTrack.Track.ArtistTrack
        //                              .Select(artistTrack => artistTrack.Artist)).ToList())
        //                 artists.AddRange(artist);
        //         }
        //     }
        // }

        // if (songs.Count > 0)
        // {
        //     foreach (var song in songs)
        //     {
        //         if (song.ArtistTrack.Count > 0)
        //         {
        //             artists.AddRange(song.ArtistTrack.Select(artistTrack => artistTrack.Artist));
        //         }
        //         if (song.AlbumTrack.Count > 0)
        //         {
        //             albums.AddRange(song.AlbumTrack.Select(albumTrack => albumTrack.Album));
        //         }
        //     }
        // }

        // Track? topTrack = songs.FirstOrDefault();
        // Artist? topArtist = artists.FirstOrDefault();
        // Album? topAlbum = albums.FirstOrDefault();
        //
        // string title = topTrack?.Name ?? topArtist?.Name ?? topAlbum?.Name ?? "Top Result";
        // string cover = topTrack?.Cover ?? topArtist?.Cover ?? topAlbum?.Cover ?? "https://via.placeholder.com/150";
        // string type = topTrack != null ? "Track" : topArtist != null ? "Artist" : topAlbum != null ? "Album" : "Top Result";
        // List<ArtistDto> artistsList = topTrack?.ArtistTrack.Select(artistTrack => new ArtistDto(artistTrack, country)).ToList() ?? new List<ArtistDto>();
        //
        // Uri link = new("/", UriKind.Relative);
        // if (topTrack != null)
        //     link = new Uri($"/music/tracks/{topTrack.Id}", UriKind.Relative);
        // else if (topArtist != null)
        //     link = new Uri($"/music/artist/{topArtist.Id}", UriKind.Relative);
        // else if (topAlbum != null)
        //     link = new Uri($"/music/album/{topAlbum.Id}", UriKind.Relative);
        //
        // var topResult = new Dictionary<string, object>
        // {
        //     {"title", title},
        //     {"cover", cover},
        //     {"link", link},
        //     {"type", type},
        //     {"artists", artistsList},
        //     {"width", "33.33333%"}
        // };

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
                            // new ComponentDto<dynamic>
                            // {
                            //     Component = "NMTopResultCard",
                            //     Props =
                            //     {
                            //         Title = "Top Result",
                            //         Data = topResult,
                            //     }
                            // },
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
