using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Database;
using NoMercy.Networking;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.People;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags(tags: "Media People")]
[ApiVersion(1.0)]
[Authorize]
public class PeopleController : BaseController
{
    [HttpGet]
    [Route("api/v{version:apiVersion}/person")] // match themoviedb.org API
    public async Task<IActionResult> Index([FromQuery] PageRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view people");

        string language = Language();

        await using MediaContext mediaContext = new();

        List<PeopleResponseItemDto> people = await PeopleResponseDto
            .GetPeople(userId, language, request.Take, request.Page);

        return GetPaginatedResponse(people, request);
    }

    [HttpGet]
    [Route("/api/v{version:apiVersion}/person/{id:int}")] // match themoviedb.org API
    public async Task<IActionResult> Show(int id)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view a person");

        string country = Country();

        TmdbPersonClient tmdbPersonClient = new(id);
        TmdbPersonAppends? personAppends = await tmdbPersonClient.WithAllAppends(true);

        if (personAppends is null)
            return NotFoundResponse("Person not found");

        return Ok(new PersonResponseDto
        {
            Data = new(personAppends, country)
        });
    }
}
