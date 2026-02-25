using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Api.Services;
using NoMercy.Database;
using NoMercy.Helpers.Extensions;
using NoMercy.NmSystem.Information;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Media Recommendations")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/recommendations")]
public class RecommendationsController(
    RecommendationService recommendationService,
    IDbContextFactory<MediaContext> contextFactory) : BaseController
{
    [HttpGet("movies")]
    public async Task<IActionResult> GetMovieRecommendations(
        [FromQuery] int take = 200,
        CancellationToken ct = default)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view recommendations");

        Guid userId = User.UserId();

        List<RecommendationDto> recommendations = await recommendationService
            .GetPersonalizedRecommendationsAsync(userId, Config.MovieMediaType, take, ct);

        ComponentEnvelope response = Component.Grid()
            .WithId("recommendations-movies")
            .WithTitle("Recommended Movies")
            .WithItems(recommendations.Select(rec => Component.Card()
                .WithData(new CardData(rec))
            ));

        return Ok(ComponentResponse.From(response));
    }

    [HttpGet("tv")]
    public async Task<IActionResult> GetTvRecommendations(
        [FromQuery] int take = 200,
        CancellationToken ct = default)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view recommendations");

        Guid userId = User.UserId();

        List<RecommendationDto> recommendations = await recommendationService
            .GetPersonalizedRecommendationsAsync(userId, Config.TvMediaType, take, ct);

        ComponentEnvelope response = Component.Grid()
            .WithId("recommendations-tv")
            .WithTitle("Recommended TV Shows")
            .WithItems(recommendations.Select(rec => Component.Card()
                .WithData(new CardData(rec))
            ));

        return Ok(ComponentResponse.From(response));
    }

    [HttpGet("anime")]
    public async Task<IActionResult> GetAnimeRecommendations(
        [FromQuery] int take = 200,
        CancellationToken ct = default)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view recommendations");

        Guid userId = User.UserId();

        List<RecommendationDto> recommendations = await recommendationService
            .GetPersonalizedRecommendationsAsync(userId, Config.AnimeMediaType, take, ct);

        ComponentEnvelope response = Component.Grid()
            .WithId("recommendations-anime")
            .WithTitle("Recommended Anime")
            .WithItems(recommendations.Select(rec => Component.Card()
                .WithData(new CardData(rec))
            ));

        return Ok(ComponentResponse.From(response));
    }

    [HttpGet("diagnostics")]
    public async Task<IActionResult> GetDiagnostics(CancellationToken ct = default)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view diagnostics");

        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);

        // Count by Library.Type (container level)
        int animeByLibraryType = await context.Tvs
            .CountAsync(t => t.Library.Type == Config.AnimeMediaType, ct);

        // Count by Tv.MediaType (per-show Kitsu.io detection)
        int animeByMediaType = await context.Tvs
            .CountAsync(t => t.MediaType == Config.AnimeMediaType, ct);

        int totalRecsWithTv = await context.Recommendations
            .CountAsync(r => r.TvFromId != null, ct);

        int animeRecsByMediaType = await context.Recommendations
            .CountAsync(r => r.TvFromId != null
                && context.Tvs.Any(t => t.Id == r.TvFromId && t.MediaType == Config.AnimeMediaType), ct);

        int totalSimWithTv = await context.Similar
            .CountAsync(s => s.TvFromId != null, ct);

        int animeSimByMediaType = await context.Similar
            .CountAsync(s => s.TvFromId != null
                && context.Tvs.Any(t => t.Id == s.TvFromId && t.MediaType == Config.AnimeMediaType), ct);

        List<string> libraryTypes = await context.Libraries
            .Select(l => l.Title + " (" + l.Type + ")")
            .ToListAsync(ct);

        // Sample: first 5 anime show IDs (by Tv.MediaType)
        List<int> sampleAnimeIds = await context.Tvs
            .Where(t => t.MediaType == Config.AnimeMediaType)
            .OrderBy(t => t.Id)
            .Take(5)
            .Select(t => t.Id)
            .ToListAsync(ct);

        int sampleRecsCount = sampleAnimeIds.Count > 0
            ? await context.Recommendations.CountAsync(r => sampleAnimeIds.Contains(r.TvFromId!.Value), ct)
            : 0;

        return Ok(new
        {
            libraries = libraryTypes,
            animeByLibraryType,
            animeByMediaType,
            totalRecsWithTv,
            animeRecsByMediaType,
            totalSimWithTv,
            animeSimByMediaType,
            sampleAnimeIds,
            sampleRecsCount
        });
    }

    [HttpGet("{type}/{id:int}")]
    public async Task<IActionResult> GetRecommendationDetail(
        string type, int id, CancellationToken ct = default)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view recommendations");

        if (type is not ("movie" or "tv" or "anime"))
            return BadRequest(new { message = "Type must be 'movie', 'tv', or 'anime'" });

        // Anime uses the same TMDB TV endpoint
        string resolvedType = type == "anime" ? "tv" : type;

        Guid userId = User.UserId();
        string country = Country();
        string language = Language();

        RecommendationDetailDto? detail = await recommendationService
            .GetRecommendationDetailAsync(userId, id, resolvedType, country, language, ct);

        if (detail is not null)
            detail.MediaType = type;

        if (detail is null)
            return NotFound(new { message = "Recommendation not found" });

        return Ok(new { data = detail });
    }
}
