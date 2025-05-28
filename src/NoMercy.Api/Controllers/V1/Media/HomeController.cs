using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Api.Services;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.Data.Repositories;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags("Media")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}")]
public class HomeController : BaseController
{
    private readonly HomeService _homeService;

    public HomeController(HomeService homeService)
    {
        _homeService = homeService;
    }
    
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] PageRequestDto request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view home");
        
        Guid userId = User.UserId();
        string language = Language();
        string country = Country();
    
        List<GenreRowDto<GenreRowItemDto>> result = await _homeService.GetHomePageContent(userId, language, country, request);
        // IActionResult response =  GetPaginatedResponse(result, request);
        
        List<GenreRowDto<GenreRowItemDto>> newData = result.ToList();
        bool hasMore = newData.Count() >= request.Take;

        newData = newData.Take(request.Take).ToList();

        PaginatedResponse<GenreRowDto<GenreRowItemDto>> response = new()
        {
            Data = newData,
            NextPage = hasMore ? request.Page + 1 : null,
            HasMore = hasMore
        };
        
        // IActionResult response =  GetPaginatedResponse(result, request);
        if (request.Page == 0)
        {
            LibraryRepository libraryRepository = new(new());
            IQueryable<Library> libraries = libraryRepository.GetLibraries(userId);

            foreach (Library library in libraries.OrderByDescending(library => library.Order))
            {
                IEnumerable<Movie> movies =
                    libraryRepository.GetLibraryMovies(userId, library.Id, language, 10, 0, m => m.CreatedAt, "desc");
                IEnumerable<Tv> shows =
                    libraryRepository.GetLibraryShows(userId, library.Id, language, 10, 0, m => m.CreatedAt, "desc");

                response.Data = response.Data.Prepend(new()
                {
                    Title = "Latest in " + library.Title,
                    MoreLink = new($"/libraries/{library.Id}", UriKind.Relative),
                    Items = movies.Select(movie => new GenreRowItemDto(movie, country))
                        .Concat(shows.Select(tv => new GenreRowItemDto(tv, country)))
                });
            }
        }

        return Ok(response);
    }

    [HttpGet("home")]
    public async Task<IActionResult> ContinueWatching()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view continue watching");

        Render result = await _homeService.GetContinueWatchingContent(User.UserId(), Language(), Country());
        
        return Ok(result);
    }

    [HttpPost("home/card")]
    public async Task<IActionResult> HomeCard([FromBody] CardRequestDto request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view home card");

        Render result = await _homeService.GetHomeCard(User.UserId(), Language(), request.ReplaceId);

        return Ok(result);
    }
    
    [HttpGet("home/tv")]
    public async Task<IActionResult> HomeTv()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view home tv");

        Render result = await _homeService.GetHomeTvContent(User.UserId(), Language(), Country());

        return Ok(result);
    }

    [HttpPost("home/continue")]
    public async Task<IActionResult> HomeContinue([FromBody] CardRequestDto request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view continue watching");

        Render result = await _homeService.GetHomeContinueContent(User.UserId(), Language(), Country(), request.ReplaceId);

        return Ok(result);
    }
    
    [HttpGet]
    [Route("screensaver")]
    public async Task<IActionResult> Screensaver()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view screensaver");

        ScreensaverDto result = await _homeService.GetScreensaverContent(User.UserId());
        
        return Ok(result);
    }
    
    [HttpGet]
    [Route("status")]
    public IActionResult Status()
    {
        return Ok(new
        {
            Status = "ok",
            Version = "1.0",
            Message = "NoMercy MediaServer API is running",
            Timestamp = DateTime.UtcNow
        });
    }
    
}
