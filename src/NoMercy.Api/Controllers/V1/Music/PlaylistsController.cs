// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Text.RegularExpressions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Api.DTOs.Music;
using NoMercy.Authorization;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Music;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs.PaletteJobs;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercyQueue;

namespace NoMercy.Api.Controllers.V1.Music;

[ApiController]
[ApiVersion(version: 1.0)]
[Tags(tags: "Music Playlists")]
[Authorize(Policy = "MediaAccess")]
[Route(template: "api/v{version:apiVersion}/music/playlists", Order = 3)]
public class PlaylistsController : BaseController
{
    private readonly IMusicRepository _musicRepository;
    private readonly IEventBus _eventBus;

    private readonly ILogger<PlaylistsController> _logger;

    public PlaylistsController(
        ILogger<PlaylistsController> logger,
        IMusicRepository musicService,
        IEventBus eventBus
    )
    {
        _logger = logger;
        _musicRepository = musicService;
        _eventBus = eventBus;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to view playlists");

        List<PlaylistCardDto> playlistCards = await _musicRepository.GetPlaylistCardsAsync(userId: userId);

        ComponentEnvelope response = Component
            .Grid()
            .WithItems(items: playlistCards.Select(selector: p => Component.MusicCard(data: new MusicCardData(playlist: p))));

        return Ok(value: ComponentResponse.From(component: response));
    }

    [HttpGet]
    [Route(template: "{id:guid}")]
    public async Task<IActionResult> Show(Guid id)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to view playlists");

        Playlist? playlist = await _musicRepository.GetPlaylistAsync(userId: userId, id: id);

        if (playlist == null)
            return NotFoundResponse(detail: "Playlist not found");

        string language = Language();

        // Fire-and-forget: enqueue takes the queue's global write lock (held by the
        // encoder workers), so dispatching inline blocked this read for seconds.
        if (string.IsNullOrEmpty(value: playlist._colorPalette) || playlist._colorPalette == "{}")
            _ = Task.Run(action: () =>
                QueueRunner.Current?.Dispatcher.Dispatch(
                    job: new ColorPaletteJob(entityType: "playlist", entityId: playlist.Id.ToString()),
                    onQueue: "palette",
                    priority: 1
                )
            );

        return Ok(value: new PlaylistResponseDto { Data = new(playlist: playlist, country: language) });
    }

    [HttpPost]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> Create([FromBody] CreatePlaylistRequestDto request)
    {
        Guid userId = User.UserId();

        if (await _musicRepository.PlaylistNameExistsAsync(name: request.Name, userId: userId))
            return ConflictResponse(detail: "You already have a playlist with that name");

        Playlist newPlaylist = new()
        {
            Name = request.Name,
            Description = request.Description,
            UserId = userId,
        };

        string slug = newPlaylist.Name.ToSlug();

        // save to app images folder
        string filePath = Path.Combine(path1: AppFiles.ImagesPath, path2: "music", path3: slug + ".jpg");
        _logger.LogInformation(message: filePath);

        if (request.Cover is not null)
        {
            Match coverMatch = Regex.Match(input: request.Cover, pattern: "data:image/(?<type>.+?),(?<data>.+)");
            if (!coverMatch.Success)
                return BadRequestResponse(detail: "Cover must be a data:image/...;base64,... payload");

            byte[] binData;
            try
            {
                binData = Convert.FromBase64String(s: coverMatch.Groups[groupname: "data"].Value);
            }
            catch (FormatException)
            {
                return BadRequestResponse(detail: "Cover payload is not valid base64");
            }

            await using (FileStream stream = new(path: filePath, mode: FileMode.OpenOrCreate))
                await stream.WriteAsync(buffer: binData);

            newPlaylist.Cover = $"/{slug}.jpg";
            newPlaylist._colorPalette = await CoverArtImageManagerManager.ColorPalette(
                type: "cover",
                url: new(uriString: filePath)
            );
        }

        _logger.LogInformation(message: "{Playlist}", args: newPlaylist);

        await _musicRepository.CreatePlaylistAsync(playlist: newPlaylist, trackIds: request.Tracks);

        Playlist? playlist = await _musicRepository.GetPlaylistByNameAsync(name: request.Name, userId: userId);

        await _eventBus.PublishAsync(@event: new LibraryRefreshedEvent { QueryKey = ["music-playlists"] });

        return Ok(value: new StatusResponseDto<Playlist?> { Data = playlist, Status = "ok" });
    }

    [HttpPatch]
    [Route(template: "{id:guid}")]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> Edit(Guid id, [FromBody] CreatePlaylistRequestDto request)
    {
        Guid userId = User.UserId();
        Playlist? playlist = await _musicRepository.GetPlaylistForEditAsync(id: id, userId: userId);

        if (playlist is null)
            return NotFoundResponse(detail: "Playlist not found");

        string slug = playlist.Name.ToSlug();
        string colorPalette = playlist._colorPalette.OrEmpty();
        string cover = playlist.Cover.OrEmpty();

        if (request.Cover is not null)
        {
            Match coverMatch = Regex.Match(input: request.Cover, pattern: "data:image/(?<type>.+?),(?<data>.+)");
            if (!coverMatch.Success)
                return BadRequestResponse(detail: "Cover must be a data:image/...;base64,... payload");

            byte[] binData;
            try
            {
                binData = Convert.FromBase64String(s: coverMatch.Groups[groupname: "data"].Value);
            }
            catch (FormatException)
            {
                return BadRequestResponse(detail: "Cover payload is not valid base64");
            }

            cover = $"/{slug}.jpg";
            string filePath = Path.Combine(path1: AppFiles.ImagesPath, path2: "music", path3: slug + ".jpg");

            await using (FileStream stream = new(path: filePath, mode: FileMode.Create))
                await stream.WriteAsync(buffer: binData);

            colorPalette = await CoverArtImageManagerManager.ColorPalette(type: "cover", url: new(uriString: filePath));
        }

        int result = await _musicRepository.UpdatePlaylistMetadataAsync(
            id: id,
            userId: userId,
            name: request.Name,
            description: request.Description,
            cover: cover,
            colorPalette: colorPalette
        );

        await _eventBus.PublishAsync(
            @event: new LibraryRefreshedEvent { QueryKey = ["music", "playlists", id] }
        );

        return Ok(
            value: new StatusResponseDto<string>
            {
                Data = (
                    result > 0 ? "Playlist updated successfully" : "No changes made"
                ).Localize(),
                Status = "ok",
            }
        );
    }

    [HttpDelete]
    [Route(template: "{id:guid}")]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> Destroy(Guid id)
    {
        int result = await _musicRepository.DeletePlaylistAsync(id: id, userId: User.UserId());

        await _eventBus.PublishAsync(@event: new LibraryRefreshedEvent { QueryKey = ["music-playlists"] });

        return Ok(
            value: new StatusResponseDto<string>
            {
                Data = (
                    result > 0 ? "Playlist deleted successfully" : "Playlist not found"
                ).Localize(),
                Status = "ok",
            }
        );
    }

    [HttpPost]
    [Route(template: "{id:guid}/cover")]
    [Consumes(contentType: "multipart/form-data")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Cover(Guid id, IFormFile image)
    {
        Playlist? playlist = await _musicRepository.GetPlaylistForCoverAsync(id: id, userId: User.UserId());

        if (playlist is null)
            return NotFoundResponse(detail: "Playlist not found");

        string slug = playlist.Name.ToSlug();

        // save to app images folder
        string filePath2 = Path.Combine(path1: AppFiles.ImagesPath, path2: "music", path3: slug + ".jpg");
        _logger.LogInformation(message: filePath2);
        await using (FileStream stream = new(path: filePath2, mode: FileMode.Create))
        {
            await image.CopyToAsync(target: stream);
        }

        string cover = $"/{slug}.jpg";
        string colorPalette = await CoverArtImageManagerManager.ColorPalette(
            type: "cover",
            url: new(uriString: filePath2)
        );

        await _musicRepository.UpdatePlaylistCoverAsync(id: id, userId: User.UserId(), cover: cover, colorPalette: colorPalette);

        await _eventBus.PublishAsync(
            @event: new LibraryRefreshedEvent { QueryKey = ["music", "playlists", playlist.Id] }
        );

        playlist._colorPalette = colorPalette;

        return Ok(
            value: new StatusResponseDto<ImageUploadResponseDto>
            {
                Status = "ok",
                Message = "Playlist cover updated",
                Data = new()
                {
                    Url = new(uriString: $"/images/music/{slug}.jpg", uriKind: UriKind.Relative),
                    ColorPalette = playlist.ColorPalette,
                },
            }
        );
    }

    [HttpPost]
    [Route(template: "{id:guid}/tracks")]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> AddTrack(
        Guid id,
        [FromBody] CreatePlaylistTrackRequestDto request
    )
    {
        int result = await _musicRepository.AddPlaylistTrackAsync(playlistId: id, trackId: request.Id, userId: User.UserId());

        if (result < 0)
            return NotFoundResponse(detail: "Playlist not found");

        await _eventBus.PublishAsync(
            @event: new LibraryRefreshedEvent { QueryKey = ["music", "playlists", id] }
        );

        return Ok(
            value: new StatusResponseDto<string>
            {
                Data = (
                    result > 0 ? "Playlist updated successfully" : "No changes made"
                ).Localize(),
                Status = "ok",
            }
        );
    }

    [HttpDelete]
    [Route(template: "{id:guid}/tracks/{trackId:guid}")]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> AddTrack(Guid id, Guid trackId)
    {
        int result = await _musicRepository.RemovePlaylistTrackAsync(playlistId: id, trackId: trackId, userId: User.UserId());

        if (result < 0)
            return NotFoundResponse(detail: "Track not found in playlist");

        await _eventBus.PublishAsync(
            @event: new LibraryRefreshedEvent { QueryKey = ["music", "playlists", id] }
        );

        return Ok(
            value: new StatusResponseDto<string>
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
    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = null!;

    [JsonProperty(propertyName: "description")]
    public string? Description { get; set; }

    [JsonProperty(propertyName: "cover")]
    public string? Cover { get; set; }

    [JsonProperty(propertyName: "tracks")]
    public List<Guid> Tracks { get; set; } = [];
}

public class CreatePlaylistTrackRequestDto
{
    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }
}
