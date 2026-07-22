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
using NoMercy.Database.Models.Movies;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Collections;
using IJobDispatcher = NoMercy.MediaProcessing.Jobs.IJobDispatcher;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags(tags: "Media Collections")]
[ApiVersion(version: 1.0)]
[Authorize]
[Route(template: "api/v{version:apiVersion}/collection/{id:int}")] // match themoviedb.org API
public class CollectionsController(
    ICollectionRepository collectionRepository,
    ILibraryRepository libraryRepository,
    IJobDispatcher jobDispatcher,
    ICollectionMetadataProvider collectionMetadataProvider,
    ILogger<CollectionsController> logger
) : BaseController
{
    [HttpGet]
    [Route(template: "/api/v{version:apiVersion}/collection")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = ["take", "page", "version"])]
    public async Task<IActionResult> Collections(
        [FromQuery] PageRequestDto request,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to view collections");

        string language = Language();
        string country = Country();

        // Use optimized query that projects only needed data
        List<CollectionListDto> collectionDtos = await collectionRepository.GetCollectionsListAsync(
            userId: userId,
            language: language,
            country: country,
            take: request.Take,
            page: request.Page
        );

        if (request.Version != "lolomo")
        {
            List<CardData> cardItems = collectionDtos.Select(selector: dto => new CardData(dto: dto)).ToList();

            ComponentEnvelope response = Component
                .Grid()
                .WithItems(builders: cardItems.Select(selector: item => Component.Card().WithData(data: item)));

            return Ok(value: ComponentResponse.From(component: response));
        }

        List<ComponentEnvelope> carousels = Letters
            .Select(
                selector: (letter, index) =>
                {
                    List<CardData> letterItems = collectionDtos
                        .Select(selector: dto => new CardData(dto: dto))
                        .Where(predicate: card => AlphaBucket.Matches(titleSort: card.TitleSort, bucket: letter))
                        .OrderBy(keySelector: item => item.TitleSort)
                        .ToList();

                    return Component
                        .Carousel()
                        .WithId(id: letter)
                        .WithTitle(title: letter)
                        .WithNavigation(
                            previousId: index == 0 ? null : Letters[index - 1],
                            nextId: index == Letters.Length - 1 ? null : Letters[index + 1]
                        )
                        .WithItems(builders: letterItems.Select(selector: item => Component.Card().WithData(data: item)))
                        .Build();
                }
            )
            .ToList();

        ComponentEnvelope containerResponse = Component.Container().WithItems(items: carousels);

        return Ok(value: containerResponse);
    }

    [HttpGet]
    [ResponseCache(Duration = 300)]
    public async Task<IActionResult> Collection(int id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to view collections");

        string language = Language();
        string country = Country();

        Collection? collection = await collectionRepository.GetCollectionAsync(
            userId: userId,
            id: id,
            language: language,
            country: country
        );

        if (
            collection is not null
            && collection.CollectionMovies.Count > 0
            && collection.Images.Count > 0
        )
            return Ok(value: new CollectionResponseDto { Data = new(collection: collection) });

        TmdbCollectionAppends? collectionAppends =
            await collectionMetadataProvider.GetCollectionAsync(id: id, language: language, ct: ct);

        if (collectionAppends is null)
            return NotFoundResponse(detail: "Collection not found");

        return Ok(value: new CollectionResponseDto { Data = new(tmdbCollectionAppends: collectionAppends) });
    }

    [HttpGet]
    [Route(template: "available")]
    public async Task<IActionResult> Available(int id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to view collections");

        Collection? collection = await collectionRepository.GetAvailableCollectionAsync(userId: userId, id: id);

        bool available =
            collection is not null
            && collection.CollectionMovies.Select(selector: movie => movie.Movie.VideoFiles).Any();

        if (!available)
            return NotFoundResponse(detail: "Collection not found");

        return Ok(
            value: new StatusResponseDto<AvailableResponseDto>
            {
                Data = new() { Available = true },
                Status = "ok",
                Message = "Collection is available",
            }
        );
    }

    [HttpGet]
    [Route(template: "watch")]
    public async Task<IActionResult> Watch(int id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to view collections");

        string language = Language();
        string country = Country();

        Collection? collection = await collectionRepository.GetCollectionPlaylistAsync(
            userId: userId,
            id: id,
            language: language,
            country: country
        );

        if (collection is null)
            return NotFoundResponse(detail: "Collection not found");

        return Ok(
            value: collection.CollectionMovies.Select(
                selector: (movie, index) =>
                    new VideoPlaylistResponseDto(
                        movie: movie.Movie,
                        playlistType: "collection",
                        playlistId: id,
                        country: country,
                        index: index + 1,
                        collection: collection
                    )
            )
        );
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
            return UnauthorizedResponse(detail: "You do not have permission to like collections");

        bool success = await collectionRepository.LikeAsync(id: id, userId: userId, like: request.Value, ct: ct);

        if (!success)
            return UnprocessableEntityResponse(detail: "Collection not found");

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

        bool success = await collectionRepository.AddToWatchListAsync(collectionId: id, userId: userId, add: request.Add);

        if (!success)
            return UnprocessableEntityResponse(detail: "Collection not found");

        return Ok(
            value: new StatusResponseDto<string>
            {
                Status = "ok",
                Message = request.Add
                    ? "Collection added to watch list"
                    : "Collection removed from watch list",
            }
        );
    }

    [HttpDelete]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> DeleteMovie(int id, CancellationToken ct = default)
    {
        await collectionRepository.DeleteAsync(id: id, ct: ct);

        return Ok(value: new StatusResponseDto<string> { Status = "ok", Message = "Movie deleted" });
    }

    [HttpPost]
    [Route(template: "rescan")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Rescan(int id, CancellationToken ct = default)
    {
        Collection? collection = await collectionRepository.GetCollectionForRescanAsync(id: id, ct: ct);

        if (collection is null)
            return UnprocessableEntityResponse(detail: "Collection not found");

        try
        {
            foreach (CollectionMovie collectionMovie in collection.CollectionMovies)
            {
                jobDispatcher.DispatchJob<FileRescanJob>(
                    id: collectionMovie.MovieId,
                    libraryId: collectionMovie.Movie.LibraryId
                );
            }
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
                Args = [collection.Title],
            }
        );
    }

    [HttpPost]
    [Route(template: "refresh")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Refresh(int id, CancellationToken ct = default)
    {
        Collection? collection = await collectionRepository.GetCollectionWithMovieLibrariesAsync(
            id: id,
            ct: ct
        );

        if (collection is null)
            return UnprocessableEntityResponse(detail: "Collection not found");

        try
        {
            foreach (CollectionMovie collectionMovie in collection.CollectionMovies)
            {
                jobDispatcher.DispatchJob<MovieImportJob>(
                    id: collectionMovie.MovieId,
                    libraryId: collectionMovie.Movie.LibraryId
                );
            }
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
                Message = "Refreshing {0} in the background",
                Args = [collection.Title],
            }
        );
    }

    [HttpPost]
    [Route(template: "add")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Add(int id, CancellationToken ct = default)
    {
        Library? library = await libraryRepository.GetLibraryByTypeAsync(
            type: MediaTypes.MovieMediaType,
            ct: ct
        );

        if (library is null)
            return UnprocessableEntityResponse(detail: "No movie library found");

        Collection? collection = await collectionRepository.GetCollectionWithMovieLibrariesAsync(
            id: id,
            ct: ct
        );

        if (collection is null)
            return UnprocessableEntityResponse(detail: "Collection not found");

        try
        {
            foreach (CollectionMovie collectionMovie in collection.CollectionMovies)
            {
                jobDispatcher.DispatchJob<MovieImportJob>(
                    id: collectionMovie.MovieId,
                    libraryId: collectionMovie.Movie.LibraryId
                );
            }
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
}
