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
using NoMercy.Helpers;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Other;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.TV;
using Serilog.Events;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags(tags: "Media TV Shows")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/tv/{id:int}")] // match themoviedb.org API
public class TvShowsController(
    TvShowRepository tvShowRepository, 
    JobDispatcher jobDispatcher,
    MediaContext mediaContext
    ) : BaseController
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

        TmdbTvClient tmdbTvClient = new(id, language: language);
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

        VideoPlaylistResponseDto[] episodes = tv.Seasons
            .Where(season => season.SeasonNumber > 0)
            .SelectMany(season => season.Episodes)
            .Select(episode => new VideoPlaylistResponseDto(episode, "tv", id))
            .ToArray();

        VideoPlaylistResponseDto[] extras = tv.Seasons
            .Where(season => season.SeasonNumber == 0)
            .SelectMany(season => season.Episodes)
            .Select(episode => new VideoPlaylistResponseDto(episode, "tv", id))
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
            jobDispatcher.DispatchJob<RescanFilesJob>(id, tv.LibraryId);
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

        TmdbTvClient tvClient = new(id);
        TmdbTvShowDetails? show = await tvClient.Details(true);
        if (show == null) return NotFoundResponse("Tv show not found");

        bool isAnime = KitsuIo.IsAnime(show.Name, show.FirstAirDate.ParseYear()).Result;

        Library? tvLibrary = await mediaContext.Libraries
            .Where(f => f.Type == (isAnime ? "anime" : "tv"))
            .FirstOrDefaultAsync() ?? await mediaContext.Libraries
            .Where(f => f.Type == "tv")
            .FirstOrDefaultAsync();

        JobDispatcher jobDispatcher = new();
        jobDispatcher.DispatchJob<AddShowJob>(id, tvLibrary?.Id ?? tv.Library.Id);

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
        
        TmdbTvClient tvClient = new(id);
        TmdbTvShowDetails? show = await tvClient.Details(true);
        if (show == null) return NotFoundResponse("Tv show not found");

        bool isAnime = KitsuIo.IsAnime(show.Name, show.FirstAirDate.ParseYear()).Result;

        Library? library = await mediaContext.Libraries
            .Where(f => f.Type == (isAnime ? "anime" : "tv"))
            .FirstOrDefaultAsync() ?? await mediaContext.Libraries
            .Where(f => f.Type == "tv")
            .FirstOrDefaultAsync();

        if (library is null)
            return UnprocessableEntityResponse("No Tv library found");
        
        try
        {
            jobDispatcher.DispatchJob<AddShowJob>(id, library.Id);
        }
        catch (Exception e)
        {
            Logger.Encoder(e.Message, LogEventLevel.Error);
            return InternalServerErrorResponse(e.Message);
        }

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Added to library"
        });
    }
    
    [HttpGet]
    [Route("missing")]
    public async Task<IActionResult> Missing(int id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view library");
        string language = Language();
        
        IEnumerable<Episode> episodes = await tvShowRepository
            .GetMissingLibraryShows(userId, id, language);
        
        List<IGrouping<long, MissingEpisodeDto>> concat = episodes
            .Select(episode => new MissingEpisodeDto(episode))
            .OrderBy(episode => episode.SeasonNumber)
            .ThenBy(episode => episode.EpisodeNumber)
            .GroupBy(episode => episode.SeasonNumber)
            .ToList();

        if (!concat.Any())
        {
            Episode noItems = new()
            {
                Id = 0,
                Title = "No missing episodes",
                SeasonNumber = 0,
                EpisodeNumber = 0,
                Overview = "There are no missing episodes in this season."
            };
            
            return Ok(new Render
            {
                Data =
                [
                    new ComponentBuilder<EpisodeDto>()
                        .WithComponent("NMGrid")
                        .WithProps((props, id) => props
                            .WithItems<object>([
                                new ComponentBuilder<EpisodeDto>()
                                    .WithComponent("NMSeasonCard")
                                    .WithProps((props, _) => props
                                        .WithData(new(noItems))
                                        .WithWatch())
                                    .Build()
                            ]))
                        .Build()
                ]
            });
        }

        return Ok(new Render
        {
            Data =
            [
                new ComponentBuilder<object>()
                    .WithComponent("NMList")
                    .WithProps((props, id) => props
                        .WithItems(
                            concat.SelectMany(seasonGroup => new object[]
                            {
                                // Season title component
                                new ComponentBuilder<object>()
                                    .WithComponent("NMSeasonTitle")
                                    .WithProps((titleProps, id) => titleProps
                                        .WithData(new
                                        {
                                            seasonNumber = seasonGroup.Key,
                                            title = $"Season {seasonGroup.Key}",
                                            episodeCount = seasonGroup.Count()
                                        }))
                                    .Build(),

                                // Episodes grid for this season
                                new ComponentBuilder<object>()
                                    .WithComponent("NMGrid")
                                    .WithProps((gridProps, _) => gridProps
                                        .WithItems(
                                            seasonGroup.Select(episode =>
                                                new ComponentBuilder<object>()
                                                    .WithComponent("NMSeasonCard")
                                                    .WithProps((props, _) => props
                                                        .WithData(episode)
                                                        .WithWatch())
                                                    .Build())))
                                    .Build()
                            })))
                    .Build()
            ]
        });
    }
}