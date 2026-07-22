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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Api.DTOs.Music;
using NoMercy.Authorization;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Music;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Events.Music;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs.PaletteJobs;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.Storage;
using NoMercyQueue;

namespace NoMercy.Api.Controllers.V1.Music;

[ApiController]
[Tags(tags: "Music Albums")]
[Authorize(Policy = "MediaAccess")]
[Route(template: "api/v{version:apiVersion}/music/albums")]
public class AlbumsController : BaseController
{
    private readonly IMusicRepository _musicRepository;
    private readonly IEventBus _eventBus;
    private readonly IStorageFactory _storageFactory;

    private readonly ILogger<AlbumsController> _logger;

    public AlbumsController(
        ILogger<AlbumsController> logger,
        IMusicRepository musicService,
        IEventBus eventBus,
        IStorageFactory storageFactory
    )
    {
        _logger = logger;
        _musicRepository = musicService;
        _eventBus = eventBus;
        _storageFactory = storageFactory;
    }

    [HttpGet]
    [Route(template: "/api/v{version:apiVersion}/music/albums/letter/{letter}")]
    public async Task<IActionResult> Index(string letter, [FromQuery] PageRequestDto request)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to view albums");

        string language = Language();

        // Lolomo with the "all" marker (`_`) returns one carousel per first-letter
        // bucket in alphabetical order, with the symbol bucket (#) at the end.
        if (request.Version == "lolomo" && (letter == "_" || letter == "all"))
        {
            List<AlbumCardDto> allCards = await _musicRepository.GetAllAlbumCardsAsync(
                userId: userId,
                language: language
            );

            List<ComponentEnvelope> items = [Component.Container()];

            IOrderedEnumerable<IGrouping<string, AlbumCardDto>> groups = allCards
                .GroupBy(keySelector: a => BucketLetter(name: a.Name))
                .OrderBy(keySelector: g => g.Key == "#" ? "zz" : g.Key);

            foreach (IGrouping<string, AlbumCardDto> group in groups)
            {
                items.Add(
                    item: Component
                        .Carousel()
                        .WithId(id: $"albums-{group.Key.ToLowerInvariant()}")
                        .WithTitle(title: $"Albums: {group.Key}".Localize())
                        .WithItems(items: group.Select(selector: a => Component.MusicCard(data: new MusicCardData(album: a))))
                );
            }

            return Ok(value: ComponentResponse.From(components: items));
        }

        List<AlbumCardDto> albumCards = await _musicRepository.GetAlbumCardsAsync(
            userId: userId,
            letter: letter,
            language: language
        );

        string displayLetter = letter == "_" ? "#" : letter.ToUpperInvariant();

        if (request.Version == "lolomo")
        {
            List<ComponentEnvelope> items =
            [
                Component.Container(),
                Component
                    .Carousel()
                    .WithId(id: $"albums-{letter}")
                    .WithTitle(title: $"Albums: {displayLetter}".Localize())
                    .WithItems(items: albumCards.Select(selector: a => Component.MusicCard(data: new MusicCardData(album: a)))),
            ];

            return Ok(value: ComponentResponse.From(components: items));
        }

        ComponentEnvelope grid = Component
            .Grid()
            .WithId(id: $"albums-{letter}")
            .WithTitle(title: $"Albums: {displayLetter}".Localize())
            .WithItems(items: albumCards.Select(selector: a => Component.MusicCard(data: new MusicCardData(album: a))));

        return Ok(value: ComponentResponse.From(component: grid));
    }

    private static string BucketLetter(string name)
    {
        if (string.IsNullOrEmpty(value: name))
            return "#";
        char first = char.ToLowerInvariant(c: name[index: 0]);
        return first is >= 'a' and <= 'z' ? first.ToString().ToUpperInvariant() : "#";
    }

    [HttpGet]
    [Route(template: "{id:guid}")]
    public async Task<IActionResult> Show(Guid id)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to view albums");

        string language = Language();

        Album? album = await _musicRepository.GetAlbumAsync(userId: userId, id: id);

        if (album is null)
            return NotFoundResponse(detail: "Albums not found");

        // Fire-and-forget: enqueue takes the queue's global write lock (held by the
        // encoder workers), so dispatching inline blocked this read for seconds.
        if (string.IsNullOrEmpty(value: album._colorPalette) || album._colorPalette == "{}")
            _ = Task.Run(action: () =>
                QueueRunner.Current?.Dispatcher.Dispatch(
                    job: new ColorPaletteJob(entityType: "album", entityId: album.Id.ToString()),
                    onQueue: "palette",
                    priority: 1
                )
            );

        return Ok(value: new AlbumResponseDto { Data = new(album: album, country: language) });
    }

    [HttpPost]
    [Route(template: "{id:guid}/like")]
    public async Task<IActionResult> Like(Guid id, [FromBody] LikeRequestDto request)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to like albums");

        Album? album = await _musicRepository.GetAlbumAsync(userId: userId, id: id);

        if (album is null)
            return UnprocessableEntityResponse(detail: "Albums not found");

        await _musicRepository.LikeAlbumAsync(userId: userId, album: album, liked: request.Value);

        await _eventBus.PublishAsync(
            @event: new LibraryRefreshedEvent { QueryKey = ["music", "album", album.Id] }
        );

        await _eventBus.PublishAsync(
            @event: new MusicItemLikedEvent
            {
                UserId = User.UserId(),
                ItemId = album.Id,
                ItemType = "album",
                Liked = request.Value,
            }
        );

        return Ok(
            value: new StatusResponseDto<string>
            {
                Status = "ok",
                Message = "{0} {1}",
                Args = new object[] { album.Name, request.Value ? "liked" : "unliked" },
            }
        );
    }

    [HttpPost]
    [Route(template: "{id:guid}/rescan")]
    [Authorize(Policy = "Moderator")]
    public IActionResult Rescan(Guid id)
    {
        return Ok(
            value: new StatusResponseDto<string>
            {
                Status = "ok",
                Message = "Rescan started",
                Args = [],
            }
        );
    }

    [HttpPatch]
    [Route(template: "{id:guid}")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Edit(Guid id, [FromBody] CreatePlaylistRequestDto request)
    {
        Album? album = await _musicRepository.GetAlbumForEditAsync(id: id);

        if (album is null)
            return NotFoundResponse(detail: "Album not found");

        string slug = album.Name.ToSlug();
        string colorPalette = album._colorPalette.OrEmpty();
        string cover = album.Cover.OrEmpty();

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

        int result = await _musicRepository.UpdateAlbumMetadataAsync(
            id: id,
            name: request.Name,
            description: request.Description,
            cover: cover,
            colorPalette: colorPalette
        );

        await _eventBus.PublishAsync(
            @event: new LibraryRefreshedEvent { QueryKey = ["music", "album", id] }
        );

        return Ok(
            value: new StatusResponseDto<string>
            {
                Data = (result > 0 ? "Album updated successfully" : "No changes made").Localize(),
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
        Album? album = await _musicRepository.GetAlbumWithLibraryFolderAsync(id: id);

        if (album is null)
            return NotFoundResponse(detail: "Album not found");

        string slug = album.Name.ToSlug();

        IStorage folderStorage = _storageFactory.For(
            folderId: album.LibraryFolder.Id,
            driverId: album.LibraryFolder.DriverId,
            subPath: string.Empty
        );
        // Resolve through the driver, not the IStorage facade: the facade's
        // GetFullPath is a LocalStorage-only escape hatch that throws on every
        // remote backend, so a facade call here 500'd cover uploads for
        // NFS / SMB / S3 / WebDAV libraries.
        string libraryRootFolder = folderStorage.Driver.GetFullPath(path: album.LibraryFolder.Path);
        if (string.IsNullOrEmpty(value: libraryRootFolder))
            return UnprocessableEntityResponse(detail: "Album library folder not found");

        // save to album folder
        string filePath = Path.Combine(
            path1: libraryRootFolder,
            path2: album.HostFolder.TrimStart(trimChar: '\\'),
            path3: "cover.jpg"
        );
        _logger.LogInformation(message: filePath);
        await using (FileStream stream = new(path: filePath, mode: FileMode.Create))
        {
            await image.CopyToAsync(target: stream);
        }

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

        await _musicRepository.UpdateAlbumCoverAsync(id: id, cover: cover, colorPalette: colorPalette);

        album._colorPalette = colorPalette;

        return Ok(
            value: new StatusResponseDto<ImageUploadResponseDto>
            {
                Status = "ok",
                Message = "Album cover updated",
                Data = new()
                {
                    Url = new(uriString: $"/images/music/{slug}.jpg", uriKind: UriKind.Relative),
                    ColorPalette = album.ColorPalette,
                },
            }
        );
    }
}
