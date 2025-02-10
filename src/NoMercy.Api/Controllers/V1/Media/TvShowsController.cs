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
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.Networking;
using NoMercy.NmSystem;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.TV;
using Serilog.Events;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags(tags: "Media TV Shows")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/tv/{id:int}")] // match themoviedb.org API
public class TvShowsController(TvShowRepository tvShowRepository, MediaContext mediaContext) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Tv(int id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view tv shows");

        string language = Language();
        string country = Country();
        
        Tv? tv = await tvShowRepository.GetTvAsync(mediaContext, userId, id, language, country);

        if (tv is not null)
            return Ok(new InfoResponseDto
            {
                Data = new(tv, language)
            });

        TmdbTvClient tmdbTvClient = new(id);
        TmdbTvShowAppends? tvShowAppends = await tmdbTvClient.WithAllAppends(true);

        if (tvShowAppends is null)
            return NotFoundResponse("Tv show not found");

        // await _tvShowRepository.AddTvShowAsync(id);

        return Ok(new InfoResponseDto
        {
            Data = new(tvShowAppends, language)
        });
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteTv(int id)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to delete shows");

        await tvShowRepository.DeleteTvAsync(id);

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Show deleted"
        });
    }

    [HttpGet]
    [Route("available")]
    public async Task<IActionResult> Available(int id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view tv shows");

        bool available = await tvShowRepository.GetTvAvailableAsync(userId, id);

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
    [Route("watch")]
    public async Task<IActionResult> Watch(int id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view tv shows");

        string language = Language();

        Tv? tv = await tvShowRepository.GetTvPlaylistAsync(userId, id, language);

        if (tv is null)
            return NotFoundResponse("Tv show not found");

        PlaylistResponseDto[] episodes = tv.Seasons
            .Where(season => season.SeasonNumber > 0)
            .SelectMany(season => season.Episodes)
            .Select(episode => new PlaylistResponseDto(episode))
            .ToArray();

        PlaylistResponseDto[] extras = tv.Seasons
            .Where(season => season.SeasonNumber == 0)
            .SelectMany(season => season.Episodes)
            .Select(episode => new PlaylistResponseDto(episode))
            .ToArray();

        return Ok(episodes.Concat(extras).ToArray());
    }

    [HttpPost]
    [Route("like")]
    public async Task<IActionResult> Like(int id, [FromBody] LikeRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to like tv shows");

        bool success = await tvShowRepository.LikeTvAsync(id, userId, request.Value);

        if (!success)
            return UnprocessableEntityResponse("Tv show not found");

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
    [Route("rescan")]
    public async Task<IActionResult> Rescan(int id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to rescan tv shows");

        Tv? tv = await mediaContext.Tvs
            .AsNoTracking()
            .Where(tv => tv.Id == id)
            .Include(tv => tv.Library)
                .ThenInclude(library => library.FolderLibraries)
                    .ThenInclude(folderLibrary => folderLibrary.Folder)
            .FirstOrDefaultAsync();

        if (tv is null)
            return UnprocessableEntityResponse("Tv show not found");

        try
        {
            JobDispatcher jobDispatcher = new();
            FileRepository fileRepository = new(mediaContext);
            FileManager fileManager = new(fileRepository, jobDispatcher);
            
            await fileManager.FindFiles(id, tv.Library);
        }
        catch (Exception e)
        {
            Logger.Encoder(e.Message, LogEventLevel.Error);
            return InternalServerErrorResponse(e.Message);
        }

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Rescanning {0} for files",
            Args = new object[]
            {
                tv.Title
            }
        });
    }

    [HttpPost]
    [Route("refresh")]
    public async Task<IActionResult> Refresh(int id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to refresh tv shows");

        Tv? tv = await mediaContext.Tvs
            .AsNoTracking()
            .Where(tv => tv.Id == id)
            .Include(tv => tv.Library)
            .FirstOrDefaultAsync();

        if (tv is null)
            return UnprocessableEntityResponse("Tv show not found");

        JobDispatcher jobDispatcher = new();
        jobDispatcher.DispatchJob<AddShowJob>(id, tv.Library.Id);

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Rescanning {0} for files",
            Args = new object[]
            {
                tv.Title
            }
        });
    }

    [HttpPost]
    [Route("add")]
    public async Task<IActionResult> Add(int id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to add tv shows");

        await tvShowRepository.AddTvShowAsync(id);

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Added to library",
        });
    }
}