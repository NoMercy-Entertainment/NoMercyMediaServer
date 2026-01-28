using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO.Components;
using NoMercy.Api.Services;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

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
      
        List<GenreRowDto<GenreRowItemDto>> newData = result.ToList();
        bool hasMore = newData.Count() >= request.Take;

        newData = newData.Take(request.Take).ToList();

        PaginatedResponse<GenreRowDto<GenreRowItemDto>> response = new()
        {
            Data = newData,
            NextPage = hasMore ? request.Page + 1 : null,
            HasMore = hasMore
        };

        if (request.Page != 0) return Ok(response);

        LibraryRepository libraryRepository = new(new());
        List<Library> libraries = await libraryRepository.GetLibraries(userId);

        // Fetch all library data in parallel - each task needs its own MediaContext for thread safety
        Task<(Library library, List<Movie> movies, List<Tv> shows)>[] libraryDataTasks = libraries
            .Select(async library =>
            {
                MediaContext context = new();
                List<Movie> libraryMovies = [];
                await foreach (Movie movie in libraryRepository
                                   .GetLibraryMovies(context, userId, library.Id, language, request.Take, request.Page, m => m.CreatedAt, "desc"))
                {
                    libraryMovies.Add(movie);
                }

                List<Tv> libraryShows = [];
                await foreach (Tv tv in libraryRepository
                                   .GetLibraryShows(context, userId, library.Id, language, request.Take, request.Page, m => m.CreatedAt, "desc"))
                {
                    libraryShows.Add(tv);
                }

                return (library, libraryMovies, libraryShows);
            })
            .ToArray();

        (Library library, List<Movie> movies, List<Tv> shows)[] libraryDataResults = await Task.WhenAll(libraryDataTasks);

        foreach ((Library library, List<Movie> libraryMovies, List<Tv> libraryShows) in libraryDataResults.OrderByDescending(r => r.library.Order))
        {
            response.Data = response.Data.Prepend(new()
            {
                Title = "Latest in " + library.Title,
                MoreLink = new($"/libraries/{library.Id}", UriKind.Relative),
                Items = libraryMovies.Select(movie => new GenreRowItemDto(movie, country))
                    .Concat(libraryShows.Select(tv => new GenreRowItemDto(tv, country)))
            });
        }

        return Ok(response);
    }
    
    [HttpGet("home")]
    public async Task<IActionResult> Home()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view continue watching");

        ComponentResponse result = await _homeService.GetHomeData(User.UserId(), Language(), Country());

        return Ok(result);
    }

    [HttpPost("home/card")]
    public async Task<IActionResult> HomeCard([FromBody] CardRequestDto request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view home card");

        ComponentResponse result = await _homeService.GetHomeCard(User.UserId(), Language(), request.ReplaceId);

        return Ok(result);
    }

    [HttpGet("home/tv")]
    public async Task<IActionResult> HomeTv()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view home tv");

        ComponentResponse result = await _homeService.GetHomeTvContent(User.UserId(), Language(), Country());

        return Ok(result);
    }

    [HttpPost("home/continue")]
    public async Task<IActionResult> HomeContinue([FromBody] CardRequestDto request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view continue watching");

        ComponentResponse result =
            await _homeService.GetHomeContinueContent(User.UserId(), Language(), Country(), request.ReplaceId);

        return Ok(result);
    }

    
    
    [HttpHead]
    [Route("trailer/{trailerId}")]
    public async Task<IActionResult> HasTrailer(int id, string trailerId)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view tv shows");
        
        string folder = Path.Combine(AppFiles.TranscodePath, trailerId);
        string jsonFile = Path.Combine(folder, "info.json");
        
        if(System.IO.File.Exists(jsonFile))
        {
            string text = await System.IO.File.ReadAllTextAsync(jsonFile);
            TrailerInfo? trailerInfo = text.FromJson<TrailerInfo>();
            if (trailerInfo is not null)
            {
                return Ok(new StatusResponseDto<string>
                {
                    Status = "ok",
                    Message = "Trailer found"
                });
            }
        }

        string arg =
            $"-f bestvideo+bestaudio -j https://youtube.com/watch?v={trailerId} --extractor-args \"youtube:player_client=default\" ";
        Shell.ExecResult result = await Shell.ExecAsync(AppFiles.YtdlpPath, arg);
        
        if(!result.Success || string.IsNullOrEmpty(result.StandardOutput))
        {
            Logger.Encoder(result.StandardError, LogEventLevel.Error);
            return NotFoundResponse("Trailer not found");
        }
        
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        await System.IO.File.WriteAllTextAsync(jsonFile, result.StandardOutput);
        
        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Trailer found"
        });
    }
    
    [HttpGet]
    [Route("trailer/{trailerId}")]
    public async Task<IActionResult> Trailer(int id, string trailerId)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view tv shows");

        string language = Language();
        
        string folder = Path.Combine(AppFiles.TranscodePath, trailerId);
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);
        
        string text = await System.IO.File.ReadAllTextAsync(Path.Combine(folder, "info.json"));
        TrailerInfo? trailerInfo = text.FromJson<TrailerInfo>();

        if (trailerInfo is null)
        {
            Logger.Encoder("Trailer info is null", LogEventLevel.Error);
            return NotFoundResponse("Trailer not found");
        }
        
        if (System.IO.File.Exists(Path.Combine(folder, "video_00002.ts")))
        {
            return Ok(
                new VideoPlaylistResponseDto
                {
                    Id = 0,
                    Title =  trailerInfo.Title,
                    Description = trailerInfo.Description,
                    Duration = trailerInfo.Duration.ToHis(),
                    Image =  trailerInfo.Thumbnail.ToString(),
                    File = $"/transcodes/{trailerId}/video.m3u8",
                    Origin = Info.DeviceId,
                    PlaylistId = trailerInfo.Id,
                    Tracks = trailerInfo.Subtitles
                        .Where(t => t.Value.Any(s => s.Ext == "vtt"))
                        .Select(t => new IVideoTrack
                        {
                            Label = t.Value.First(s => s.Ext == "vtt").Name,
                            File = $"/transcodes/{trailerId}/-.{t.Key}.vtt",
                            Language = t.Key,
                            Kind = "subtitles"
                        }).ToList(), 
                    Sources =
                    [
                        new()
                        {
                            Src = $"/transcodes/{trailerId}/video.m3u8",
                            Type = "application/x-mpegURL",
                            Languages = [trailerInfo.Language ?? ""]
                        }
                    ]
                }
            );
        }

        _ = Task.Run(async () =>
        {
            StringBuilder sb = new();
            
            sb.Append(AppFiles.YtdlpPath);
            sb.Append(" -f bestvideo+bestaudio  --extractor-args \"youtube:player_client=default\" ");
            
            if (!string.IsNullOrEmpty(language))
                sb.Append($" -o \"subtitle:{language}.%(ext)s\" --sub-langs all --write-subs ");
            
            sb.Append(trailerId);
            
            sb.Append(" -o - ");
            sb.Append($" | {AppFiles.FfmpegPath} -i pipe: -map 0:0 -map 0:1 -c:v libx264 -c:a aac -ac 2 -preset ultrafast ");
            sb.Append("-segment_list_type m3u8 -hls_playlist_type event -hls_init_time 4 -hls_time 4 -hls_segment_filename video_%05d.ts video.m3u8 ");

            if (Software.IsWindows)
            {
                Logger.Encoder($"cmd -c \"{sb}\"", LogEventLevel.Debug);
                Shell.ExecSync("cmd", $"/c \"{sb}\"", new()
                {
                    WorkingDirectory = folder
                });
            }
            else
            {
                Logger.Encoder($"/bin/bash -c \"{sb}\"", LogEventLevel.Debug);
                Shell.ExecSync("/bin/bash", $"-c \"{sb}\"", new()
                {
                    WorkingDirectory = folder
                });
            }

            return Task.CompletedTask;
        });

        while (!System.IO.File.Exists(Path.Combine(folder, "video_00002.ts")))
        {
            Task.Delay(1000).Wait();
        }
        
        return Ok(
            new VideoPlaylistResponseDto
            {
                Id = 0,
                Title =  trailerInfo.Title,
                Description = trailerInfo.Description,
                Duration = trailerInfo.Duration.ToHis(),
                Image =  trailerInfo.Thumbnail.ToString(),
                File = $"/transcodes/{trailerId}/video.m3u8",
                Origin = Info.DeviceId,
                PlaylistId = trailerInfo.Id,
                Tracks = trailerInfo.Subtitles
                    .Where(t => t.Value.Any(s => s.Ext == "vtt"))
                    .Select(t => new IVideoTrack
                    {
                        Label = t.Value.First(s => s.Ext == "vtt").Name,
                        File = $"/transcodes/{trailerId}/-.{t.Key}.vtt",
                        Language = t.Key,
                        Kind = "subtitles"
                    }).ToList(), 
                Sources =
                [
                    new()
                    {
                        Src = $"/transcodes/{trailerId}/video.m3u8",
                        Type = "application/x-mpegURL",
                        Languages = [trailerInfo.Language ?? ""]
                    }
                ]
            }
        );
    }

    [HttpDelete]
    [Route("trailer/{trailerId}")]
    public async Task<IActionResult> RemoveTrailer(int id, string trailerId)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view tv shows");
        
        string folder = Path.Combine(AppFiles.TranscodePath, trailerId);
        
        if (!Directory.Exists(folder))
            return Ok(new StatusResponseDto<string>
            {
                Status = "ok",
                Message = "Trailer removed"
            });
        
        try
        {
            Directory.Delete(folder, recursive: true);
            Logger.Encoder($"Trailer folder deleted: {folder}");
        }
        catch (Exception ex)
        {
            Logger.Encoder($"Failed to delete trailer folder {folder}: {ex.Message}", LogEventLevel.Error);
            return StatusCode(500, new StatusResponseDto<string>
            {
                Status = "error",
                Message = "Failed to remove trailer"
            });
        }

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Trailer removed"
        });
    }
}