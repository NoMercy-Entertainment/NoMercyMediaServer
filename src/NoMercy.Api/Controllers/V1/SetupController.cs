using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1.Dashboard.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.Api.Controllers.V1.DTO;
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

        return Ok(new StatusResponseDto<List<Playlist>>
        {
            Status = "ok",
            Data = playlistItems,
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