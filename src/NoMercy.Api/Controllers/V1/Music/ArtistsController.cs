using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Controllers.Socket.music;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Music.DTO;
using NoMercy.Api.Services;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.Networking.Dto;

namespace NoMercy.Api.Controllers.V1.Music;

[ApiController]
[ApiVersion(1.0)]
[Tags("Music Artists")]
[Authorize]
[Route("api/v{version:apiVersion}/music/artist")]
public class ArtistsController : BaseController
{
    private readonly MusicRepository _musicRepository;
    private readonly MediaContext _mediaContext;

    public ArtistsController(MusicRepository musicService, MediaContext mediaContext)
    {
        _musicRepository = musicService;
        _mediaContext = mediaContext;
    }

    public static event EventHandler<MusicLikeEventDto>? OnLikeEvent;

    [HttpGet]
    [Route("/api/v{version:apiVersion}/music/artists/{letter}")]
    public async Task<IActionResult> Index(string letter)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view artists");

        List<ArtistsResponseItemDto> artists = [];

        await foreach (Artist artist in _musicRepository.GetArtists(_mediaContext, userId, letter))
            artists.Add(new(artist));

        List<ArtistTrack> tracks = await _musicRepository.GetArtistTracksForCollection(_mediaContext,
            artists.Select(a => a.Id)
                .ToList());

        foreach (ArtistsResponseItemDto artist in artists)
            artist.Tracks = tracks.Count(track => track.ArtistId == artist.Id);

        return Ok(new ArtistsResponseDto
        {
            Data = artists
                .Where(response => response.Tracks > 0)
                .OrderBy(artist => artist.Name)
        });
    }

    [HttpGet]
    [Route("{id:guid}")]
    public async Task<IActionResult> Show(Guid id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view artists");

        Artist? artist = await _musicRepository.GetArtist(_mediaContext, userId, id);

        string country = Country();

        if (artist is null)
            return NotFoundResponse("Artist not found");

        return Ok(new ArtistResponseDto
        {
            Data = new(artist, userId, country)
        });
    }

    [HttpPost]
    [Route("{id:guid}/like")]
    public async Task<IActionResult> Like(Guid id, [FromBody] LikeRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to like artists");

        await using MediaContext mediaContext = new();
        Artist? artist = await mediaContext.Artists
            .AsNoTracking()
            .Where(artistUser => artistUser.Id == id)
            .FirstOrDefaultAsync();

        if (artist is null)
            return UnprocessableEntityResponse("Artist not found");

        await _musicRepository.LikeArtistAsync(userId, artist, request.Value);

        Networking.Networking.SendToAll("RefreshLibrary", "socket", new RefreshLibraryDto
        {
            QueryKey = ["music", "artist", artist.Id]
        });

        MusicLikeEventDto musicLikeEventDto = new()
        {
            Id = artist.Id,
            Type = "artist",
            Liked = request.Value,
            User = User.User()
        };

        OnLikeEvent?.Invoke(this, musicLikeEventDto);

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "{0} {1}",
            Args = new object[]
            {
                artist.Name,
                request.Value ? "liked" : "unliked"
            }
        });
    }

    [HttpPost]
    [Route("{id:guid}/rescan")]
    public async Task<IActionResult> Like(Guid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to rescan artists");

        await using MediaContext mediaContext = new();

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Rescan started",
            Args = []
        });
    }
}