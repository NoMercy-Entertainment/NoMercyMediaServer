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
[ApiVersion(version: 1.0)]
[Tags(tags: "Music Artists")]
[Authorize(Policy = "MediaAccess")]
[Route(template: "api/v{version:apiVersion}/music/artists")]
public class ArtistsController : BaseController
{
    private readonly IMusicRepository _musicRepository;
    private readonly IEventBus _eventBus;
    private readonly IStorageFactory _storageFactory;

    private readonly ILogger<ArtistsController> _logger;

    public ArtistsController(
        ILogger<ArtistsController> logger,
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
    [Route(template: "/api/v{version:apiVersion}/music/artists/letter/{letter}")]
    public async Task<IActionResult> Index(string letter, [FromQuery] PageRequestDto request)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to view artists");

        // Lolomo with the "all" marker (`_`) returns one carousel per first-letter
        // bucket in alphabetical order, with the symbol bucket (#) at the end.
        if (request.Version == "lolomo" && (letter == "_" || letter == "all"))
        {
            List<ArtistCardDto> allCards = await _musicRepository.GetAllArtistCardsAsync(userId: userId);

            List<ComponentEnvelope> items = [Component.Container()];

            IOrderedEnumerable<IGrouping<string, ArtistCardDto>> groups = allCards
                .GroupBy(keySelector: a => BucketLetter(name: a.Name))
                .OrderBy(keySelector: g => g.Key == "#" ? "zz" : g.Key);

            foreach (IGrouping<string, ArtistCardDto> group in groups)
            {
                items.Add(
                    item: Component
                        .Carousel()
                        .WithId(id: $"artists-{group.Key.ToLowerInvariant()}")
                        .WithTitle(title: $"Artists: {group.Key}".Localize())
                        .WithItems(items: group.Select(selector: a => Component.MusicCard(data: new MusicCardData(artist: a))))
                );
            }

            return Ok(value: ComponentResponse.From(components: items));
        }

        List<ArtistCardDto> artistCards = await _musicRepository.GetArtistCardsAsync(
            userId: userId,
            letter: letter
        );

        string displayLetter = letter == "_" ? "#" : letter.ToUpperInvariant();

        if (request.Version == "lolomo")
        {
            List<ComponentEnvelope> items =
            [
                Component.Container(),
                Component
                    .Carousel()
                    .WithId(id: $"artists-{letter}")
                    .WithTitle(title: $"Artists: {displayLetter}".Localize())
                    .WithItems(items: artistCards.Select(selector: a => Component.MusicCard(data: new MusicCardData(artist: a)))),
            ];

            return Ok(value: ComponentResponse.From(components: items));
        }

        ComponentEnvelope grid = Component
            .Grid()
            .WithId(id: $"artists-{letter}")
            .WithTitle(title: $"Artists: {displayLetter}".Localize())
            .WithItems(items: artistCards.Select(selector: a => Component.MusicCard(data: new MusicCardData(artist: a))));

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
            return UnauthorizedResponse(detail: "You do not have permission to view artists");

        Artist? artist = await _musicRepository.GetArtistAsync(userId: userId, id: id);

        string country = Country();

        if (artist is null)
            return NotFoundResponse(detail: "Artist not found");

        // Fire-and-forget: enqueue serializes on the queue's global write lock, which
        // the busy encoder workers hold while touching the large queue DB. Awaiting it
        // inline made this read block for seconds. The palette is a background enrichment.
        if (string.IsNullOrEmpty(value: artist._colorPalette) || artist._colorPalette == "{}")
            _ = Task.Run(action: () =>
                QueueRunner.Current?.Dispatcher.Dispatch(
                    job: new ColorPaletteJob(entityType: "artist", entityId: artist.Id.ToString()),
                    onQueue: "palette",
                    priority: 1
                )
            );

        return Ok(value: new ArtistResponseDto { Data = new(artist: artist, userId: userId, country: country) });
    }

    [HttpPost]
    [Route(template: "{id:guid}/like")]
    public async Task<IActionResult> Like(Guid id, [FromBody] LikeRequestDto request)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to like artists");

        Artist? artist = await _musicRepository.GetArtistByIdAsync(id: id);

        if (artist is null)
            return UnprocessableEntityResponse(detail: "Artist not found");

        await _musicRepository.LikeArtistAsync(userId: userId, artist: artist, liked: request.Value);

        await _eventBus.PublishAsync(
            @event: new LibraryRefreshedEvent { QueryKey = ["music", "artist", artist.Id] }
        );

        await _eventBus.PublishAsync(
            @event: new MusicItemLikedEvent
            {
                UserId = User.UserId(),
                ItemId = artist.Id,
                ItemType = "artist",
                Liked = request.Value,
            }
        );

        return Ok(
            value: new StatusResponseDto<string>
            {
                Status = "ok",
                Message = "{0} {1}",
                Args = new object[] { artist.Name, request.Value ? "liked" : "unliked" },
            }
        );
    }

    [HttpPost]
    [Route(template: "{id:guid}/rescan")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Rescan(Guid id)
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

    [HttpDelete]
    [Route(template: "{id:guid}")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Destroy(Guid id)
    {
        bool deleted = await _musicRepository.DeleteArtistAsync(id: id);

        await _eventBus.PublishAsync(@event: new LibraryRefreshedEvent { QueryKey = ["music", "artist"] });

        return Ok(
            value: new StatusResponseDto<string>
            {
                Data = (deleted ? "Artist deleted successfully" : "Artist not found").Localize(),
                Status = "ok",
            }
        );
    }

    [HttpPatch]
    [Route(template: "{id:guid}")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Edit(Guid id, [FromBody] UpdateMusicMetadataRequestDto request)
    {
        Artist? artist = await _musicRepository.GetArtistForEditAsync(id: id);

        if (artist is null)
            return NotFoundResponse(detail: "Artist not found");

        string slug = artist.Name.ToSlug();
        string colorPalette = artist._colorPalette.OrEmpty();
        string cover = artist.Cover.OrEmpty();

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

        int result = await _musicRepository.UpdateArtistMetadataAsync(
            id: id,
            name: request.Name ?? artist.Name,
            description: request.Description,
            cover: cover,
            colorPalette: colorPalette
        );

        await _eventBus.PublishAsync(
            @event: new LibraryRefreshedEvent { QueryKey = ["music", "artist", id] }
        );

        return Ok(
            value: new StatusResponseDto<string>
            {
                Data = (result > 0 ? "Artist updated successfully" : "No changes made").Localize(),
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
        Artist? artist = await _musicRepository.GetArtistWithLibraryFolderAsync(id: id);

        if (artist is null)
            return NotFoundResponse(detail: "Artist not found");

        string slug = artist.Name.ToSlug();

        IStorage folderStorage = _storageFactory.For(
            folderId: artist.LibraryFolder.Id,
            driverId: artist.LibraryFolder.DriverId,
            subPath: string.Empty
        );
        // Resolve through the driver, not the IStorage facade: the facade's
        // GetFullPath is a LocalStorage-only escape hatch that throws on every
        // remote backend, so a facade call here 500'd cover uploads for
        // NFS / SMB / S3 / WebDAV libraries.
        string libraryRootFolder = folderStorage.Driver.GetFullPath(path: artist.LibraryFolder.Path);
        if (string.IsNullOrEmpty(value: libraryRootFolder))
            return UnprocessableEntityResponse(detail: "Artist library folder not found");

        // save to artist folder
        string filePath = Path.Combine(
            path1: libraryRootFolder,
            path2: artist.HostFolder.TrimStart(trimChar: '\\'),
            path3: slug + ".jpg"
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

        await _musicRepository.UpdateArtistCoverAsync(id: id, cover: cover, colorPalette: colorPalette);

        await _eventBus.PublishAsync(
            @event: new LibraryRefreshedEvent { QueryKey = ["music", "artist", artist.Id] }
        );

        artist._colorPalette = colorPalette;

        return Ok(
            value: new StatusResponseDto<ImageUploadResponseDto>
            {
                Status = "ok",
                Message = "Artist cover updated",
                Data = new()
                {
                    Url = new(uriString: $"/images/music/{slug}.jpg", uriKind: UriKind.Relative),
                    ColorPalette = artist.ColorPalette,
                },
            }
        );
    }
}
