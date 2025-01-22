using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Networking;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags("Media Specials")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/specials")]
public class SpecialController(SpecialRepository specialRepository) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] PageRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view specials");

        string language = Language();

        List<Special> specials = await specialRepository.GetSpecialsAsync(userId, language, request.Take, request.Page);

        if (request.Version != "lolomo")
            return Ok(new SpecialsResponseDto
            {
                Data = specials
                    .Select(special => new SpecialsResponseItemDto(special))
            });

        string[] numbers = ["*", "#", "'", "\"", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0"];
        string[] letters =
        [
            "#", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N",
            "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"
        ];

        return Ok(new LoloMoResponseDto<SpecialsResponseItemDto>
        {
            Data = letters.Select(genre => new LoloMoRowDto<SpecialsResponseItemDto>
            {
                Title = genre,
                Id = genre,

                Items = specials.Where(special => genre == "#"
                        ? numbers.Any(p => special.Title.StartsWith(p))
                        : special.Title.StartsWith(genre))
                    .Select(special => new SpecialsResponseItemDto(special))
                    .OrderBy(libraryResponseDto => libraryResponseDto.TitleSort)
            })
        });
    }

    [HttpGet]
    [Route("{id:ulid}")]
    public async Task<IActionResult> Show(Ulid id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view a special");

        string language = Language();
        string country = Country();

        await using MediaContext mediaContext = new();
        Special? special = await SpecialResponseDto.GetSpecial(mediaContext, userId, id, language, country);

        if (special is null)
            return NotFoundResponse("Special not found");

        IEnumerable<int> movieIds = special.Items
            .Where(item => item.MovieId is not null)
            .Select(item => item.MovieId ?? 0);

        IEnumerable<int> tvIds = special.Items
            .Where(item => item.EpisodeId is not null)
            .Select(item => item.Episode)
            .Select(episode => episode!.Tv)
            .Select(tv => tv.Id);

        List<SpecialItemsDto> items = [];
        
        IAsyncEnumerable<Movie> specialMovies = SpecialResponseDto.GetSpecialMovies(mediaContext, userId, movieIds, language, country);
        await foreach (Movie movie in specialMovies)
            items.Add(new(movie));
        
        IAsyncEnumerable<Tv> specialTvs = SpecialResponseDto.GetSpecialTvs(mediaContext, userId, tvIds, language, country);
        await foreach (Tv tv in specialTvs)
            items.Add(new(tv));
        
        return Ok(new DataResponseDto<SpecialResponseItemDto>
        {
            Data = new(special, items)
        });
    }

    [HttpGet]
    [Route("{id:ulid}/available")]
    public async Task<IActionResult> Available(Ulid id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view a special");

        await using MediaContext mediaContext = new();
        Special? special = await SpecialResponseDto
            .GetSpecialAvailable(mediaContext, userId, id);

        bool hasFiles = special is not null && (
            special.Items
                .Select(movie => movie.Movie?.VideoFiles)
                .Any()
            || special.Items
                .Select(movie => movie.Episode?.VideoFiles)
                .Any()
        );

        if (!hasFiles)
            return NotFound(new AvailableResponseDto
            {
                Available = false
            });

        return Ok(new AvailableResponseDto
        {
            Available = true
        });
    }

    [HttpGet]
    [Route("{id:ulid}/watch")]
    public async Task<IActionResult> Watch(Ulid id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view a special");

        string language = Language();

        await using MediaContext mediaContext = new();

        Special? special = await SpecialResponseDto
            .GetSpecialPlaylist(mediaContext, userId, id, language);

        if (special is null)
            return NotFoundResponse("Special not found");

        PlaylistResponseDto[] items = special.Items
            .OrderBy(item => item.Order)
            .Select((item, index) => item.EpisodeId is not null
                ? new(item.Episode ?? new Episode(), index)
                : new PlaylistResponseDto(item.Movie ?? new Movie(), index)
            )
            .ToArray();

        if (items.Length == 0)
            return NotFoundResponse("Special not found");

        return Ok(items);
    }

    [HttpPost]
    [Route("{id:ulid}/like")]
    public async Task<IActionResult> Like(Ulid id, [FromBody] LikeRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to like a special");

        await using MediaContext mediaContext = new();
        Special? collection = await mediaContext.Specials
            .AsNoTracking()
            .Where(collection => collection.Id == id)
            .FirstOrDefaultAsync();

        if (collection is null)
            return NotFoundResponse("Special not found");

        if (request.Value)
        {
            await mediaContext.SpecialUser.Upsert(new(collection.Id, userId))
                .On(m => new { m.SpecialId, m.UserId })
                .WhenMatched(m => new()
                {
                    SpecialId = m.SpecialId,
                    UserId = m.UserId
                })
                .RunAsync();
        }
        else
        {
            SpecialUser? collectionUser = await mediaContext.SpecialUser
                .Where(collectionUser => collectionUser.SpecialId == collection.Id && collectionUser.UserId.Equals(userId))
                .FirstOrDefaultAsync();

            if (collectionUser is not null) mediaContext.SpecialUser.Remove(collectionUser);

            await mediaContext.SaveChangesAsync();
        }

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "{0} {1}",
            Args = new object[]
            {
                collection.Title,
                request.Value ? "liked" : "unliked"
            }
        });
    }
}
