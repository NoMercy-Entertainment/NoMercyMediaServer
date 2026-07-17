// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.Services;
using NoMercy.Authorization;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Music;
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
    AppDbContext appContext,
    SetupService setupService,
    HomeService homeService
) : BaseController
{
    [HttpGet("libraries")]
    public async Task<IActionResult> Libraries()
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(User))
            return UnauthorizedResponse("You do not have permission to view libraries");

        List<LibrariesResponseItemDto> response = (await setupService.GetSetupLibraries(userId))
            .Select(library => new LibrariesResponseItemDto(library))
            .ToList();

        return Ok(new LibrariesDto { Data = response.OrderBy(library => library.Order) });
    }

    [HttpGet]
    [Route("server-info")]
    [ResponseCache(NoStore = true, Duration = 0)]
    [Authorize(Policy = "MediaAccess")]
    public IActionResult ServerInfo()
    {
        bool setupComplete =
            context.Libraries.Any() && context.Folders.Any() && context.EncodingPresets.Any();

        Configuration? device = appContext.Configuration.FirstOrDefault(device =>
            device.Key == "serverName"
        );
        string serverName = device?.Value ?? Environment.MachineName;

        return Ok(
            new StatusResponseDto<ServerInfoDto>
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
                    SetupComplete = setupComplete,
                },
            }
        );
    }

    [HttpGet]
    [Route("permissions")]
    [Authorize(Policy = "MediaAccess")]
    public IActionResult Permissions()
    {
        return Ok(
            new
            {
                owner = AuthPolicy.IsOwner(User),
                manager = AuthPolicy.IsModerator(User),
                allowed = AuthPolicy.IsAllowed(User),
            }
        );
    }

    [HttpGet("music-playlists")]
    public async Task<IActionResult> Index()
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(User))
            return UnauthorizedResponse("You do not have permission to view playlists");

        List<Playlist> playlistItems = await setupService.GetSetupPlaylistsAsync(userId);

        return Ok(
            new StatusResponseDto<List<PlaylistDto>>
            {
                Status = "ok",
                Data = playlistItems.Select(p => new PlaylistDto(p)).ToList(),
            }
        );
    }

    [HttpGet]
    [Route("screensaver")]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> Screensaver()
    {
        ScreensaverDto result = await homeService.GetSetupScreensaverContent(User.UserId());

        return Ok(result);
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("/status")]
    [ResponseCache(Duration = 30)]
    public IActionResult Status()
    {
        return Ok(
            new
            {
                Status = "ok",
                Version = "1.0",
                Message = "NoMercy MediaServer API is running",
                Timestamp = DateTime.UtcNow,
            }
        );
    }
}
