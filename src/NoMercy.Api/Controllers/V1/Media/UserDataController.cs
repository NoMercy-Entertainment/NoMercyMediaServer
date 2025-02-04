using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Api.Controllers.V1.Music;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Networking;
using NoMercy.NmSystem;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/userData")]
public class UserDataController : BaseController
{
    [HttpGet]
    public IActionResult Index()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return Unauthorized(new StatusResponseDto<string>
            {
                Status = "error",
                Message = "You do not have permission to view user data"
            });

        return Ok(new PlaceholderResponse()
        {
            Data = []
        });
    }

    [HttpGet]
    [Route("continue")]
    public async Task<IActionResult> ContinueWatching()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return Unauthorized(new StatusResponseDto<string>
            {
                Status = "error",
                Message = "You do not have permission to view continue watching"
            });

        string? language = Language();
        string country = Country();

        await using MediaContext mediaContext = new();
        List<UserData> continueWatching = await mediaContext.UserData
            .AsNoTracking()
            .Where(user => user.UserId.Equals(userId))
            
            .Include(userData => userData.Movie)
            .ThenInclude(movie => movie.Media
                .Where(media => media.Site == "Youtube")
            )
            .Include(userData => userData.Movie)
            .ThenInclude(movie => movie.CertificationMovies
                .Where(certificationMovie => certificationMovie.Certification.Iso31661 == country)
            )
            .ThenInclude(certificationMovie => certificationMovie.Certification)
            .Include(userData => userData.Movie)
            .ThenInclude(movie => movie.VideoFiles)
            
            .Include(userData => userData.Tv)
            .ThenInclude(tv => tv.Media
                .Where(media => media.Site == "Youtube")
            )
            .Include(userData => userData.Tv)
            .ThenInclude(tv => tv.CertificationTvs
                .Where(certificationTv => certificationTv.Certification.Iso31661 == country)
            )
            .ThenInclude(certificationTv => certificationTv.Certification)
            .Include(userData => userData.Tv)
            .ThenInclude(tv => tv.Episodes
                .Where(episode => episode.SeasonNumber > 0 && episode.VideoFiles.Count != 0)
            )
            .ThenInclude(episode => episode.VideoFiles)
            
            .Include(userData => userData.Collection)
            .ThenInclude(collection => collection.CollectionMovies)
            .ThenInclude(collectionMovie => collectionMovie.Movie)
            .ThenInclude(movie => movie.CertificationMovies)
            .ThenInclude(certificationMovie => certificationMovie.Certification)
            .Include(userData => userData.Collection)
            .ThenInclude(collection => collection.CollectionMovies)
            .ThenInclude(collectionMovie => collectionMovie.Movie)
            .ThenInclude(movie => movie.Media
                .Where(media => media.Site == "Youtube")
            )
            .Include(userData => userData.Collection)
            .ThenInclude(collection => collection.CollectionMovies)
            .ThenInclude(collectionMovie => collectionMovie.Movie)
            .ThenInclude(movie => movie.VideoFiles)
            
            .Include(userData => userData.Special)
            .OrderByDescending(userData => userData.UpdatedAt)
            .ToListAsync();

        IEnumerable<UserData> filteredContinueWatching = continueWatching
            .DistinctBy(userData => new
            {
                userData.MovieId,
                userData.CollectionId,
                userData.TvId,
                userData.SpecialId
            }).ToList();

        return Ok(new ContinueWatchingDto
        {
            Data = filteredContinueWatching
                .Select(item => new ContinueWatchingItemDto(item,
                    country))
                .DistinctBy(item => item.Link),
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

        await using MediaContext mediaContext = new();

        List<UserData>? userData = body.Type switch
        {
            "movie" => await mediaContext.UserData
                .AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.MovieId == int.Parse(body.Id))
                .ToListAsync(),
            "tv" => await mediaContext.UserData
                .AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.TvId == int.Parse(body.Id))
                .ToListAsync(),
            "special" => await mediaContext.UserData
                .AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.SpecialId == Ulid.Parse(body.Id))
                .ToListAsync(),
            "collection" => await mediaContext.UserData
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

        await using MediaContext mediaContext = new();

        UserData? userData = body.Type switch
        {
            "movie" => await mediaContext.UserData
                .AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.MovieId == int.Parse(body.Id))
                .FirstOrDefaultAsync(),
            "tv" => await mediaContext.UserData
                .AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.TvId == int.Parse(body.Id))
                .FirstOrDefaultAsync(),
            "special" => await mediaContext.UserData
                .AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.SpecialId == Ulid.Parse(body.Id))
                .FirstOrDefaultAsync(),
            "collection" => await mediaContext.UserData
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

        await using MediaContext mediaContext = new();

        UserData? userData = body.Type switch
        {
            "movie" => await mediaContext.UserData
                .AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.MovieId == int.Parse(body.Id))
                .FirstOrDefaultAsync(),
            "tv" => await mediaContext.UserData
                .AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.TvId == int.Parse(body.Id))
                .FirstOrDefaultAsync(),
            "special" => await mediaContext.UserData
                .AsNoTracking()
                .Where(data => data.UserId.Equals(userId))
                .Where(data => data.SpecialId == Ulid.Parse(body.Id))
                .FirstOrDefaultAsync(),
            "collection" => await mediaContext.UserData
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
