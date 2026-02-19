using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Helpers.Extensions;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags("Media Specials")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/specials")]
public class SpecialController(SpecialRepository specialRepository, MediaContext context, IDbContextFactory<MediaContext> contextFactory) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] PageRequestDto request, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view specials");

        string language = Language();
        string country = Country();

        List<SpecialCardDto> specials = await specialRepository.GetSpecialCardsAsync(userId, language, request.Take, request.Page, ct);

        if (request.Version != "lolomo")
        {
            List<CardData> cardItems = specials
                .Select(special => new CardData(special, country))
                .ToList();

            ComponentEnvelope response = Component.Grid()
                .WithItems(cardItems.Select(item => Component.Card()
                    .WithData(item)
                    ))
                ;

            return Ok(ComponentResponse.From(response));
        }

        List<ComponentEnvelope> carousels = Letters
            .Select(letter =>
            {
                List<CardData> letterItems = specials
                    .Select(special => new CardData(special, country))
                    .Where(item => letter == "#"
                        ? Numbers.Any(p => item.Title.StartsWith(p))
                        : item.Title.StartsWith(letter))
                    .ToList();

                return Component.Carousel()
                    .WithId(letter)
                    .WithTitle(letter)
                    .WithItems(letterItems.Select(item => Component.Card()
                        .WithData(item)
                        )).Build();
            })
            .ToList();

        ComponentEnvelope containerResponse = Component.Container()
            .WithItems(carousels);

        return Ok(containerResponse);
    }

    [HttpGet]
    [Route("{id:ulid}")]
    public async Task<IActionResult> Show(Ulid id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view a special");

        string country = Country();

        SpecialDetailDto? detail = await specialRepository.GetSpecialDetailAsync(userId, id, ct);

        if (detail is null)
            return NotFoundResponse("Special not found");

        IEnumerable<int> movieIds = detail.Items
            .Where(item => item.MovieId is not null)
            .Select(item => item.MovieId ?? 0);

        IEnumerable<int> tvIds = detail.Items
            .Where(item => item.EpisodeId is not null)
            .Select(item => item.TvId)
            .Distinct();

        // Fetch movies and TVs in parallel using separate DbContexts
        Task<List<SpecialItemsDto>> moviesTask = Task.Run(async () =>
        {
            await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
            List<SpecialMovieProjection> projections =
                await SpecialRepository.GetSpecialMovieProjectionsAsync(mediaContext, userId, movieIds, country, ct);
            return projections.Select(p => new SpecialItemsDto(p)).ToList();
        });

        Task<List<SpecialItemsDto>> tvsTask = Task.Run(async () =>
        {
            await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
            List<SpecialTvProjection> projections =
                await SpecialRepository.GetSpecialTvProjectionsAsync(mediaContext, userId, tvIds, country, ct);
            return projections.Select(p => new SpecialItemsDto(p)).ToList();
        });

        await Task.WhenAll(moviesTask, tvsTask);

        List<SpecialItemsDto> items = [..moviesTask.Result, ..tvsTask.Result];

        return Ok(new DataResponseDto<SpecialResponseItemDto>
        {
            Data = new(detail, items)
        });
    }

    [HttpGet]
    [Route("{id:ulid}/available")]
    public async Task<IActionResult> Available(Ulid id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view a special");

        Special? special = await SpecialResponseDto
            .GetSpecialAvailable(context, userId, id);

        bool hasFiles = special is not null && (
            special.Items
                .Select(movie => movie.Movie?.VideoFiles)
                .Any()
            || special.Items
                .Select(movie => movie.Episode?.VideoFiles)
                .Any()
        );

        if (!hasFiles)
            return NotFound(new StatusResponseDto<AvailableResponseDto>
            {
                Data = new()
                {
                    Available = false
                },
                Status = "error",
                Message = "Special not found"
            });

        return Ok(new StatusResponseDto<AvailableResponseDto>
        {
            Data = new()
            {
                Available = true
            },
            Status = "ok",
            Message = "Special is available"
        });
    }

    [HttpGet]
    [Route("{id:ulid}/watch")]
    public async Task<IActionResult> Watch(Ulid id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view a special");

        string language = Language();
        string country = Country();

        Special? special = await specialRepository
            .GetSpecialPlaylistAsync(userId, id, language, country, ct);

        if (special is null)
            return NotFoundResponse("Special not found");

        VideoPlaylistResponseDto[] items = special.Items
            .OrderBy(item => item.Order)
            .Select((item, index) => item.EpisodeId is not null
                ? new(item.Episode ?? new Episode(), "specials", id, country, index)
                : new VideoPlaylistResponseDto(item.Movie ?? new Movie(), "specials", id, country, index))
            .ToArray();

        if (items.Length == 0)
            return NotFoundResponse("Special not found");

        return Ok(items);
    }

    [HttpPost]
    [Route("{id:ulid}/like")]
    public async Task<IActionResult> Like(Ulid id, [FromBody] LikeRequestDto request, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to like a special");

        Special? collection = await context.Specials
            .AsNoTracking()
            .Where(collection => collection.Id == id)
            .FirstOrDefaultAsync(ct);

        if (collection is null)
            return NotFoundResponse("Special not found");

        if (request.Value)
        {
            await context.SpecialUser.Upsert(new(collection.Id, userId))
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
            SpecialUser? collectionUser = await context.SpecialUser
                .Where(collectionUser =>
                    collectionUser.SpecialId == collection.Id && collectionUser.UserId.Equals(userId))
                .FirstOrDefaultAsync(ct);

            if (collectionUser is not null) context.SpecialUser.Remove(collectionUser);

            await context.SaveChangesAsync(ct);
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

    [HttpPost]
    [Route("{id:ulid}/watch-list")]
    public async Task<IActionResult> AddToWatchList(Ulid id, [FromBody] WatchListRequestDto request, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to manage watch list");

        bool success = await specialRepository.AddToWatchListAsync(id, userId, request.Add, ct);

        if (!success)
            return UnprocessableEntityResponse("Special not found");

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = request.Add ? "Special added to watch list" : "Special removed from watch list"
        });
    }
}
