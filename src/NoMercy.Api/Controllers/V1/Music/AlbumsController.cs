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
[Tags("Music Albums")]
[Authorize]
[Route("api/v{version:apiVersion}/music/album")]
public class AlbumsController : BaseController
{
    [HttpGet]
    [Route("/api/v{version:apiVersion}/music/albums/{letter}")]
    public async Task<IActionResult> Index(string letter)
    {
        List<AlbumsResponseItemDto> albums = [];
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view albums");

        string language = Language();

        await using MediaContext mediaContext = new();
        await foreach (Album album in AlbumsResponseDto.GetAlbums(mediaContext, userId, letter))
            albums.Add(new(album, language));

        List<AlbumTrack> tracks = mediaContext.AlbumTrack
            .Where(albumTrack => albums.Select(a => a.Id).Contains(albumTrack.AlbumId))
            .Where(albumTrack => albumTrack.Track.Duration != null)
            .ToList();

        if (tracks.Count == 0)
            return NotFoundResponse("Albums not found");

        foreach (AlbumsResponseItemDto album in albums) album.Tracks = tracks.Count(track => track.AlbumId == album.Id);

        return Ok(new AlbumsResponseDto
        {
            Data = albums
                .Where(response => response.Tracks > 0)
        });
    }

    [HttpGet]
    [Route("{id:guid}")]
    public async Task<IActionResult> Show(Guid id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view albums");

        string language = Language();

        await using MediaContext mediaContext = new();
        Album? album = await AlbumResponseDto.GetAlbum(mediaContext, userId, id);

        if (album is null)
            return NotFoundResponse("Album not found");

        return Ok(new AlbumResponseDto
        {
            Data = new(album, language)
        });
    }

    [HttpPost]
    [Route("{id:guid}/like")]
    public async Task<IActionResult> Like(Guid id, [FromBody] LikeRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to like albums");

        await using MediaContext mediaContext = new();
        Album? album = await mediaContext.Albums
            .AsNoTracking()
            .Where(album => album.Id == id)
            .FirstOrDefaultAsync();

        if (album is null)
            return UnprocessableEntityResponse("Album not found");

        if (request.Value)
        {
            await mediaContext.AlbumUser
                .Upsert(new(album.Id, userId))
                .On(m => new { m.AlbumId, m.UserId })
                .WhenMatched(m => new()
                {
                    AlbumId = m.AlbumId,
                    UserId = m.UserId
                })
                .RunAsync();
        }
        else
        {
            AlbumUser? tvUser = await mediaContext.AlbumUser
                .Where(tvUser => tvUser.AlbumId == album.Id && tvUser.UserId.Equals(userId))
                .FirstOrDefaultAsync();

            if (tvUser is not null) mediaContext.AlbumUser.Remove(tvUser);

            await mediaContext.SaveChangesAsync();
        }

        Networking.Networking.SendToAll("RefreshLibrary", "socket", new RefreshLibraryDto()
        {
            QueryKey = ["music", "albums", album.Id]
        });

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "{0} {1}",
            Args = new object[]
            {
                album.Name,
                request.Value ? "liked" : "unliked"
            }
        });
    }

    [HttpPost]
    [Route("{id:guid}/rescan")]
    public async Task<IActionResult> Like(Guid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to rescan albums");

        await using MediaContext mediaContext = new();

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Rescan started",
            Args = []
        });
    }
}
