using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.DTOs.Media;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Helpers.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.People;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags(tags: "Media People")]
[ApiVersion(1.0)]
[Authorize]
public class PeopleController(MediaContext mediaContext, IPeopleRepository peopleRepository)
    : BaseController
{
    [HttpGet]
    [Route("api/v{version:apiVersion}/person")] // match themoviedb.org API
    [ResponseCache(Duration = 300, VaryByQueryKeys = ["take", "page"])]
    public async Task<IActionResult> Index([FromQuery] PageRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view people");

        string language = Language();

        List<PeopleResponseItemDto> people = (
            await peopleRepository.GetPeopleAsync(userId, language, request.Take, request.Page)
        )
            .Select(person => new PeopleResponseItemDto(person))
            .ToList();

        return GetPaginatedResponse(people, request);
    }

    [HttpGet]
    [Route("/api/v{version:apiVersion}/person/{id:int}")] // match themoviedb.org API
    [ResponseCache(Duration = 300)]
    public async Task<IActionResult> Show(int id)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view a person");

        string country = Country();

        TmdbPersonClient tmdbPersonClient = new(id);
        TmdbPersonAppends? personAppends = await tmdbPersonClient.WithAllAppends(true);

        if (personAppends is null)
            return NotFoundResponse("Person not found");

        if (personAppends.Adult && !Config.ShowAdultContent)
            return UnauthorizedResponse(
                "Person is adult which is not allowed by the server configuration"
            );

        return Ok(new PersonResponseDto { Data = new(personAppends, country, mediaContext) });
    }
}
