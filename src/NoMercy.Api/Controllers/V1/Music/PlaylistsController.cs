using System.Text.RegularExpressions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Api.DTOs.Music;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Music;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Helpers.Extensions;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs.PaletteJobs;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercyQueue;

namespace NoMercy.Api.Controllers.V1.Music;

[ApiController]
[ApiVersion(1.0)]
[Tags("Music Playlists")]
[Authorize]
[Route("api/v{version:apiVersion}/music/playlists", Order = 3)]
public class PlaylistsController : BaseController
{
    private readonly IMusicRepository _musicRepository;
    private readonly IEventBus _eventBus;

    public PlaylistsController(IMusicRepository musicService, IEventBus eventBus)
    {
        _musicRepository = musicService;
        _eventBus = eventBus;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view playlists");

        List<PlaylistCardDto> playlistCards = await _musicRepository.GetPlaylistCardsAsync(userId);

        ComponentEnvelope response = Component
            .Grid()
            .WithItems(playlistCards.Select(p => Component.MusicCard(new MusicCardData(p))));

        return Ok(ComponentResponse.From(response));
    }

    [HttpGet]
    [Route("{id:guid}")]
    public async Task<IActionResult> Show(Guid id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view playlists");

        Playlist? playlist = await _musicRepository.GetPlaylistAsync(userId, id);

        if (playlist == null)
            return NotFoundResponse("Playlist not found");

        string language = Language();

        if (string.IsNullOrEmpty(playlist._colorPalette) || playlist._colorPalette == "{}")
            QueueRunner.Current?.Dispatcher.Dispatch(
                new ColorPaletteJob("playlist", playlist.Id.ToString()),
                "palette",
                1
            );

        return Ok(new PlaylistResponseDto { Data = new(playlist, language) });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePlaylistRequestDto request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to create a playlist");

        Guid userId = User.UserId();

        if (await _musicRepository.PlaylistNameExistsAsync(request.Name, userId))
            return ConflictResponse("You already have a playlist with that name");

        Playlist newPlaylist = new()
        {
            Name = request.Name,
            Description = request.Description,
            UserId = userId,
        };

        string slug = newPlaylist.Name.ToSlug();

        // save to app images folder
        string filePath = Path.Combine(AppFiles.ImagesPath, "music", slug + ".jpg");
        Logger.App(filePath);

        if (request.Cover is not null)
        {
            Match coverMatch = Regex.Match(request.Cover, "data:image/(?<type>.+?),(?<data>.+)");
            if (!coverMatch.Success)
                return BadRequestResponse("Cover must be a data:image/...;base64,... payload");

            byte[] binData;
            try
            {
                binData = Convert.FromBase64String(coverMatch.Groups["data"].Value);
            }
            catch (FormatException)
            {
                return BadRequestResponse("Cover payload is not valid base64");
            }

            await using (FileStream stream = new(filePath, FileMode.OpenOrCreate))
                await stream.WriteAsync(binData);

            newPlaylist.Cover = $"/{slug}.jpg";
            newPlaylist._colorPalette = await CoverArtImageManagerManager.ColorPalette(
                "cover",
                new(filePath)
            );
        }

        Logger.App(newPlaylist);

        await _musicRepository.CreatePlaylistAsync(newPlaylist, request.Tracks);

        Playlist? playlist = await _musicRepository.GetPlaylistByNameAsync(request.Name, userId);

        await _eventBus.PublishAsync(new LibraryRefreshEvent { QueryKey = ["music-playlists"] });

        return Ok(new StatusResponseDto<Playlist?> { Data = playlist, Status = "ok" });
    }

    [HttpPatch]
    [Route("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, [FromBody] CreatePlaylistRequestDto request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to edit a playlist");

        Playlist? playlist = await _musicRepository.GetPlaylistForEditAsync(id);

        if (playlist is null)
            return NotFoundResponse("Playlist not found");

        string slug = playlist.Name.ToSlug();
        string colorPalette = playlist._colorPalette.OrEmpty();
        string cover = playlist.Cover.OrEmpty();

        if (request.Cover is not null)
        {
            Match coverMatch = Regex.Match(request.Cover, "data:image/(?<type>.+?),(?<data>.+)");
            if (!coverMatch.Success)
                return BadRequestResponse("Cover must be a data:image/...;base64,... payload");

            byte[] binData;
            try
            {
                binData = Convert.FromBase64String(coverMatch.Groups["data"].Value);
            }
            catch (FormatException)
            {
                return BadRequestResponse("Cover payload is not valid base64");
            }

            cover = $"/{slug}.jpg";
            string filePath = Path.Combine(AppFiles.ImagesPath, "music", slug + ".jpg");

            await using (FileStream stream = new(filePath, FileMode.Create))
                await stream.WriteAsync(binData);

            colorPalette = await CoverArtImageManagerManager.ColorPalette("cover", new(filePath));
        }

        int result = await _musicRepository.UpdatePlaylistMetadataAsync(
            id,
            request.Name,
            request.Description,
            cover,
            colorPalette
        );

        await _eventBus.PublishAsync(
            new LibraryRefreshEvent { QueryKey = ["music", "playlists", id] }
        );

        return Ok(
            new StatusResponseDto<string>
            {
                Data = (
                    result > 0 ? "Playlist updated successfully" : "No changes made"
                ).Localize(),
                Status = "ok",
            }
        );
    }

    [HttpDelete]
    [Route("{id:guid}")]
    public async Task<IActionResult> Destroy(Guid id)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to delete a playlist");

        int result = await _musicRepository.DeletePlaylistAsync(id, User.UserId());

        await _eventBus.PublishAsync(new LibraryRefreshEvent { QueryKey = ["music-playlists"] });

        return Ok(
            new StatusResponseDto<string>
            {
                Data = (
                    result > 0 ? "Playlist deleted successfully" : "Playlist not found"
                ).Localize(),
                Status = "ok",
            }
        );
    }

    [HttpPost]
    [Route("{id:guid}/cover")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Cover(Guid id, IFormFile image)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to upload playlist covers");

        Playlist? playlist = await _musicRepository.GetPlaylistForCoverAsync(id, User.UserId());

        if (playlist is null)
            return NotFoundResponse("Playlist not found");

        string slug = playlist.Name.ToSlug();

        // save to app images folder
        string filePath2 = Path.Combine(AppFiles.ImagesPath, "music", slug + ".jpg");
        Logger.App(filePath2);
        await using (FileStream stream = new(filePath2, FileMode.Create))
        {
            await image.CopyToAsync(stream);
        }

        string cover = $"/{slug}.jpg";
        string colorPalette = await CoverArtImageManagerManager.ColorPalette(
            "cover",
            new(filePath2)
        );

        await _musicRepository.UpdatePlaylistCoverAsync(id, User.UserId(), cover, colorPalette);

        await _eventBus.PublishAsync(
            new LibraryRefreshEvent { QueryKey = ["music", "playlists", playlist.Id] }
        );

        playlist._colorPalette = colorPalette;

        return Ok(
            new StatusResponseDto<ImageUploadResponseDto>
            {
                Status = "ok",
                Message = "Playlist cover updated",
                Data = new()
                {
                    Url = new($"/images/music/{slug}.jpg", UriKind.Relative),
                    ColorPalette = playlist.ColorPalette,
                },
            }
        );
    }

    [HttpPost]
    [Route("{id:guid}/tracks")]
    public async Task<IActionResult> AddTrack(
        Guid id,
        [FromBody] CreatePlaylistTrackRequestDto request
    )
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to edit a playlist");

        int result = await _musicRepository.AddPlaylistTrackAsync(id, request.Id);

        await _eventBus.PublishAsync(
            new LibraryRefreshEvent { QueryKey = ["music", "playlists", id] }
        );

        return Ok(
            new StatusResponseDto<string>
            {
                Data = (
                    result > 0 ? "Playlist updated successfully" : "No changes made"
                ).Localize(),
                Status = "ok",
            }
        );
    }

    [HttpDelete]
    [Route("{id:guid}/tracks/{trackId:guid}")]
    public async Task<IActionResult> AddTrack(Guid id, Guid trackId)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to edit a playlist");

        int result = await _musicRepository.RemovePlaylistTrackAsync(id, trackId, User.UserId());

        if (result < 0)
            return NotFoundResponse("Track not found in playlist");

        await _eventBus.PublishAsync(
            new LibraryRefreshEvent { QueryKey = ["music", "playlists", id] }
        );

        return Ok(
            new StatusResponseDto<string>
            {
                Data = (
                    result > 0 ? "Playlist updated successfully" : "No changes made"
                ).Localize(),
                Status = "ok",
            }
        );
    }
}

public class CreatePlaylistRequestDto
{
    [JsonProperty("name")]
    public string Name { get; set; } = null!;

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("cover")]
    public string? Cover { get; set; }

    [JsonProperty("tracks")]
    public List<Guid> Tracks { get; set; } = [];
}

public class CreatePlaylistTrackRequestDto
{
    [JsonProperty("id")]
    public Guid Id { get; set; }
}
