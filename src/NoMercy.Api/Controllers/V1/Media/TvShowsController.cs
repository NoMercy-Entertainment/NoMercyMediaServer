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

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Authorization;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.TvShows;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.KitsuIo;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.TV;
using IJobDispatcher = NoMercy.MediaProcessing.Jobs.IJobDispatcher;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags(tags: "Media TV Shows")]
[ApiVersion(version: 1.0)]
[Authorize]
[Route(template: "api/v{version:apiVersion}/tv/{id:int}")] // match themoviedb.org API
public class TvShowsController(
    ITvShowRepository tvShowRepository,
    ILibraryRepository libraryRepository,
    IJobDispatcher jobDispatcher,
    ITvShowMetadataProvider tvShowMetadataProvider,
    IEventBus eventBus,
    ILogger<TvShowsController> logger
) : BaseController
{
    [HttpGet]
    [ResponseCache(Duration = 120)]
    public async Task<IActionResult> Tv(int id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to view tv shows");

        string language = Language();
        string country = Country();

        TvDetail? tvDetail = await tvShowRepository.GetTvAsync(userId: userId, id: id, language: language, country: country, ct: ct);

        if (tvDetail is not null)
            return Ok(
                value: new InfoResponseDto
                {
                    Data = new(tv: tvDetail.Tv, country: country, similars: tvDetail.Similars, recommendations: tvDetail.Recommendations),
                }
            );

        TmdbTvShowAppends? tvShowAppends = await tvShowMetadataProvider.GetTvShowAsync(
            id: id,
            language: language,
            ct: ct
        );

        if (tvShowAppends is null)
            return NotFoundResponse(detail: "Tv show not found");

        // await _tvShowRepository.AddTvShowAsync(id);

        return Ok(value: new InfoResponseDto { Data = new(tmdbTv: tvShowAppends, country: country) });
    }

    [HttpDelete]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> DeleteTv(int id, CancellationToken ct = default)
    {
        await tvShowRepository.DeleteAsync(id: id, ct: ct);

        await eventBus.PublishAsync(@event: new LibraryRefreshedEvent { QueryKey = ["tv", id.ToString()] });
        await eventBus.PublishAsync(@event: new LibraryRefreshedEvent { QueryKey = ["libraries"] });
        await eventBus.PublishAsync(@event: new LibraryRefreshedEvent { QueryKey = ["home"] });
        await eventBus.PublishAsync(@event: new LibraryRefreshedEvent { QueryKey = ["continue-watching"] });

        return Ok(value: new StatusResponseDto<string> { Status = "ok", Message = "Show deleted" });
    }

    [HttpGet]
    [Route(template: "available")]
    public async Task<IActionResult> Available(int id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to view tv shows");

        bool available = await tvShowRepository.GetTvAvailableAsync(userId: userId, id: id, ct: ct);

        if (!available)
            return NotFoundResponse(detail: "Tv show not found");

        return Ok(
            value: new StatusResponseDto<AvailableResponseDto>
            {
                Data = new() { Available = true },
                Status = "ok",
                Message = "Tv show is available",
            }
        );
    }

    [HttpGet]
    [Route(template: "watch")]
    public async Task<IActionResult> Watch(int id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to view tv shows");

        string language = Language();
        string country = Country();

        Tv? tv = await tvShowRepository.GetPlaylistAsync(userId: userId, id: id, language: language, country: country, ct: ct);

        if (tv is null)
            return NotFoundResponse(detail: "Tv show not found");

        VideoPlaylistResponseDto[] episodes = tv
            .Seasons.Where(predicate: season => season.SeasonNumber > 0)
            .SelectMany(selector: season => season.Episodes)
            .Select(selector: episode => new VideoPlaylistResponseDto(episode: episode, playlistType: "tv", playlistId: id, country: country))
            .ToArray();

        VideoPlaylistResponseDto[] extras = tv
            .Seasons.Where(predicate: season => season.SeasonNumber == 0)
            .SelectMany(selector: season => season.Episodes)
            .Select(selector: episode => new VideoPlaylistResponseDto(episode: episode, playlistType: "tv", playlistId: id, country: country))
            .ToArray();

        VideoPlaylistResponseDto[] result = episodes
            .Concat(second: extras)
            .Where(predicate: episode => episode.Id != 0)
            .ToArray();

        return Ok(value: result);
    }

    [HttpPost]
    [Route(template: "like")]
    public async Task<IActionResult> Like(
        int id,
        [FromBody] LikeRequestDto request,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to like tv shows");

        bool success = await tvShowRepository.LikeAsync(id: id, userId: userId, like: request.Value, ct: ct);

        if (!success)
            return UnprocessableEntityResponse(detail: "Tv show not found");

        return Ok(
            value: new StatusResponseDto<string>
            {
                Status = "ok",
                Message = "{1}",
                Args = new object[] { request.Value ? "liked" : "unliked" },
            }
        );
    }

    [HttpPost]
    [Route(template: "watch-list")]
    public async Task<IActionResult> AddToWatchList(
        int id,
        [FromBody] WatchListRequestDto request,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to manage watch list");

        bool success = await tvShowRepository.AddToWatchListAsync(tvId: id, userId: userId, add: request.Add, ct: ct);

        if (!success)
            return UnprocessableEntityResponse(detail: "Tv show not found");

        return Ok(
            value: new StatusResponseDto<string>
            {
                Status = "ok",
                Message = request.Add
                    ? "Tv show added to watch list"
                    : "Tv show removed from watch list",
            }
        );
    }

    [HttpPost]
    [Route(template: "rescan")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Rescan(int id, CancellationToken ct = default)
    {
        Tv? tv = await tvShowRepository.GetTvWithLibraryAsync(id: id, ct: ct);

        if (tv is null)
            return UnprocessableEntityResponse(detail: "Tv show not found");

        try
        {
            jobDispatcher.DispatchJob<FileRescanJob>(id: id, libraryId: tv.LibraryId);
        }
        catch (Exception e)
        {
            logger.LogError(message: e.Message);
            return InternalServerErrorResponse(detail: e.Message);
        }

        return Ok(
            value: new StatusResponseDto<string>
            {
                Status = "ok",
                Message = "Rescanning {0} for files in the background",
                Args = [tv.Title],
            }
        );
    }

    [HttpPost]
    [Route(template: "refresh")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Refresh(
        int id,
        [FromQuery] Ulid? libraryId = null,
        CancellationToken ct = default
    )
    {
        Tv? tv = await tvShowRepository.GetTvWithLibraryAsync(id: id, ct: ct);

        if (tv is null)
            return UnprocessableEntityResponse(detail: "Tv show not found");

        Ulid targetLibraryId;

        if (libraryId is not null)
        {
            Library? specified = await libraryRepository.GetLibraryByIdLiteAsync(
                id: libraryId.Value,
                ct: ct
            );

            if (specified is null)
                return NotFoundResponse(detail: "Library not found");

            targetLibraryId = specified.Id;
        }
        else
        {
            TmdbTvShowDetails? show = await tvShowMetadataProvider.GetTvShowDetailsAsync(id: id, ct: ct);
            if (show == null)
                return NotFoundResponse(detail: "Tv show not found");

            bool isAnime = await KitsuIoClient.IsAnime(title: show.Name, year: show.FirstAirDate.ParseYear());

            // Require Japanese origin to avoid false positives on western co-productions
            if (
                isAnime
                && !show.OriginCountry.Any(predicate: c =>
                    string.Equals(a: c, b: "JP", comparisonType: StringComparison.OrdinalIgnoreCase)
                )
            )
                isAnime = false;

            Library? tvLibrary = await libraryRepository.GetLibraryByTypeAsync(
                type: isAnime ? "anime" : "tv",
                fallbackType: "tv",
                ct: ct
            );

            targetLibraryId = tvLibrary?.Id ?? tv.Library.Id;
        }

        jobDispatcher.DispatchJob<ShowImportJob>(id: id, libraryId: targetLibraryId);

        return Ok(
            value: new StatusResponseDto<string>
            {
                Status = "ok",
                Message = "Refreshing {0} data in background",
                Args = [tv.Title],
            }
        );
    }

    [HttpPost]
    [Route(template: "add")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Add(
        int id,
        [FromQuery] Ulid? libraryId = null,
        CancellationToken ct = default
    )
    {
        Library? library;

        if (libraryId is not null)
        {
            library = await libraryRepository.GetLibraryByIdLiteAsync(id: libraryId.Value, ct: ct);

            if (library is null)
                return NotFoundResponse(detail: "Library not found");
        }
        else
        {
            TmdbTvShowDetails? show = await tvShowMetadataProvider.GetTvShowDetailsAsync(id: id, ct: ct);
            if (show == null)
                return NotFoundResponse(detail: "Tv show not found");

            bool isAnime = await KitsuIoClient.IsAnime(title: show.Name, year: show.FirstAirDate.ParseYear());

            if (
                isAnime
                && !show.OriginCountry.Any(predicate: c =>
                    string.Equals(a: c, b: "JP", comparisonType: StringComparison.OrdinalIgnoreCase)
                )
            )
                isAnime = false;

            library = await libraryRepository.GetLibraryByTypeAsync(
                type: isAnime ? "anime" : "tv",
                fallbackType: "tv",
                ct: ct
            );

            if (library is null)
                return UnprocessableEntityResponse(detail: "No Tv library found");
        }

        try
        {
            jobDispatcher.DispatchJob<ShowImportJob>(id: id, libraryId: library.Id);
        }
        catch (Exception e)
        {
            logger.LogError(message: e.Message);
            return InternalServerErrorResponse(detail: e.Message);
        }

        return Ok(
            value: new StatusResponseDto<string>
            {
                Status = "ok",
                Message = "Adding {0} in the background",
                Args = [library.Title],
            }
        );
    }

    [HttpGet]
    [Route(template: "missing")]
    public async Task<IActionResult> Missing(int id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to view library");
        string language = Language();

        IEnumerable<Episode> episodes = await tvShowRepository.GetMissingLibraryShows(
            userId: userId,
            id: id,
            language: language,
            ct: ct
        );

        List<IGrouping<long, MissingEpisodeDto>> concat = episodes
            .Select(selector: episode => new MissingEpisodeDto(episode: episode))
            .OrderBy(keySelector: episode => episode.SeasonNumber)
            .ThenBy(keySelector: episode => episode.EpisodeNumber)
            .GroupBy(keySelector: episode => episode.SeasonNumber)
            .ToList();

        if (concat.Count == 0)
        {
            SeasonCardData noItems = new()
            {
                Id = 0,
                Title = "No missing episodes",
                SeasonNumber = 0,
                EpisodeNumber = 0,
                Overview = "There are no missing episodes in this season.",
                Available = false,
            };

            return Ok(
                value: ComponentResponse.From(
                    component: Component
                        .Grid()
                        .WithId(id: "missing-episodes-empty")
                        .WithItems(items: Component.SeasonCard(data: noItems).WithWatch().Build())
                )
            );
        }

        return Ok(
            value: ComponentResponse.From(
                component: Component
                    .List()
                    .WithId(id: "missing-episodes")
                    .WithItems(
                        items: concat.SelectMany(selector: seasonGroup =>
                            new ComponentEnvelope[]
                            {
                                // Season title component
                                Component
                                    .SeasonTitle(data: new(seasonNumber: (int)seasonGroup.Key, episodeCount: seasonGroup.Count()))
                                    .WithId(id: $"season-{seasonGroup.Key}-title"),
                                // Episodes grid for this season
                                Component
                                    .Grid()
                                    .WithId(id: $"season-{seasonGroup.Key}-episodes")
                                    .WithProperties(properties: new() { { "paddingTop", 16 } })
                                    .WithItems(
                                        builders: seasonGroup.Select(selector: episode =>
                                            Component.SeasonCard(data: new(dto: episode)).WithWatch()
                                        )
                                    ),
                            }
                        )
                    )
            )
        );
    }
}
