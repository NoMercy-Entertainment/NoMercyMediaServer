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
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Api.Services;
using NoMercy.Data.Repositories;
using NoMercy.Helpers.Extensions;
using NoMercy.Authorization;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Information;

namespace NoMercy.Api.Controllers.V1.Dashboard.Media;

[ApiController]
[Tags("Media Recommendations")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/recommendations")]
public class RecommendationsController(
    RecommendationService recommendationService,
    IRecommendationRepository recommendationRepository
) : BaseController
{
    [HttpGet("movies")]
    public async Task<IActionResult> GetMovieRecommendations(
        [FromQuery] int take = 200,
        CancellationToken ct = default
    )
    {
        if (!AuthPolicy.IsAllowed(User))
            return UnauthorizedResponse("You do not have permission to view recommendations");

        Guid userId = User.UserId();

        List<RecommendationDto> recommendations =
            await recommendationService.GetPersonalizedRecommendationsAsync(
                userId,
                MediaTypes.MovieMediaType,
                take,
                ct
            );

        ComponentEnvelope response = Component
            .Grid()
            .WithId("recommendations-movies")
            .WithTitle("Recommended Movies")
            .WithItems(recommendations.Select(rec => Component.Card().WithData(new(rec))));

        return Ok(ComponentResponse.From(response));
    }

    [HttpGet("tv")]
    public async Task<IActionResult> GetTvRecommendations(
        [FromQuery] int take = 200,
        CancellationToken ct = default
    )
    {
        if (!AuthPolicy.IsAllowed(User))
            return UnauthorizedResponse("You do not have permission to view recommendations");

        Guid userId = User.UserId();

        List<RecommendationDto> recommendations =
            await recommendationService.GetPersonalizedRecommendationsAsync(
                userId,
                MediaTypes.TvMediaType,
                take,
                ct
            );

        ComponentEnvelope response = Component
            .Grid()
            .WithId("recommendations-tv")
            .WithTitle("Recommended TV Shows")
            .WithItems(recommendations.Select(rec => Component.Card().WithData(new(rec))));

        return Ok(ComponentResponse.From(response));
    }

    [HttpGet("anime")]
    public async Task<IActionResult> GetAnimeRecommendations(
        [FromQuery] int take = 200,
        CancellationToken ct = default
    )
    {
        if (!AuthPolicy.IsAllowed(User))
            return UnauthorizedResponse("You do not have permission to view recommendations");

        Guid userId = User.UserId();

        List<RecommendationDto> recommendations =
            await recommendationService.GetPersonalizedRecommendationsAsync(
                userId,
                MediaTypes.AnimeMediaType,
                take,
                ct
            );

        ComponentEnvelope response = Component
            .Grid()
            .WithId("recommendations-anime")
            .WithTitle("Recommended Anime")
            .WithItems(recommendations.Select(rec => Component.Card().WithData(new(rec))));

        return Ok(ComponentResponse.From(response));
    }

    [HttpGet("diagnostics")]
    public async Task<IActionResult> GetDiagnostics(CancellationToken ct = default)
    {
        if (!AuthPolicy.IsModerator(User))
            return UnauthorizedResponse("You do not have permission to view diagnostics");

        RecommendationDiagnosticsDto diagnostics =
            await recommendationRepository.GetDiagnosticsAsync(ct);

        return Ok(
            new
            {
                libraries = diagnostics.Libraries,
                animeByLibraryType = diagnostics.AnimeByLibraryType,
                animeByMediaType = diagnostics.AnimeByMediaType,
                totalRecsWithTv = diagnostics.TotalRecsWithTv,
                animeRecsByMediaType = diagnostics.AnimeRecsByMediaType,
                totalSimWithTv = diagnostics.TotalSimWithTv,
                animeSimByMediaType = diagnostics.AnimeSimByMediaType,
                sampleAnimeIds = diagnostics.SampleAnimeIds,
                sampleRecsCount = diagnostics.SampleRecsCount,
            }
        );
    }

    [HttpGet("{type}/{id:int}")]
    public async Task<IActionResult> GetRecommendationDetail(
        string type,
        int id,
        CancellationToken ct = default
    )
    {
        if (!AuthPolicy.IsAllowed(User))
            return UnauthorizedResponse("You do not have permission to view recommendations");

        if (type is not ("movie" or "tv" or "anime"))
            return BadRequestResponse("Type must be 'movie', 'tv', or 'anime'");

        // Anime uses the same TMDB TV endpoint
        string resolvedType = type == "anime" ? "tv" : type;

        Guid userId = User.UserId();
        string country = Country();
        string language = Language();

        RecommendationDetailDto? detail = await recommendationService.GetRecommendationDetailAsync(
            userId,
            id,
            resolvedType,
            country,
            language,
            ct
        );

        if (detail is not null)
            detail.MediaType = type;

        if (detail is null)
            return NotFoundResponse("Recommendation not found");

        return Ok(new { data = detail });
    }
}
