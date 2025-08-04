using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Api.Services;
using NoMercy.Database;
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

    [HttpGet("home")]
    public async Task<IActionResult> ContinueWatching()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view continue watching");

        Render result = await _homeService.GetHomeData(User.UserId(), Language(), Country());

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

        Render result =
            await _homeService.GetHomeContinueContent(User.UserId(), Language(), Country(), request.ReplaceId);

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
    [AllowAnonymous]
    [Route("/status")]
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

    [HttpGet]
    [Route("permissions")]
    public IActionResult Permissions()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have access to this server");

        return Ok(new
        {
            owner = User.IsOwner(),
            manager = User.IsModerator(),
            allowed = User.IsAllowed()
        });
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

        Shell.ExecResult result = await Shell.ExecAsync(AppFiles.YtdlpPath, $"{trailerId} -f bestvideo[ext=mp4]+bestaudio[ext=m4a]/bestvideo[ext=mp4]+bestaudio[ext=m4a] -j");
        
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
        
        if (System.IO.File.Exists(Path.Combine(folder, "video_00001.ts")))
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
                        .Select(t => new IVideoTrack
                        {
                            Label = t.Value.Name,
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
        
        StringBuilder sb = new();
        
        sb.Append(" -f bestvideo[ext=mp4]+bestaudio[ext=m4a]/bestvideo[ext=mp4]+bestaudio[ext=m4a] ");
        
        if (!string.IsNullOrEmpty(language))
            sb.Append(" -o \"subtitle:%(language)s.%(ext)s\" --sub-langs all --write-subs ");
        
        sb.Append(trailerId);

        sb.Append(" -o - ");
        sb.Append($"| {AppFiles.FfmpegPath} -i pipe: -c:v libx264 -c:a aac -preset ultrafast ");
        sb.Append(" -hls_allow_cache 1 -hls_flags independent_segments -hls_segment_type mpegts -segment_list_type m3u8 -segment_time_delta 1 -start_number 0 -hls_playlist_type event -hls_init_time 4 -hls_time 4 -hls_list_size 0 -hls_segment_filename video_%05d.ts video.m3u8 ");
        
        Logger.Encoder(AppFiles.YtdlpPath + " " + sb, LogEventLevel.Debug);

        Shell.ExecResult x  = Shell.ExecSync("yt-dlp", sb.ToString(), new()
        {
            WorkingDirectory = folder
        });
        
        Logger.Encoder(x, LogEventLevel.Debug);

        while (!System.IO.File.Exists(Path.Combine(folder, "video_00001.ts")))
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
                    .Select(t => new IVideoTrack
                    {
                        Label = t.Value.Name,
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

        string language = Language();

        string folder = Path.Combine(AppFiles.TranscodePath, trailerId);
        if (Directory.Exists(folder))
            Directory.CreateDirectory(folder);
        
        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Trailer removed"
        });
    }
}