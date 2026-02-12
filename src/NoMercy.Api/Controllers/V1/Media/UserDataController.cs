using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.Controllers.V1.Music;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/userData")]
public class UserDataController(HomeRepository homeRepository, MediaContext mediaContext) : BaseController
{
    [HttpGet]
    public IActionResult Index()
    {
        // Guid userId = User.UserId();
        if (!User.IsAllowed())
            return Unauthorized(new StatusResponseDto<string>
            {
                Status = "error",
                Message = "You do not have permission to view user data"
            });

        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }

    [HttpGet]
    [Route("continue")]
    [ResponseCache(NoStore = true)]
    public async Task<IActionResult> ContinueWatching()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return Unauthorized(new StatusResponseDto<string>
            {
                Status = "error",
                Message = "You do not have permission to view continue watching"
            });

        string language = Language();
        string country = Country();

        HashSet<UserData> continueWatching = await homeRepository
            .GetContinueWatchingAsync(mediaContext, userId, language, country);

        return Ok(new CarouselResponseDto<NmCardDto>
        {
            Data = continueWatching
                .Select(item => new NmCardDto(item, country))
                .DistinctBy(item => item.Link)
        });
    }

    [HttpDelete]
    [Route("continue")]
    public async Task<IActionResult> RemoveContinue(FavoriteRequest body)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return Unauthorized(new StatusResponseDto<string>
            {
                Status = "error",
                Message = "You do not have permission to remove continue watching"
            });


        List<UserData>? userData = body.Type switch
        {
            Config.MovieMediaType => await mediaContext.UserData
                .AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.MovieId == int.Parse(body.Id))
                .ToListAsync(),
            Config.TvMediaType => await mediaContext.UserData
                .AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.TvId == int.Parse(body.Id))
                .ToListAsync(),
            Config.SpecialMediaType => await mediaContext.UserData
                .AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.SpecialId == Ulid.Parse(body.Id))
                .ToListAsync(),
            Config.CollectionMediaType => await mediaContext.UserData
                .AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.CollectionId == int.Parse(body.Id))
                .ToListAsync(),
            _ => null
        };

        if (userData == null || userData.Count == 0)
            return NotFound(new StatusResponseDto<string>
            {
                Status = "error",
                Message = "Item not found"
            });

        Logger.Socket(userData);

        mediaContext.UserData.RemoveRange(userData);
        await mediaContext.SaveChangesAsync();

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Item removed"
        });
    }

    [HttpGet]
    [Route("watched")]
    public async Task<IActionResult> Watched([FromBody] FavoriteRequest body)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return Unauthorized(new StatusResponseDto<string>
            {
                Status = "error",
                Message = "You do not have permission to view watched"
            });


        UserData? userData = body.Type switch
        {
            Config.MovieMediaType => await mediaContext.UserData
                .AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.MovieId == int.Parse(body.Id))
                .FirstOrDefaultAsync(),
            Config.TvMediaType => await mediaContext.UserData
                .AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.TvId == int.Parse(body.Id))
                .FirstOrDefaultAsync(),
            Config.SpecialMediaType => await mediaContext.UserData
                .AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.SpecialId == Ulid.Parse(body.Id))
                .FirstOrDefaultAsync(),
            Config.CollectionMediaType => await mediaContext.UserData
                .AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.CollectionId == int.Parse(body.Id))
                .FirstOrDefaultAsync(),
            _ => null
        };

        if (userData == null)
            return NotFound(new StatusResponseDto<string>
            {
                Status = "error",
                Message = "Item not found"
            });

        await mediaContext.SaveChangesAsync();

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Item marked as watched"
        });
    }

    [HttpGet]
    [Route("favorites")]
    public async Task<IActionResult> Favorites([FromBody] FavoriteRequest body)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return Unauthorized(new StatusResponseDto<string>
            {
                Status = "error",
                Message = "You do not have permission to view favorites"
            });


        UserData? userData = body.Type switch
        {
            Config.MovieMediaType => await mediaContext.UserData
                .AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.MovieId == int.Parse(body.Id))
                .FirstOrDefaultAsync(),
            Config.TvMediaType => await mediaContext.UserData
                .AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.TvId == int.Parse(body.Id))
                .FirstOrDefaultAsync(),
            Config.SpecialMediaType => await mediaContext.UserData
                .AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.SpecialId == Ulid.Parse(body.Id))
                .FirstOrDefaultAsync(),
            Config.CollectionMediaType => await mediaContext.UserData
                .AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.CollectionId == int.Parse(body.Id))
                .FirstOrDefaultAsync(),
            _ => null
        };

        if (userData is null)
            return NotFound(new StatusResponseDto<string>
            {
                Status = "error",
                Message = "Item not found"
            });

        await mediaContext.SaveChangesAsync();

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Item marked as favorite"
        });
    }
}