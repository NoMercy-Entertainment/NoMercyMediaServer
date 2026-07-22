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
using NoMercy.Authorization;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Movies;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Information;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using IJobDispatcher = NoMercy.MediaProcessing.Jobs.IJobDispatcher;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags(tags: "Media Movies")]
[ApiVersion(version: 1.0)]
[Authorize]
[Route(template: "api/v{version:apiVersion}/movie/{id:int}")] // match themoviedb.org API
public class MoviesController(
    IMovieRepository movieRepository,
    ILibraryRepository libraryRepository,
    IJobDispatcher jobDispatcher,
    IMovieMetadataProvider movieMetadataProvider,
    IServerConfiguration config,
    IEventBus eventBus,
    ILogger<MoviesController> logger
) : BaseController
{
    [HttpGet]
    [ResponseCache(Duration = 120)]
    public async Task<IActionResult> Movie(int id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to view movies");

        string language = Language();
        string country = Country();

        Movie? movie = await movieRepository.GetMovieDetailAsync(userId: userId, id: id, language: language, country: country, ct: ct);

        if (movie is not null)
            return Ok(value: new InfoResponseDto { Data = new(movie: movie, country: country) });

        try
        {
            TmdbMovieAppends? movieAppends = await movieMetadataProvider.GetMovieAsync(
                id: id,
                language: language,
                ct: ct
            );

            if (movieAppends is null)
                return NotFoundResponse(detail: "Movie not found");

            if (movieAppends.Adult && !config.ShowAdultContent)
                return UnauthorizedResponse(
                    detail: "Movie is adult which is not allowed by the server configuration"
                );

            return Ok(value: new InfoResponseDto { Data = new(tmdbMovie: movieAppends, country: country) });
        }
        catch (Exception)
        {
            return NotFoundResponse(detail: "Movie not found");
        }
    }

    [HttpDelete]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> DeleteMovie(int id, CancellationToken ct = default)
    {
        await movieRepository.DeleteAsync(id: id, ct: ct);

        await eventBus.PublishAsync(
            @event: new LibraryRefreshedEvent { QueryKey = ["movie", id.ToString()] }
        );
        await eventBus.PublishAsync(@event: new LibraryRefreshedEvent { QueryKey = ["libraries"] });
        await eventBus.PublishAsync(@event: new LibraryRefreshedEvent { QueryKey = ["home"] });
        await eventBus.PublishAsync(@event: new LibraryRefreshedEvent { QueryKey = ["continue-watching"] });

        return Ok(value: new StatusResponseDto<string> { Status = "ok", Message = "Movie deleted" });
    }

    [HttpGet]
    [Route(template: "available")]
    public async Task<IActionResult> Available(int id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to view movies");

        string language = Language();
        string country = Country();

        bool available = await movieRepository.GetMovieAvailableAsync(userId: userId, id: id, ct: ct);

        if (!available)
            return NotFoundResponse(detail: "Movie not found");

        return Ok(
            value: new StatusResponseDto<AvailableResponseDto>
            {
                Data = new() { Available = true },
                Status = "ok",
                Message = "Movie is available",
            }
        );
    }

    [HttpGet]
    [Route(template: "watch")]
    public async Task<IActionResult> Watch(int id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to view movies");

        string language = Language();
        string country = Country();

        IEnumerable<VideoPlaylistResponseDto> playlist = (
            await movieRepository.GetMoviePlaylistAsync(userId: userId, id: id, language: language, country: country, ct: ct)
        ).Select(selector: movie => new VideoPlaylistResponseDto(
            movie: movie,
            playlistType: MediaTypes.MovieMediaType,
            playlistId: id,
            country: country
        ));

        if (!playlist.Any())
            return NotFoundResponse(detail: "Movie not found");

        return Ok(value: playlist);
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
            return UnauthorizedResponse(detail: "You do not have permission to like movies");

        bool success = await movieRepository.LikeMovieAsync(id: id, userId: userId, like: request.Value, ct: ct);

        if (!success)
            return UnprocessableEntityResponse(detail: "Movie not found");

        return Ok(
            value: new StatusResponseDto<string>
            {
                Status = "ok",
                Message = "{0}: {1}",
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

        bool success = await movieRepository.AddToWatchListAsync(movieId: id, userId: userId, add: request.Add, ct: ct);

        if (!success)
            return UnprocessableEntityResponse(detail: "Movie not found");

        return Ok(
            value: new StatusResponseDto<string>
            {
                Status = "ok",
                Message = request.Add
                    ? "Movie added to watch list"
                    : "Movie removed from watch list",
            }
        );
    }

    [HttpPost]
    [Route(template: "rescan")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Rescan(int id, CancellationToken ct = default)
    {
        Movie? movie = await movieRepository.GetMovieForRescanAsync(id: id, ct: ct);

        if (movie is null)
            return UnprocessableEntityResponse(detail: "Movie not found");

        try
        {
            jobDispatcher.DispatchJob<FileRescanJob>(id: id, libraryId: movie.LibraryId);
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
                Args = [movie.Title],
            }
        );
    }

    [HttpPost]
    [Route(template: "refresh")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Refresh(int id, CancellationToken ct = default)
    {
        Movie? movie = await movieRepository.GetMovieForRefreshAsync(id: id, ct: ct);

        if (movie is null)
            return UnprocessableEntityResponse(detail: "Movie not found");

        try
        {
            jobDispatcher.DispatchJob<MovieImportJob>(id: id, libraryId: movie.Library.Id);
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
                Args = [movie.Title],
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
            library = await libraryRepository.GetLibraryByTypeAsync(
                type: MediaTypes.MovieMediaType,
                fallbackType: null,
                ct: ct
            );

            if (library is null)
                return UnprocessableEntityResponse(detail: "No movie library found");
        }

        try
        {
            jobDispatcher.DispatchJob<MovieImportJob>(id: id, libraryId: library.Id);
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
