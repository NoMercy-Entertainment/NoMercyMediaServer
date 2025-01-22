using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models;
using NoMercy.Networking;
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

        List<Collection> collections =
            await collectionRepository.GetCollectionsAsync(userId, language, request.Take, request.Page);

        if (request.Version != "lolomo")
        {
            IEnumerable<CollectionsResponseItemDto> concat = collections
                .Select(collection => new CollectionsResponseItemDto(collection));

            return GetPaginatedResponse(concat, request);
        }

        string[] numbers = ["*", "#", "'", "\"", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0"];
        string[] letters =
        [
            "#", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N",
            "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"
        ];

        return Ok(new LoloMoResponseDto<LibraryResponseItemDto>
        {
            Data = letters.Select(genre => new LoloMoRowDto<LibraryResponseItemDto>
            {
                Title = genre,
                Id = genre,

                Items = collections.Where(collection => genre == "#"
                        ? numbers.Any(p => collection.Title.StartsWith(p))
                        : collection.Title.StartsWith(genre))
                    .Select(collection => new LibraryResponseItemDto(collection))
            })
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
                Data = new(collection, country)
            });

        TmdbCollectionClient tmdbCollectionsClient = new(id);
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
        string country = Country();

        Collection? collection = await collectionRepository.GetWatchCollectionAsync(userId, id, language, country);

        if (collection is null)
            return NotFoundResponse("Collection not found");

        return Ok(collection.CollectionMovies
            .Select((movie, index) => new PlaylistResponseDto(movie.Movie, index + 1, collection)));
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
