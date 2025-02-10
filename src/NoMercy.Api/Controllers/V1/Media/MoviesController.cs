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
using NoMercy.Providers.TMDB.Models.Movies;
using Serilog.Events;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags(tags: "Media Movies")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/movie/{id:int}")] // match themoviedb.org API
public class MoviesController(MovieRepository movieRepository, MediaContext mediaContext) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Movie(int id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view movies");

        string language = Language();
        string country = Country();
        
        Movie? movie = await movieRepository.GetMovieAsync(mediaContext, userId, id, language, country);

        if (movie is not null)
            return Ok(new InfoResponseDto
            {
                Data = new(movie, country)
            });

        TmdbMovieClient tmdbMovieClient = new(id);
        TmdbMovieAppends? movieAppends = await tmdbMovieClient.WithAllAppends(true);

        if (movieAppends is null)
            return NotFoundResponse("Movie not found");

        // await _movieRepository.AddMovieAsync(id);

        return Ok(new InfoResponseDto
        {
            Data = new(movieAppends, country)
        });
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteMovie(int id)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to delete movies");

        await movieRepository.DeleteMovieAsync(id);

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Movie deleted"
        });
    }

    [HttpGet]
    [Route("available")]
    public async Task<IActionResult> Available(int id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view movies");

        bool available = await movieRepository.GetMovieAvailableAsync(userId, id);

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
    public IActionResult Watch(int id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view movies");

        string language = Language();

        IEnumerable<PlaylistResponseDto> playlist = movieRepository.GetMoviePlaylistAsync(userId, id, language)
            .Select(movie => new PlaylistResponseDto(movie));

        if (!playlist.Any())
            return NotFoundResponse("Movie not found");

        return Ok(playlist);
    }

    [HttpPost]
    [Route("like")]
    public async Task<IActionResult> Like(int id, [FromBody] LikeRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to like movies");

        bool success = await movieRepository.LikeMovieAsync(id, userId, request.Value);

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
    [Route("rescan")]
    public async Task<IActionResult> Rescan(int id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to rescan movies");

        Movie? movie = await mediaContext.Movies
            .AsNoTracking()
            .Include(movie => movie.Library)
                .ThenInclude(f => f.FolderLibraries)
                    .ThenInclude(f => f.Folder)
            .FirstOrDefaultAsync(movie => movie.Id == id);

        if (movie is null)
            return UnprocessableEntityResponse("Movie not found");

        try
        {
            JobDispatcher jobDispatcher = new();
            FileRepository fileRepository = new(mediaContext);
            FileManager fileManager = new(fileRepository, jobDispatcher);
            
            await fileManager.FindFiles(id, movie.Library);
        }
        catch (Exception e)
        {
            Logger.MovieDb(e.Message, LogEventLevel.Error);
        }

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Rescanning {0} for files",
            Args = new object[]
            {
                movie.Title
            }
        });
    }

    [HttpPost]
    [Route("refresh")]
    public async Task<IActionResult> Refresh(int id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to refresh movies");

        Movie? movie = await mediaContext.Movies
            .AsNoTracking()
            .Include(movie => movie.Library)
            .FirstOrDefaultAsync(movie => movie.Id == id);

        if (movie is null)
            return UnprocessableEntityResponse("Movie not found");

        try
        {
            JobDispatcher jobDispatcher = new();
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
            Message = "Refreshing {0}",
            Args = new object[]
            {
                movie.Title
            }
        });
    }
    
    [HttpPost]
    [Route("add")]
    public async Task<IActionResult> Add(int id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to add tv shows");

        await movieRepository.AddMovieAsync(id);

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Added to library",
        });
    }
}