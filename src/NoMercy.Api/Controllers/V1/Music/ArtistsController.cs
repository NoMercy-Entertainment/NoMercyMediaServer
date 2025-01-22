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
[Tags("Music Artists")]
[Authorize]
[Route("api/v{version:apiVersion}/music/artist")]
public class ArtistsController : BaseController
{
    [HttpGet]
    [Route("/api/v{version:apiVersion}/music/artists/{letter}")]
    public async Task<IActionResult> Index(string letter)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view artists");

        List<ArtistsResponseItemDto> artists = [];

        await using MediaContext mediaContext = new();
        await foreach (Artist artist in ArtistsResponseDto.GetArtists(mediaContext, userId, letter))
            artists.Add(new(artist));

        List<ArtistTrack> tracks = mediaContext.ArtistTrack
            .Where(artistTrack => artists.Select(a => a.Id).Contains(artistTrack.ArtistId))
            .Where(artistTrack => artistTrack.Track.Duration != null)
            .ToList();

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

        await using MediaContext mediaContext = new();
        Artist? artist = await ArtistResponseDto.GetArtist(mediaContext, userId, id);

        string language = Language();

        if (artist is null)
            return NotFoundResponse("Artist not found");

        return Ok(new ArtistResponseDto
        {
            Data = new(artist, userId, language)
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

        if (request.Value)
        {
            await mediaContext.ArtistUser
                .Upsert(new(artist.Id, userId))
                .On(m => new { m.ArtistId, m.UserId })
                .WhenMatched(m => new()
                {
                    ArtistId = m.ArtistId,
                    UserId = m.UserId
                })
                .RunAsync();
        }
        else
        {
            ArtistUser? tvUser = await mediaContext.ArtistUser
                .Where(tvUser => tvUser.ArtistId == artist.Id && tvUser.UserId.Equals(userId))
                .FirstOrDefaultAsync();

            if (tvUser is not null) mediaContext.ArtistUser.Remove(tvUser);

            await mediaContext.SaveChangesAsync();
        }

        Networking.Networking.SendToAll("RefreshLibrary", "socket", new RefreshLibraryDto()
        {
            QueryKey = ["music", "artists", artist.Id]
        });

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
