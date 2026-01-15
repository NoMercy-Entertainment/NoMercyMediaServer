using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO.Components;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Collections;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags(tags: "Media Collections")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/collection")] // match themoviedb.org API
public class CollectionsController(CollectionRepository collectionRepository) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Collections([FromQuery] PageRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view collections");

        string language = Language();
        string country = Country();

        // Use optimized query that projects only needed data
        List<CollectionListDto> collectionDtos =
            await collectionRepository.GetCollectionsListAsync(userId, language, country, request.Take, request.Page);

        if (request.Version != "lolomo")
        {
            List<CardData> cardItems = collectionDtos
                .Select(dto => new CardData(dto))
                .ToList();

            ComponentEnvelope response = Component.Grid()
                .WithItems(cardItems.Select(item => Component.Card()
                    .WithData(item)
                    ))
                ;

            return Ok(ComponentResponse.From(response));
        }

        List<ComponentEnvelope> carousels = Letters
            .Select((letter, index) =>
            {
                List<CardData> letterItems = collectionDtos
                    .Where(dto => letter == "#"
                        ? Numbers.Any(p => dto.Title.StartsWith(p))
                        : dto.Title.StartsWith(letter))
                    .Select(dto => new CardData(dto))
                    .OrderBy(item => item.TitleSort)
                    .ToList();

                return Component.Carousel()
                    .WithId(letter)
                    .WithTitle(letter)
                    .WithNavigation(
                        index == 0 ? null : Letters[index - 1],
                        index == Letters.Length - 1 ? null : Letters[index + 1])
                    .WithItems(letterItems.Select(item => Component.Card()
                        .WithData(item)))
                    .Build();
            })
            .ToList();

        ComponentEnvelope containerResponse = Component.Container()
            .WithItems(carousels);

        return Ok(containerResponse);
    }

    [HttpGet]
    [Route("{id:int}")]
    public async Task<IActionResult> Collection(int id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view collections");

        string language = Language();
        string country = Country();

        Collection? collection = await collectionRepository.GetCollectionAsync(userId, id, language, country);

        if (collection is not null && collection.CollectionMovies.Count > 0 && collection.Images.Count > 0)
            return Ok(new CollectionResponseDto
            {
                Data = new(collection)
            });

        TmdbCollectionClient tmdbCollectionsClient = new(id, language: language);
        TmdbCollectionAppends? collectionAppends = await tmdbCollectionsClient.WithAllAppends(true);

        if (collectionAppends is null)
            return NotFound(new CollectionResponseDto
            {
                Data = null
            });

        // Library? library = await mediaContext.Libraries
        //     .Where(predicate: f => f.Type == "movie")
        //     .Include(navigationPropertyPath: l => l.FolderLibraries)
        //     .ThenInclude(navigationPropertyPath: fl => fl.Folder)
        //     .FirstOrDefaultAsync();

        // TmdbCollectionJob tmdbJob = new(collectionAppends.Id, library);
        // jobDispatcher.Dispatch(tmdbJob, "queue", 10);

        return Ok(new CollectionResponseDto
        {
            Data = new(collectionAppends)
        });
    }

    [HttpGet]
    [Route("{id:int}/available")]
    public async Task<IActionResult> Available(int id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view collections");

        Collection? collection = await collectionRepository.GetAvailableCollectionAsync(userId, id);

        bool available = collection is not null && collection.CollectionMovies
            .Select(movie => movie.Movie.VideoFiles)
            .Any();

        if (!available)
            return NotFound(new StatusResponseDto<AvailableResponseDto>
            {
                Data = new()
                {
                    Available = false
                },
                Status = "error",
                Message = "Collection not found"
            });

        return Ok(new StatusResponseDto<AvailableResponseDto>
        {
            Data = new()
            {
                Available = true
            },
            Status = "ok",
            Message = "Collection is available"
        });
    }

    [HttpGet]
    [Route("{id:int}/watch")]
    public async Task<IActionResult> Watch(int id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view collections");

        string language = Language();
        string country = Country();

        Collection? collection = await collectionRepository.GetCollectionPlaylistAsync(userId, id, language, country);

        if (collection is null)
            return NotFoundResponse("Collection not found");

        return Ok(collection.CollectionMovies
            .Select((movie, index) => new VideoPlaylistResponseDto(movie.Movie, "collection", id, country, index + 1, collection)));
    }

    [HttpPost]
    [Route("{id:int}/like")]
    public async Task<IActionResult> Like(int id, [FromBody] LikeRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to like collections");

        bool success = await collectionRepository.LikeCollectionAsync(id, userId, request.Value);

        if (!success)
            return UnprocessableEntityResponse("Collection not found");

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "{1}",
            Args = new object[]
            {
                request.Value ? "liked" : "unliked"
            }
        });
    }

    [HttpPost]
    [Route("{id:int}/watch-list")]
    public async Task<IActionResult> AddToWatchList(int id, [FromBody] WatchListRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to manage watch list");

        bool success = await collectionRepository.AddToWatchListAsync(id, userId, request.Add);

        if (!success)
            return UnprocessableEntityResponse("Collection not found");

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = request.Add ? "Collection added to watch list" : "Collection removed from watch list"
        });
    }
}