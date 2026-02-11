using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers;
using NoMercy.Helpers.Extensions;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using Serilog.Events;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags(tags: "Media Movies")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/movie/{id:int}")] // match themoviedb.org API
public class MoviesController(
    MovieRepository movieRepository,
    JobDispatcher jobDispatcher,
    MediaContext mediaContext
    ) : BaseController
{
    [HttpGet]
    [ResponseCache(Duration = 120)]
    public async Task<IActionResult> Movie(int id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view movies");

        string language = Language();
        string country = Country();

        Movie? movie = await movieRepository.GetMovieDetailAsync(mediaContext, userId, id, language, country);

        if (movie is not null)
            return Ok(new InfoResponseDto
            {
                Data = new(movie, country)
            });

        try
        {
            TmdbMovieClient tmdbMovieClient = new(id, language: language);
            TmdbMovieAppends? movieAppends = await tmdbMovieClient.WithAllAppends(true);

            if (movieAppends is null)
                return NotFoundResponse("Movie not found");

            return Ok(new InfoResponseDto
            {
                Data = new(movieAppends, country)
            });
        }
        catch (Exception)
        {
            return NotFoundResponse("Movie not found");
        }
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteMovie(int id, CancellationToken ct = default)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to delete movies");

        await movieRepository.DeleteMovieAsync(id, ct);

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Movie deleted"
        });
    }

    [HttpGet]
    [Route("available")]
    public async Task<IActionResult> Available(int id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view movies");

        string language = Language();
        string country = Country();

        bool available = await movieRepository.GetMovieAvailableAsync(userId, id, ct);

        if (!available)
            return NotFound(new StatusResponseDto<AvailableResponseDto>
            {
                Data = new()
                {
                    Available = false
                },
                Status = "error",
                Message = "Movie not found"
            });

        return Ok(new StatusResponseDto<AvailableResponseDto>
        {
            Data = new()
            {
                Available = true
            },
            Status = "ok",
            Message = "Movie is available"
        });
    }

    [HttpGet]
    [Route("watch")]
    public async Task<IActionResult> Watch(int id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view movies");

        string language = Language();
        string country = Country();

        IEnumerable<VideoPlaylistResponseDto> playlist =
            (await movieRepository.GetMoviePlaylistAsync(userId, id, language, country, ct))
            .Select(movie => new VideoPlaylistResponseDto(movie, Config.MovieMediaType, id, country));

        if (!playlist.Any())
            return NotFoundResponse("Movie not found");

        return Ok(playlist);
    }

    [HttpPost]
    [Route("like")]
    public async Task<IActionResult> Like(int id, [FromBody] LikeRequestDto request, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to like movies");

        bool success = await movieRepository.LikeMovieAsync(id, userId, request.Value, ct);

        if (!success)
            return UnprocessableEntityResponse("Movie not found");

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "{0}: {1}",
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

        bool success = await movieRepository.AddToWatchListAsync(id, userId, request.Add, ct);

        if (!success)
            return UnprocessableEntityResponse("Movie not found");

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = request.Add ? "Movie added to watch list" : "Movie removed from watch list"
        });
    }

    [HttpPost]
    [Route("rescan")]
    public async Task<IActionResult> Rescan(int id, CancellationToken ct = default)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to rescan movies");

        Movie? movie = await mediaContext.Movies
            .AsNoTracking()
            .Include(movie => movie.Library)
            .ThenInclude(f => f.FolderLibraries)
            .ThenInclude(f => f.Folder)
            .FirstOrDefaultAsync(movie => movie.Id == id, ct);

        if (movie is null)
            return UnprocessableEntityResponse("Movie not found");

        try
        {
            jobDispatcher.DispatchJob<RescanFilesJob>(id, movie.LibraryId);
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
            Args = [movie.Title]
        });
    }

    [HttpPost]
    [Route("refresh")]
    public async Task<IActionResult> Refresh(int id, CancellationToken ct = default)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to refresh movies");

        Movie? movie = await mediaContext.Movies
            .AsNoTracking()
            .Include(movie => movie.Library)
            .FirstOrDefaultAsync(movie => movie.Id == id, ct);

        if (movie is null)
            return UnprocessableEntityResponse("Movie not found");

        try
        {
            jobDispatcher.DispatchJob<AddMovieJob>(id, movie.Library.Id);
        }
        catch (Exception e)
        {
            Logger.Encoder(e.Message, LogEventLevel.Error);
            return InternalServerErrorResponse(e.Message);
        }

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Refreshing {0} in the background",
            Args = [movie.Title]
        });
    }

    [HttpPost]
    [Route("add")]
    public async Task<IActionResult> Add(int id, CancellationToken ct = default)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to add tv shows");

        Library? library = await mediaContext.Libraries
            .Where(f => f.Type == Config.MovieMediaType)
            .FirstOrDefaultAsync(ct);

        if (library is null)
            return UnprocessableEntityResponse("No movie library found");

        try
        {
            jobDispatcher.DispatchJob<AddMovieJob>(id, library.Id);
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
            Args = [library.Title]
        });
    }
}
