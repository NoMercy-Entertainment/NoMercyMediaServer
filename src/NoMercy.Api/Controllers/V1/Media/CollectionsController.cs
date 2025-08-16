using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO;
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

        List<Collection> collections =
            await collectionRepository.GetCollectionsAsync(userId, language, request.Take, request.Page);

        if (request.Version != "lolomo")
        {
            IEnumerable<NmCardDto> concat = collections
                .Select(collection => new NmCardDto(collection, country));

            return Ok(new Render
            {
                Data =
                [
                    new ComponentBuilder<NmCardDto>()
                        .WithComponent("NMGrid")
                        .WithProps((props, _) => props
                            .WithItems(
                                concat.Select(item =>
                                    new ComponentBuilder<NmCardDto>()
                                        .WithComponent("NMCard")
                                        .WithProps((props, _) => props
                                            .WithData(item)
                                            .WithWatch())
                                        .Build())))
                        .Build()
                ]
            });
        }

        return Ok(new Render
        {
            Data = Letters.Select(genre => new ComponentBuilder<NmCarouselDto<NmCardDto>>()
                .WithComponent("NMCarousel")
                .WithProps((props, _) => props
                    .WithId(genre)
                    .WithTitle(genre)
                    .WithItems(
                        collections.Select(movie => new NmCardDto(movie, country))
                            .Where(item => genre == "#"
                                ? Numbers.Any(p => item.Title.StartsWith(p))
                                : item.Title.StartsWith(genre))
                            .Select(item => new ComponentBuilder<NmCardDto>()
                                .WithComponent("NMCard")
                                .WithProps((props, _) => props
                                    .WithData(item)
                                    .WithWatch())
                                .Build())))
                .Build())
        });
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
    [Route("{id:int}/watch")]
    public async Task<IActionResult> Watch(int id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view collections");

        string language = Language();

        Collection? collection = await collectionRepository.GetCollectionPlaylistAsync(userId, id, language);

        if (collection is null)
            return NotFoundResponse("Collection not found");

        return Ok(collection.CollectionMovies
            .Select((movie, index) => new VideoPlaylistResponseDto(movie.Movie, "collection", id, index + 1, collection)));
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
}