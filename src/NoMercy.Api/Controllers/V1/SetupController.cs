using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Api.DTOs.Media;
using NoMercy.Data.Repositories;
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
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.Services;
using NoMercy.Database;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;

namespace NoMercy.Api.Controllers.V1;

[ApiController]
[Tags("App Setup")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/setup")]
public class SetupController(
    MediaContext context,
    SetupService setupService,
    LibraryRepository libraryRepository,
    MusicRepository musicRepository,
    HomeService homeService, 
    CollectionRepository collectionRepository,
    SpecialRepository specialRepository
) : BaseController
{
    [HttpGet("libraries")]
    public async Task<IActionResult> Libraries()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view libraries");

        List<LibrariesResponseItemDto> response = (await setupService.GetSetupLibraries(userId))
            .Select(library => new LibrariesResponseItemDto(library))
            .ToList();

        return Ok(new LibrariesDto
        {
            Data = response.OrderBy(library => library.Order)
        });
    }
    
    [HttpGet]
    [Route("server-info")]
    [ResponseCache(Duration = 3600)]
    public IActionResult ServerInfo()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view server information");

        bool setupComplete = context.Libraries.Any()
                             && context.Folders.Any()
                             && context.EncoderProfiles.Any();
        
        Configuration? device = context.Configuration.FirstOrDefault(device => device.Key == "serverName");
        string serverName = device?.Value ?? Environment.MachineName;

        return Ok(new StatusResponseDto<ServerInfoDto>
        {
            Status = "ok",
            Data = new()
            {
                Server = serverName,
                Cpu = Info.CpuNames,
                Gpu = Info.GpuNames,
                Os = $"{Info.Platform.ToTitleCase()} {Info.OsVersion}",
                Arch = Info.Architecture,
                Version = Software.GetReleaseVersion(),
                BootTime = Info.StartTime,
                SetupComplete = setupComplete
            }
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


    [HttpGet("music-playlists")]
    public async Task<IActionResult> Index()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view playlists");

        List<Playlist> playlistItems = await setupService.GetSetupPlaylistsAsync(userId);

        return Ok(new StatusResponseDto<List<PlaylistDto>>
        {
            Status = "ok",
            Data = playlistItems.Select(p => new PlaylistDto(p)).ToList(),
        });
    }
    
    [HttpGet]
    [Route("screensaver")]
    public async Task<IActionResult> Screensaver()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view screensaver");

        ScreensaverDto result = await homeService.GetSetupScreensaverContent(User.UserId());

        return Ok(result);
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("/status")]
    [ResponseCache(Duration = 30)]
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