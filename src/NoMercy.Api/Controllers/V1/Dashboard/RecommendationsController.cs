using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Api.Services;
using NoMercy.Helpers.Extensions;
using NoMercy.NmSystem.Information;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Media Recommendations")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/recommendations")]
public class RecommendationsController(
    RecommendationService recommendationService) : BaseController
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
