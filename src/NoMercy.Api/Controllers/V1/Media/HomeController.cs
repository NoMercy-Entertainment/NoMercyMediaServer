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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Api.Services;
using NoMercy.Authorization;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags(tags: "Media")]
[ApiVersion(version: 1.0)]
[Authorize]
[Route(template: "api/v{version:apiVersion}")]
public partial class HomeController : BaseController
{
    // YouTube video ids are exactly 11 chars of [A-Za-z0-9_-]. trailerId flows
    // into shell command strings (yt-dlp/ffmpeg) and filesystem paths, so a
    // strict match is the trust boundary that blocks command injection and
    // path traversal before the value reaches Shell.Exec* or Path.Combine.
    [GeneratedRegex(pattern: "^[A-Za-z0-9_-]{11}$")]
    private static partial Regex TrailerIdRegex();

    private readonly HomeService _homeService;
    private readonly IDbContextFactory<MediaContext> _contextFactory;
    private readonly IStorage _transcodeStorage;

    private readonly ILogger<HomeController> _logger;

    public HomeController(
        ILogger<HomeController> logger,
        HomeService homeService,
        IDbContextFactory<MediaContext> contextFactory,
        [FromKeyedServices(key: "transcode")] IStorage transcodeStorage
    )
    {
        _logger = logger;
        _homeService = homeService;
        _contextFactory = contextFactory;
        _transcodeStorage = transcodeStorage;
    }

    [HttpGet]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> Index(
        [FromQuery] PageRequestDto request,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();
        string language = Language();
        string country = Country();

        List<GenreRowDto<GenreRowItemDto>> result = await _homeService.GetHomePageContent(
            userId: userId,
            language: language,
            country: country,
            request: request
        );

        List<GenreRowDto<GenreRowItemDto>> newData = result.ToList();
        bool hasMore = newData.Count >= request.Take;

        newData = newData.Take(count: request.Take).ToList();

        PaginatedResponse<GenreRowDto<GenreRowItemDto>> response = new()
        {
            Data = newData,
            NextPage = hasMore ? request.Page + 1 : null,
            HasMore = hasMore,
        };

        if (request.Page != 0)
            return Ok(value: response);

        // "Latest in {library}" carousels belong to the non-lolomo home only;
        // the lolomo (mobile/TV) variant lays those library rows out itself.
        if (request.Version == "lolomo")
            return Ok(value: response);

        LibraryRepository libraryRepository = new(contextFactory: _contextFactory);
        List<Library> libraries = await libraryRepository.GetLibrariesLite(userId: userId, ct: ct);

        // Fetch all library data in parallel - each task needs its own MediaContext for thread safety
        Task<(Library library, List<Movie> movies, List<Tv> shows)>[] libraryDataTasks = libraries
            .Select(selector: async library =>
            {
                await using MediaContext context = await _contextFactory.CreateDbContextAsync(cancellationToken: ct);
                List<Movie> libraryMovies = [];
                await foreach (
                    Movie movie in libraryRepository
                        .GetLibraryMovies(
                            mediaContext: context,
                            userId: userId,
                            libraryId: library.Id,
                            language: language,
                            take: UiLimits.MaximumCardsInCarousel,
                            skip: request.Page,
                            orderByExpression: m => m.CreatedAt,
                            direction: "desc"
                        )
                        .WithCancellation(cancellationToken: ct)
                )
                {
                    libraryMovies.Add(item: movie);
                }

                List<Tv> libraryShows = [];
                await foreach (
                    Tv tv in libraryRepository
                        .GetLibraryShows(
                            mediaContext: context,
                            userId: userId,
                            libraryId: library.Id,
                            language: language,
                            take: UiLimits.MaximumCardsInCarousel,
                            skip: request.Page,
                            orderByExpression: m => m.CreatedAt,
                            direction: "desc"
                        )
                        .WithCancellation(cancellationToken: ct)
                )
                {
                    libraryShows.Add(item: tv);
                }

                return (library, libraryMovies, libraryShows);
            })
            .ToArray();

        (Library library, List<Movie> movies, List<Tv> shows)[] libraryDataResults =
            await Task.WhenAll(tasks: libraryDataTasks);

        foreach (
            (
                Library library,
                List<Movie> libraryMovies,
                List<Tv> libraryShows
            ) in libraryDataResults.OrderByDescending(keySelector: r => r.library.Order)
        )
        {
            response.Data = response.Data.Prepend(
                element: new()
                {
                    Title = "Latest in " + library.Title,
                    MoreLink = new(uriString: $"/libraries/{library.Id}", uriKind: UriKind.Relative),
                    Items = libraryMovies
                        .Select(selector: movie => new GenreRowItemDto(movie: movie, country: country))
                        .Concat(second: libraryShows.Select(selector: tv => new GenreRowItemDto(tv: tv, country: country))),
                }
            );
        }

        return Ok(value: response);
    }

    [HttpGet(template: "home")]
    [ResponseCache(NoStore = true)]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> Home(CancellationToken ct = default)
    {
        ComponentResponse result = await _homeService.GetHomeData(
            userId: User.UserId(),
            language: Language(),
            country: Country()
        );

        return Ok(value: result);
    }

    [HttpPost(template: "home/card")]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> HomeCard(
        [FromBody] CardRequestDto request,
        CancellationToken ct = default
    )
    {
        ComponentResponse result = await _homeService.GetHomeCard(
            userId: User.UserId(),
            language: Language(),
            country: Country(),
            replaceId: request.ReplaceId
        );

        return Ok(value: result);
    }

    [HttpGet(template: "home/tv")]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> HomeTv(CancellationToken ct = default)
    {
        ComponentResponse result = await _homeService.GetHomeTvContent(
            userId: User.UserId(),
            language: Language(),
            country: Country()
        );

        return Ok(value: result);
    }

    [HttpPost(template: "home/continue")]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> HomeContinue(
        [FromBody] CardRequestDto request,
        CancellationToken ct = default
    )
    {
        ComponentResponse result = await _homeService.GetHomeContinueContent(
            userId: User.UserId(),
            language: Language(),
            country: Country(),
            replaceId: request.ReplaceId
        );

        return Ok(value: result);
    }

    [HttpHead]
    [Route(template: "trailer/{trailerId}")]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> HasTrailer(
        int id,
        string trailerId,
        CancellationToken ct = default
    )
    {
        if (!TrailerIdRegex().IsMatch(input: trailerId))
            return NotFoundResponse(detail: "Trailer not found");

        string infoJsonPath = _transcodeStorage.CombinePath(parent: trailerId, child: "info.json");

        if (await _transcodeStorage.ExistsAsync(path: infoJsonPath, ct: ct))
        {
            string text = await _transcodeStorage.ReadAllTextAsync(path: infoJsonPath, ct: ct);
            TrailerInfo? trailerInfo = text.FromJson<TrailerInfo>();
            if (trailerInfo is not null)
            {
                return Ok(
                    value: new StatusResponseDto<string> { Status = "ok", Message = "Trailer found" }
                );
            }
        }

        string arg =
            $"-f bestvideo+bestaudio -j https://youtube.com/watch?v={trailerId} --extractor-args \"youtube:player_client=default\" ";
        Shell.ExecResult result = await Shell.ExecAsync(executable: AppFiles.YtdlpPath, arguments: arg);

        if (!result.Success || string.IsNullOrEmpty(value: result.StandardOutput))
        {
            _logger.LogError(message: result.StandardError);
            return NotFoundResponse(detail: "Trailer not found");
        }

        if (!await _transcodeStorage.ExistsAsync(path: trailerId, ct: ct))
            await _transcodeStorage.CreateDirectoryAsync(path: trailerId, ct: ct);

        await _transcodeStorage.WriteAllTextAsync(path: infoJsonPath, contents: result.StandardOutput, ct: ct);

        return Ok(value: new StatusResponseDto<string> { Status = "ok", Message = "Trailer found" });
    }

    [HttpGet]
    [Route(template: "trailer/{trailerId}")]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> Trailer(
        int id,
        string trailerId,
        CancellationToken ct = default
    )
    {
        if (!TrailerIdRegex().IsMatch(input: trailerId))
            return NotFoundResponse(detail: "Trailer not found");

        string language = Language();

        if (!await _transcodeStorage.ExistsAsync(path: trailerId, ct: ct))
            await _transcodeStorage.CreateDirectoryAsync(path: trailerId, ct: ct);

        string infoJsonPath = _transcodeStorage.CombinePath(parent: trailerId, child: "info.json");
        string text = await _transcodeStorage.ReadAllTextAsync(path: infoJsonPath, ct: ct);
        TrailerInfo? trailerInfo = text.FromJson<TrailerInfo>();

        if (trailerInfo is null)
        {
            _logger.LogError(message: "Trailer info is null");
            return NotFoundResponse(detail: "Trailer not found");
        }

        string firstSegmentPath = _transcodeStorage.CombinePath(parent: trailerId, child: "video_00002.ts");
        if (await _transcodeStorage.ExistsAsync(path: firstSegmentPath, ct: ct))
        {
            return Ok(
                value: new VideoPlaylistResponseDto
                {
                    Id = 0,
                    Title = trailerInfo.Title,
                    Description = trailerInfo.Description,
                    Duration = trailerInfo.Duration.ToHis(),
                    Image = trailerInfo.Thumbnail?.ToString(),
                    File = $"/transcodes/{trailerId}/video.m3u8",
                    Origin = Info.DeviceId,
                    PlaylistId = trailerInfo.Id!,
                    Tracks = trailerInfo
                        .Subtitles.Where(predicate: t => t.Value.Any(predicate: s => s.Ext == "vtt"))
                        .Select(selector: t => new VideoTrack
                        {
                            Label = t.Value.First(predicate: s => s.Ext == "vtt").Name,
                            File = $"/transcodes/{trailerId}/-.{t.Key}.vtt",
                            Language = t.Key,
                            Kind = "subtitles",
                        })
                        .ToList(),
                    Sources =
                    [
                        new()
                        {
                            Src = $"/transcodes/{trailerId}/video.m3u8",
                            Type = "application/x-mpegURL",
                            Languages = [trailerInfo.Language.OrEmpty()],
                        },
                    ],
                }
            );
        }

        string trailerWorkDir = Path.Combine(path1: AppFiles.TranscodePath, path2: trailerId);

        _ = Task.Run(
            action: () =>
            {
                try
                {
                    string command = TrailerCommandBuilder.Build(
                        ytdlpPath: AppFiles.YtdlpPath,
                        ffmpegPath: AppFiles.FfmpegPath,
                        trailerId: trailerId,
                        language: language
                    );

                    if (Software.IsWindows)
                    {
                        _logger.LogDebug(message: "cmd -c \"{Command}\"", args: command);
                        Shell.ExecSync(
                            executable: "cmd",
                            arguments: $"/c \"{command}\"",
                            options: new() { WorkingDirectory = trailerWorkDir }
                        );
                    }
                    else
                    {
                        _logger.LogDebug(message: "/bin/bash -c \"{Command}\"", args: command);
                        Shell.ExecSync(
                            executable: "/bin/bash",
                            arguments: $"-c \"{command}\"",
                            options: new() { WorkingDirectory = trailerWorkDir }
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        message: "Trailer download failed for {TrailerId}: {Message}", args: [trailerId, ex.Message]
                    );
                }
            },
            cancellationToken: ct
        );

        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
            token: HttpContext.RequestAborted
        );
        timeoutCts.CancelAfter(delay: TimeSpan.FromSeconds(seconds: 30));
        while (!await _transcodeStorage.ExistsAsync(path: firstSegmentPath, ct: ct))
        {
            await Task.Delay(millisecondsDelay: 1000, cancellationToken: timeoutCts.Token);
        }

        return Ok(
            value: new VideoPlaylistResponseDto
            {
                Id = 0,
                Title = trailerInfo.Title,
                Description = trailerInfo.Description,
                Duration = trailerInfo.Duration.ToHis(),
                Image = trailerInfo.Thumbnail?.ToString(),
                File = $"/transcodes/{trailerId}/video.m3u8",
                Origin = Info.DeviceId,
                PlaylistId = trailerInfo.Id!,
                Tracks = trailerInfo
                    .Subtitles.Where(predicate: t => t.Value.Any(predicate: s => s.Ext == "vtt"))
                    .Select(selector: t => new VideoTrack
                    {
                        Label = t.Value.First(predicate: s => s.Ext == "vtt").Name,
                        File = $"/transcodes/{trailerId}/-.{t.Key}.vtt",
                        Language = t.Key,
                        Kind = "subtitles",
                    })
                    .ToList(),
                Sources =
                [
                    new()
                    {
                        Src = $"/transcodes/{trailerId}/video.m3u8",
                        Type = "application/x-mpegURL",
                        Languages = [trailerInfo.Language.OrEmpty()],
                    },
                ],
            }
        );
    }

    [HttpDelete]
    [Route(template: "trailer/{trailerId}")]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> RemoveTrailer(
        int id,
        string trailerId,
        CancellationToken ct = default
    )
    {
        if (!TrailerIdRegex().IsMatch(input: trailerId))
            return NotFoundResponse(detail: "Trailer not found");

        if (!await _transcodeStorage.ExistsAsync(path: trailerId, ct: ct))
            return Ok(value: new StatusResponseDto<string> { Status = "ok", Message = "Trailer removed" });

        string trailerAbsPath = Path.Combine(path1: AppFiles.TranscodePath, path2: trailerId);

        try
        {
            await _transcodeStorage.DeleteDirectoryAsync(path: trailerId, recursive: true, ct: ct);
            _logger.LogInformation(message: "Trailer folder deleted: {TrailerAbsPath}", args: trailerAbsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                message: "Failed to delete trailer folder {TrailerAbsPath}: {Message}", args: [trailerAbsPath, ex.Message]
            );
            return InternalServerErrorResponse(detail: "Failed to remove trailer");
        }

        return Ok(value: new StatusResponseDto<string> { Status = "ok", Message = "Trailer removed" });
    }
}
