using System.Text.RegularExpressions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Music.DTO;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Networking;

namespace NoMercy.Api.Controllers.V1.Music;

[ApiController]
[ApiVersion(1.0)]
[Tags("Music Collections")]
[Authorize]
[Route("api/v{version:apiVersion}/music/collection", Order = 2)]
public class CollectionsController : BaseController
{
    [HttpGet]
    [Route("tracks")]
    public async Task<IActionResult> Tracks()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view tracks");

        List<ArtistTrackDto> tracks = [];

        string language = Language();

        await using MediaContext mediaContext = new();
        await foreach (TrackUser track in TracksResponseDto.GetTracks(mediaContext, userId))
            tracks.Add(new(track.Track, language));

        if (tracks.Count == 0)
            return NotFoundResponse("Tracks not found");

        return Ok(new TracksResponseDto
        {
            Data = new()
            {
                ColorPalette = new(),
                Tracks = tracks
            }
        });
    }

    [HttpGet]
    [Route("artists")]
    public async Task<IActionResult> Artists([FromQuery] FilterRequest request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view artists");

        List<ArtistsResponseItemDto> artists = [];

        await using MediaContext mediaContext = new();
        await foreach (Artist artist in ArtistsResponseDto.GetArtists(mediaContext, userId, request.Letter!))
            artists.Add(new(artist));

        if (artists.Count == 0)
            return NotFoundResponse("Artists not found");

        List<ArtistTrack> tracks = mediaContext.ArtistTrack
            .Where(artistTrack => artists.Select(a => a.Id)
                .Contains(artistTrack.ArtistId))
            .Where(artistTrack => artistTrack.Track.Duration != null)
            .Include(artistTrack => artistTrack.Track)
            .ToList();

        foreach (ArtistsResponseItemDto artist in artists)
            artist.Tracks = tracks
                .DistinctBy(track => Regex.Replace(track.Track.Filename ?? "", @"[\d+-]\s", "").ToLower())
                .Count(track => track.ArtistId == artist.Id);

        return Ok(new ArtistsResponseDto
        {
            Data = artists
                .Where(response => response.Tracks > 0)
        });
    }

    [HttpGet]
    [Route("albums")]
    public async Task<IActionResult> Albums([FromQuery] FilterRequest request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view albums");

        List<AlbumsResponseItemDto> albums = [];

        await using MediaContext mediaContext = new();
        await foreach (Album album in AlbumsResponseDto.GetAlbums(mediaContext, userId, request.Letter!))
            albums.Add(new(album));

        if (albums.Count == 0)
            return NotFoundResponse("Albums not found");

        List<AlbumTrack> tracks = mediaContext.AlbumTrack
            .Where(albumTrack => albums.Select(a => a.Id)
                .Contains(albumTrack.AlbumId))
            .Where(albumTrack => albumTrack.Track.Duration != null)
            .Include(albumTrack => albumTrack.Track)
            .ToList();

        foreach (AlbumsResponseItemDto album in albums)
            album.Tracks = tracks
                .DistinctBy(track => Regex.Replace(track.Track.Filename ?? "", @"[\d+-]\s", "").ToLower())
                .Count(track => track.AlbumId == album.Id);

        return Ok(new AlbumsResponseDto
        {
            Data = albums
                .Where(response => response.Tracks > 0)
        });
    }

    [HttpGet]
    [Route("playlists")]
    public async Task<IActionResult> Playlists()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view playlists");

        List<PlaylistResponseItemDto> playlists = [];

        await using MediaContext mediaContext = new();
        await foreach (Playlist playlist in PlaylistResponseDto.GetPlaylists(mediaContext, userId))
            playlists.Add(new(playlist));

        if (playlists.Count == 0)
            return NotFoundResponse("Playlists not found");

        return Ok(new PlaylistResponseDto
        {
            Data = playlists
        });
    }
}