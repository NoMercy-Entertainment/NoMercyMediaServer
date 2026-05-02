using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Controllers.V1.Music;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Helpers.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/userData")]
public class UserDataController(
    HomeRepository homeRepository,
    MediaContext mediaContext,
    IEventBus eventBus
) : BaseController
{
    [HttpGet]
    public IActionResult Index()
    {
        // Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthenticatedResponse("You do not have permission to view user data");

        return Ok(new PlaceholderResponse { Data = [] });
    }

    [HttpGet]
    [Route("continue")]
    [ResponseCache(NoStore = true)]
    public async Task<IActionResult> ContinueWatching()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthenticatedResponse("You do not have permission to view continue watching");

        string language = Language();
        string country = Country();

        HashSet<UserData> continueWatching = await homeRepository.GetContinueWatchingAsync(
            mediaContext,
            userId,
            language,
            country
        );

        return Ok(
            new CarouselResponseDto<NmCardDto>
            {
                Data = continueWatching
                    .Select(item => new NmCardDto(item, country))
                    .DistinctBy(item => item.Link),
            }
        );
    }

    [HttpDelete]
    [Route("continue")]
    public async Task<IActionResult> RemoveContinue(FavoriteRequest body)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthenticatedResponse(
                "You do not have permission to remove continue watching"
            );

        if (!TryParseFavoriteIds(body, out int? intId, out Ulid? ulidId))
            return BadRequestResponse("Invalid id for the requested type");

        List<UserData>? userData = body.Type switch
        {
            Config.MovieMediaType => await mediaContext
                .UserData.AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.MovieId == intId)
                .ToListAsync(),
            Config.TvMediaType => await mediaContext
                .UserData.AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.TvId == intId)
                .ToListAsync(),
            Config.SpecialMediaType => await mediaContext
                .UserData.AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.SpecialId == ulidId)
                .ToListAsync(),
            Config.CollectionMediaType => await mediaContext
                .UserData.AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.CollectionId == intId)
                .ToListAsync(),
            _ => null,
        };

        if (userData == null || userData.Count == 0)
            return NotFoundResponse("Item not found");

        Logger.Socket(userData);

        mediaContext.UserData.RemoveRange(userData);
        await mediaContext.SaveChangesAsync();

        await eventBus.PublishAsync(new LibraryRefreshEvent { QueryKey = ["continue-watching"] });

        return Ok(new StatusResponseDto<string> { Status = "ok", Message = "Item removed" });
    }

    [HttpGet]
    [Route("watched")]
    public async Task<IActionResult> Watched([FromQuery] FavoriteRequest body)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthenticatedResponse("You do not have permission to view watched");

        if (!TryParseFavoriteIds(body, out int? intId, out Ulid? ulidId))
            return BadRequestResponse("Invalid id for the requested type");

        UserData? userData = body.Type switch
        {
            Config.MovieMediaType => await mediaContext
                .UserData.AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.MovieId == intId)
                .FirstOrDefaultAsync(),
            Config.TvMediaType => await mediaContext
                .UserData.AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.TvId == intId)
                .FirstOrDefaultAsync(),
            Config.SpecialMediaType => await mediaContext
                .UserData.AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.SpecialId == ulidId)
                .FirstOrDefaultAsync(),
            Config.CollectionMediaType => await mediaContext
                .UserData.AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.CollectionId == intId)
                .FirstOrDefaultAsync(),
            _ => null,
        };

        if (userData == null)
            return NotFoundResponse("Item not found");

        return Ok(
            new StatusResponseDto<string> { Status = "ok", Message = "Item marked as watched" }
        );
    }

    [HttpGet]
    [Route("favorites")]
    public async Task<IActionResult> Favorites([FromQuery] FavoriteRequest body)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthenticatedResponse("You do not have permission to view favorites");

        if (!TryParseFavoriteIds(body, out int? intId, out Ulid? ulidId))
            return BadRequestResponse("Invalid id for the requested type");

        UserData? userData = body.Type switch
        {
            Config.MovieMediaType => await mediaContext
                .UserData.AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.MovieId == intId)
                .FirstOrDefaultAsync(),
            Config.TvMediaType => await mediaContext
                .UserData.AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.TvId == intId)
                .FirstOrDefaultAsync(),
            Config.SpecialMediaType => await mediaContext
                .UserData.AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.SpecialId == ulidId)
                .FirstOrDefaultAsync(),
            Config.CollectionMediaType => await mediaContext
                .UserData.AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.CollectionId == intId)
                .FirstOrDefaultAsync(),
            _ => null,
        };

        if (userData is null)
            return NotFoundResponse("Item not found");

        return Ok(
            new StatusResponseDto<string> { Status = "ok", Message = "Item marked as favorite" }
        );
    }

    /// <summary>
    /// Parses <see cref="FavoriteRequest.Id"/> as either an int (movie / tv /
    /// collection) or a Ulid (special) depending on <see cref="FavoriteRequest.Type"/>.
    /// Returns false when the id is malformed for the requested type so the
    /// caller can short-circuit with a 400 instead of throwing FormatException
    /// from inside an EF query.
    /// </summary>
    private static bool TryParseFavoriteIds(FavoriteRequest body, out int? intId, out Ulid? ulidId)
    {
        intId = null;
        ulidId = null;
        if (string.IsNullOrEmpty(body.Id))
            return false;

        switch (body.Type)
        {
            case Config.MovieMediaType:
            case Config.TvMediaType:
            case Config.CollectionMediaType:
                if (!int.TryParse(body.Id, out int parsedInt))
                    return false;
                intId = parsedInt;
                return true;
            case Config.SpecialMediaType:
                if (!Ulid.TryParse(body.Id, out Ulid parsedUlid))
                    return false;
                ulidId = parsedUlid;
                return true;
            default:
                return false;
        }
    }
}
