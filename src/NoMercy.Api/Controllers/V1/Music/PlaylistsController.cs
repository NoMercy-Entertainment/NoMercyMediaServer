using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1.Music.DTO;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Networking;
using PlaylistResponseItemDto = NoMercy.Api.Controllers.V1.Music.DTO.PlaylistResponseItemDto;

namespace NoMercy.Api.Controllers.V1.Music;

[ApiController]
[ApiVersion(1.0)]
[Tags("Music Playlists")]
[Authorize]
[Route("api/v{version:apiVersion}/music/playlists", Order = 3)]
public class PlaylistsController : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view playlists");

        List<PlaylistResponseItemDto> playlists = [];

        await using MediaContext mediaContext = new();
        await foreach (Playlist playlist in PlaylistResponseDto.GetPlaylists(mediaContext, userId))
            playlists.Add(new(playlist));

        return Ok(new PlaylistResponseDto
        {
            Data = playlists
        });
    }

    [HttpGet]
    [Route("{id:guid}")]
    public async Task<IActionResult> Show(Guid id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view playlists");

        await using MediaContext mediaContext = new();
        Playlist? playlist = await PlaylistResponseDto.GetPlaylist(mediaContext, userId, id);

        if (playlist == null)
            return NotFoundResponse("Playlist not found");

        string language = Language();

        return Ok(new TracksResponseDto
        {
            Data = new()
            {
                Id = playlist.Id,
                Name = playlist.Name,
                Cover = playlist.Cover is not null ? new Uri($"/images/music{playlist.Cover}", UriKind.Relative).ToString() : null,
                Description = playlist.Description,
                ColorPalette = playlist.ColorPalette,
                Tracks = playlist.Tracks.Select(t => new ArtistTrackDto(t.Track, language)).ToList()
            }
        });
    }

    [HttpPost]
    public IActionResult Create()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to create a playlist");

        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }

    [HttpPatch]
    [Route("{id:guid}")]
    public IActionResult Edit(Guid id)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to edit a playlist");

        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }

    [HttpDelete]
    [Route("{id:guid}")]
    public IActionResult Destroy(Guid id)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to delete a playlist");

        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }
}
