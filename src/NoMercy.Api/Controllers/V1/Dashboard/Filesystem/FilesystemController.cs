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
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Authorization;
using NoMercy.MediaProcessing.Files;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Api.Controllers.V1.Dashboard.Filesystem;

[ApiController]
[Tags("Dashboard Filesystem")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/filesystem")]
public class FilesystemController(FilesystemRepository filesystem) : BaseController
{
    [HttpPost]
    [Route("ls")]
    public IActionResult List([FromBody] DirectoryListRequest request)
    {
        if (!AuthPolicy.IsModerator(User))
            return UnauthorizedResponse("You do not have permission to view folders");

        try
        {
            (string? parent, List<DirectoryTree> entries) = filesystem.List(
                request.Folder,
                request.WithEmpty
            );

            return Ok(
                new DirectoryTreeListing
                {
                    Status = "ok",
                    Path = string.IsNullOrEmpty(request.Folder) ? null : request.Folder,
                    Parent = parent,
                    Data = entries,
                }
            );
        }
        catch (Exception e)
        {
            Logger.App(e, LogEventLevel.Error);
            return InternalServerErrorResponse(
                "Something went wrong retrieving the directory tree"
            );
        }
    }

    [HttpPost]
    [Route("home")]
    public IActionResult Home([FromBody] DirectoryListRequest? request)
    {
        if (!AuthPolicy.IsModerator(User))
            return UnauthorizedResponse("You do not have permission to view folders");

        bool withEmpty = request?.WithEmpty ?? false;

        try
        {
            (string path, string? parent, List<DirectoryTree> entries) = filesystem.Home(withEmpty);

            return Ok(
                new DirectoryTreeListing
                {
                    Status = "ok",
                    Path = path,
                    Parent = parent,
                    Data = entries,
                }
            );
        }
        catch (Exception e)
        {
            Logger.App(e, LogEventLevel.Error);
            return InternalServerErrorResponse(
                "Something went wrong retrieving the home directory"
            );
        }
    }

    [HttpPost]
    [Route("roots")]
    public IActionResult Roots([FromBody] DirectoryListRequest? request)
    {
        if (!AuthPolicy.IsModerator(User))
            return UnauthorizedResponse("You do not have permission to view folders");

        bool withEmpty = request?.WithEmpty ?? false;

        try
        {
            List<DirectoryTree> entries = filesystem.Roots(withEmpty);

            return Ok(
                new DirectoryTreeListing
                {
                    Status = "ok",
                    Path = "roots",
                    Parent = null,
                    Data = entries,
                }
            );
        }
        catch (Exception e)
        {
            Logger.App(e, LogEventLevel.Error);
            return InternalServerErrorResponse("Something went wrong retrieving the drive list");
        }
    }

    [HttpPost]
    [Route("mkdir")]
    public IActionResult Mkdir([FromBody] MkdirRequest request)
    {
        if (!AuthPolicy.IsModerator(User))
            return UnauthorizedResponse("You do not have permission to create folders");

        try
        {
            string path = filesystem.Mkdir(request.Parent, request.Name);
            return Ok(new MkdirResponse { Status = "ok", Path = path });
        }
        catch (ArgumentException e)
        {
            return BadRequestResponse(e.Message);
        }
        catch (DirectoryNotFoundException e)
        {
            return NotFoundResponse(e.Message);
        }
        catch (UnauthorizedAccessException e)
        {
            return UnauthorizedResponse(e.Message);
        }
        catch (Exception e)
        {
            Logger.App(e, LogEventLevel.Error);
            return InternalServerErrorResponse("Something went wrong creating the folder");
        }
    }
}
