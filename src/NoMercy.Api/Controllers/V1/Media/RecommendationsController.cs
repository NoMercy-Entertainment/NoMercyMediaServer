using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.Services;
using NoMercy.Helpers.Extensions;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags("Media Recommendations")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/recommendations")]
public class RecommendationsController(
    RecommendationService recommendationService) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetRecommendations(
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view recommendations");

        Guid userId = User.UserId();

        List<ScoredRecommendationDto> recommendations = await recommendationService
            .GetPersonalizedRecommendationsAsync(userId, take, ct);

        return Ok(new { data = recommendations });
    }
}
