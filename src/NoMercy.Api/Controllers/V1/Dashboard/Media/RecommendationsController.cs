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
using NoMercy.Authorization;
using NoMercy.Data.Repositories;
using NoMercy.NmSystem.Domain;

namespace NoMercy.Api.Controllers.V1.Dashboard.Media;

[ApiController]
[Tags(tags: "Media Recommendations")]
[ApiVersion(version: 1.0)]
[Authorize]
[Route(template: "api/v{version:apiVersion}/dashboard/recommendations")]
public class RecommendationsController(
    RecommendationService recommendationService,
    IRecommendationRepository recommendationRepository
) : BaseController
{
    [HttpGet(template: "movies")]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> GetMovieRecommendations(
        [FromQuery] int take = 200,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();

        List<RecommendationDto> recommendations =
            await recommendationService.GetPersonalizedRecommendationsAsync(
                userId: userId,
                mediaTypeFilter: MediaTypes.MovieMediaType,
                take: take,
                ct: ct
            );

        ComponentEnvelope response = Component
            .Grid()
            .WithId(id: "recommendations-movies")
            .WithTitle(title: "Recommended Movies")
            .WithItems(builders: recommendations.Select(selector: rec => Component.Card().WithData(data: new(rec: rec))));

        return Ok(value: ComponentResponse.From(component: response));
    }

    [HttpGet(template: "tv")]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> GetTvRecommendations(
        [FromQuery] int take = 200,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();

        List<RecommendationDto> recommendations =
            await recommendationService.GetPersonalizedRecommendationsAsync(
                userId: userId,
                mediaTypeFilter: MediaTypes.TvMediaType,
                take: take,
                ct: ct
            );

        ComponentEnvelope response = Component
            .Grid()
            .WithId(id: "recommendations-tv")
            .WithTitle(title: "Recommended TV Shows")
            .WithItems(builders: recommendations.Select(selector: rec => Component.Card().WithData(data: new(rec: rec))));

        return Ok(value: ComponentResponse.From(component: response));
    }

    [HttpGet(template: "anime")]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> GetAnimeRecommendations(
        [FromQuery] int take = 200,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();

        List<RecommendationDto> recommendations =
            await recommendationService.GetPersonalizedRecommendationsAsync(
                userId: userId,
                mediaTypeFilter: MediaTypes.AnimeMediaType,
                take: take,
                ct: ct
            );

        ComponentEnvelope response = Component
            .Grid()
            .WithId(id: "recommendations-anime")
            .WithTitle(title: "Recommended Anime")
            .WithItems(builders: recommendations.Select(selector: rec => Component.Card().WithData(data: new(rec: rec))));

        return Ok(value: ComponentResponse.From(component: response));
    }

    [HttpGet(template: "diagnostics")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> GetDiagnostics(CancellationToken ct = default)
    {
        RecommendationDiagnosticsDto diagnostics =
            await recommendationRepository.GetDiagnosticsAsync(ct: ct);

        return Ok(
            value: new
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

    [HttpGet(template: "{type}/{id:int}")]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> GetRecommendationDetail(
        string type,
        int id,
        CancellationToken ct = default
    )
    {
        if (type is not ("movie" or "tv" or "anime"))
            return BadRequestResponse(detail: "Type must be 'movie', 'tv', or 'anime'");

        // Anime uses the same TMDB TV endpoint
        string resolvedType = type == "anime" ? "tv" : type;

        Guid userId = User.UserId();
        string country = Country();
        string language = Language();

        RecommendationDetailDto? detail = await recommendationService.GetRecommendationDetailAsync(
            userId: userId,
            mediaId: id,
            mediaType: resolvedType,
            country: country,
            language: language,
            ct: ct
        );

        if (detail is not null)
            detail.MediaType = type;

        if (detail is null)
            return NotFoundResponse(detail: "Recommendation not found");

        return Ok(value: new { data = detail });
    }
}
