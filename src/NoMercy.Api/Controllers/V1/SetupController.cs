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
[Tags(tags: "App Setup")]
[ApiVersion(version: 1.0)]
[Authorize]
[Route(template: "api/v{version:apiVersion}/setup")]
public class SetupController(
    MediaContext context,
    AppDbContext appContext,
    SetupService setupService,
    HomeService homeService
) : BaseController
{
    [HttpGet(template: "libraries")]
    public async Task<IActionResult> Libraries()
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to view libraries");

        List<LibrariesResponseItemDto> response = (await setupService.GetSetupLibraries(userId: userId))
            .Select(selector: library => new LibrariesResponseItemDto(library: library))
            .ToList();

        return Ok(value: new LibrariesDto { Data = response.OrderBy(keySelector: library => library.Order) });
    }

    [HttpGet]
    [Route(template: "server-info")]
    [ResponseCache(NoStore = true, Duration = 0)]
    [Authorize(Policy = "MediaAccess")]
    public IActionResult ServerInfo()
    {
        bool setupComplete =
            context.Libraries.Any() && context.Folders.Any() && context.EncodingPresets.Any();

        Configuration? device = appContext.Configuration.FirstOrDefault(predicate: device =>
            device.Key == "serverName"
        );
        string serverName = device?.Value ?? Environment.MachineName;

        return Ok(
            value: new StatusResponseDto<ServerInfoDto>
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
    [Route(template: "permissions")]
    [Authorize(Policy = "MediaAccess")]
    public IActionResult Permissions()
    {
        return Ok(
            value: new
            {
                owner = AuthPolicy.IsOwner(principal: User),
                manager = AuthPolicy.IsModerator(principal: User),
                allowed = AuthPolicy.IsAllowed(principal: User),
            }
        );
    }

    [HttpGet(template: "music-playlists")]
    public async Task<IActionResult> Index()
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to view playlists");

        List<Playlist> playlistItems = await setupService.GetSetupPlaylistsAsync(userId: userId);

        return Ok(
            value: new StatusResponseDto<List<PlaylistDto>>
            {
                Status = "ok",
                Data = playlistItems.Select(selector: p => new PlaylistDto(playlist: p)).ToList(),
            }
        );
    }

    [HttpGet]
    [Route(template: "screensaver")]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> Screensaver()
    {
        ScreensaverDto result = await homeService.GetSetupScreensaverContent(userId: User.UserId());

        return Ok(value: result);
    }

    [HttpGet]
    [AllowAnonymous]
    [Route(template: "/status")]
    [ResponseCache(Duration = 30)]
    public IActionResult Status()
    {
        return Ok(
            value: new
            {
                Status = "ok",
                Version = "1.0",
                Message = "NoMercy MediaServer API is running",
                Timestamp = DateTime.UtcNow,
            }
        );
    }
}
