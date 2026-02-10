using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO.Components;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
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
    [ResponseCache(Duration = 120)]
    public async Task<IActionResult> Tv(int id, CancellationToken ct = default)
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
                Data = new(tv, country, mediaContext)
            });

        TmdbTvClient tmdbTvClient = new(id, language: language);
        TmdbTvShowAppends? tvShowAppends = await tmdbTvClient.WithAllAppends(true);

        if (tvShowAppends is null)
            return NotFoundResponse("Tv show not found");

        // await _tvShowRepository.AddTvShowAsync(id);

        return Ok(new InfoResponseDto
        {
            Data = new(tvShowAppends, country)
        });
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteTv(int id, CancellationToken ct = default)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to delete shows");

        await tvShowRepository.DeleteTvAsync(id, ct);

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Show deleted"
        });
    }

    [HttpGet]
    [Route("available")]
    public async Task<IActionResult> Available(int id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view tv shows");

        bool available = await tvShowRepository.GetTvAvailableAsync(userId, id, ct);

        if (!available)
            return NotFound(new StatusResponseDto<AvailableResponseDto>
            {
                Data = new()
                {
                    Available = false
                },
                Status = "error",
                Message = "Tv show not found"
            });

        return Ok(new StatusResponseDto<AvailableResponseDto>
        {
            Data = new()
            {
                Available = true
            },
            Status = "ok",
            Message = "Tv show is available"
        });
    }

    [HttpGet]
    [Route("watch")]
    public async Task<IActionResult> Watch(int id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view tv shows");

        string language = Language();
        string country = Country();

        Tv? tv = await tvShowRepository.GetTvPlaylistAsync(userId, id, language, country, ct);

        if (tv is null)
            return NotFoundResponse("Tv show not found");

        VideoPlaylistResponseDto[] episodes = tv.Seasons
            .Where(season => season.SeasonNumber > 0)
            .SelectMany(season => season.Episodes)
            .Select(episode => new VideoPlaylistResponseDto(episode, "tv", id, country))
            .ToArray();

        VideoPlaylistResponseDto[] extras = tv.Seasons
            .Where(season => season.SeasonNumber == 0)
            .SelectMany(season => season.Episodes)
            .Select(episode => new VideoPlaylistResponseDto(episode, "tv", id, country))
            .ToArray();

        VideoPlaylistResponseDto[] result = episodes
            .Concat(extras)
            .Where(episode => episode.Id != 0)
            .ToArray();

        return Ok(result);
    }

    [HttpPost]
    [Route("like")]
    public async Task<IActionResult> Like(int id, [FromBody] LikeRequestDto request, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to like tv shows");

        bool success = await tvShowRepository.LikeTvAsync(id, userId, request.Value, ct);

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
    [Route("watch-list")]
    public async Task<IActionResult> AddToWatchList(int id, [FromBody] WatchListRequestDto request, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to manage watch list");

        bool success = await tvShowRepository.AddToWatchListAsync(id, userId, request.Add, ct);

        if (!success)
            return UnprocessableEntityResponse("Tv show not found");

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = request.Add ? "Tv show added to watch list" : "Tv show removed from watch list"
        });
    }

    [HttpPost]
    [Route("rescan")]
    public async Task<IActionResult> Rescan(int id, CancellationToken ct = default)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to rescan tv shows");

        Tv? tv = await mediaContext.Tvs
            .AsNoTracking()
            .Where(tv => tv.Id == id)
            .Include(tv => tv.Library)
            .ThenInclude(library => library.FolderLibraries)
            .ThenInclude(folderLibrary => folderLibrary.Folder)
            .FirstOrDefaultAsync(ct);

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
            Message = "Rescanning {0} for files in the background",
            Args = [tv.Title]
        });
    }

    [HttpPost]
    [Route("refresh")]
    public async Task<IActionResult> Refresh(int id, CancellationToken ct = default)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to refresh tv shows");

        Tv? tv = await mediaContext.Tvs
            .AsNoTracking()
            .Where(tv => tv.Id == id)
            .Include(tv => tv.Library)
            .FirstOrDefaultAsync(ct);

        if (tv is null)
            return UnprocessableEntityResponse("Tv show not found");

        TmdbTvClient tvClient = new(id);
        TmdbTvShowDetails? show = await tvClient.Details(true);
        if (show == null) return NotFoundResponse("Tv show not found");

        bool isAnime = KitsuIo.IsAnime(show.Name, show.FirstAirDate.ParseYear()).Result;

        Library? tvLibrary = await mediaContext.Libraries
            .Where(f => f.Type == (isAnime ? "anime" : "tv"))
            .FirstOrDefaultAsync(ct) ?? await mediaContext.Libraries
            .Where(f => f.Type == "tv")
            .FirstOrDefaultAsync(ct);

        jobDispatcher.DispatchJob<AddShowJob>(id, tvLibrary?.Id ?? tv.Library.Id);

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Refreshing {0} data in background",
            Args = [tv.Title]
        });
    }

    [HttpPost]
    [Route("add")]
    public async Task<IActionResult> Add(int id, CancellationToken ct = default)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to add tv shows");

        TmdbTvClient tvClient = new(id);
        TmdbTvShowDetails? show = await tvClient.Details(true);
        if (show == null) return NotFoundResponse("Tv show not found");

        bool isAnime = KitsuIo.IsAnime(show.Name, show.FirstAirDate.ParseYear()).Result;

        Library? library = await mediaContext.Libraries
            .Where(f => f.Type == (isAnime ? "anime" : "tv"))
            .FirstOrDefaultAsync(ct) ?? await mediaContext.Libraries
            .Where(f => f.Type == "tv")
            .FirstOrDefaultAsync(ct);

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
            Message = "Adding {0} in the background",
            Args = [show.Name]
        });
    }

    [HttpGet]
    [Route("missing")]
    public async Task<IActionResult> Missing(int id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view library");
        string language = Language();

        IEnumerable<Episode> episodes = await tvShowRepository
            .GetMissingLibraryShows(userId, id, language, ct);

        List<IGrouping<long, MissingEpisodeDto>> concat = episodes
            .Select(episode => new MissingEpisodeDto(episode))
            .OrderBy(episode => episode.SeasonNumber)
            .ThenBy(episode => episode.EpisodeNumber)
            .GroupBy(episode => episode.SeasonNumber)
            .ToList();

        if (concat.Count == 0)
        {
            SeasonCardData noItems = new()
            {
                Id = 0,
                Title = "No missing episodes",
                SeasonNumber = 0,
                EpisodeNumber = 0,
                Overview = "There are no missing episodes in this season.",
                Available = false
            };

            return Ok(ComponentResponse.From(
                Component.Grid()
                    .WithId("missing-episodes-empty")
                    .WithItems(
                        Component.SeasonCard(noItems)
                            .WithWatch().Build()
                        )
                    ));
        }

        return Ok(ComponentResponse.From(
            Component.List()
                .WithId("missing-episodes")
                .WithItems(concat.SelectMany(seasonGroup => new ComponentEnvelope[]
                {
                    // Season title component
                    Component.SeasonTitle(new((int)seasonGroup.Key, seasonGroup.Count()))
                        .WithId($"season-{seasonGroup.Key}-title")
                        ,

                    // Episodes grid for this season
                    Component.Grid()
                        .WithId($"season-{seasonGroup.Key}-episodes")
                        .WithProperties(new() { { "paddingTop", 16 } })
                        .WithItems(seasonGroup.Select(episode =>
                            Component.SeasonCard(new(episode))
                                .WithWatch()))

                }))));
    }

}
