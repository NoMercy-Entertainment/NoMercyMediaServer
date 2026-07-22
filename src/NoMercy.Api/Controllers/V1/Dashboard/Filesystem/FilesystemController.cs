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
using Microsoft.Extensions.Logging;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.MediaProcessing.Files;
using NoMercy.NmSystem.Dto;

namespace NoMercy.Api.Controllers.V1.Dashboard.Filesystem;

[ApiController]
[Tags(tags: "Dashboard Filesystem")]
[ApiVersion(version: 1.0)]
[Authorize(Policy = "Moderator")]
[Route(template: "api/v{version:apiVersion}/dashboard/filesystem")]
public class FilesystemController(
    FilesystemRepository filesystem,
    ILogger<FilesystemController> logger
) : BaseController
{
    [HttpPost]
    [Route(template: "ls")]
    public IActionResult List([FromBody] DirectoryListRequest request)
    {
        try
        {
            (string? parent, List<DirectoryTree> entries) = filesystem.List(
                folder: request.Folder,
                withEmpty: request.WithEmpty
            );

            return Ok(
                value: new DirectoryTreeListing
                {
                    Status = "ok",
                    Path = string.IsNullOrEmpty(value: request.Folder) ? null : request.Folder,
                    Parent = parent,
                    Data = entries,
                }
            );
        }
        catch (Exception e)
        {
            logger.LogError(exception: e, message: "Filesystem request failed");
            return InternalServerErrorResponse(
                detail: "Something went wrong retrieving the directory tree"
            );
        }
    }

    [HttpPost]
    [Route(template: "home")]
    public IActionResult Home([FromBody] DirectoryListRequest? request)
    {
        bool withEmpty = request?.WithEmpty ?? false;

        try
        {
            (string path, string? parent, List<DirectoryTree> entries) = filesystem.Home(withEmpty: withEmpty);

            return Ok(
                value: new DirectoryTreeListing
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
            logger.LogError(exception: e, message: "Filesystem request failed");
            return InternalServerErrorResponse(
                detail: "Something went wrong retrieving the home directory"
            );
        }
    }

    [HttpPost]
    [Route(template: "roots")]
    public IActionResult Roots([FromBody] DirectoryListRequest? request)
    {
        bool withEmpty = request?.WithEmpty ?? false;

        try
        {
            List<DirectoryTree> entries = filesystem.Roots(withEmpty: withEmpty);

            return Ok(
                value: new DirectoryTreeListing
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
            logger.LogError(exception: e, message: "Filesystem request failed");
            return InternalServerErrorResponse(detail: "Something went wrong retrieving the drive list");
        }
    }

    [HttpPost]
    [Route(template: "mkdir")]
    public IActionResult Mkdir([FromBody] MkdirRequest request)
    {
        try
        {
            string path = filesystem.Mkdir(parent: request.Parent, name: request.Name);
            return Ok(value: new MkdirResponse { Status = "ok", Path = path });
        }
        catch (ArgumentException e)
        {
            return BadRequestResponse(detail: e.Message);
        }
        catch (DirectoryNotFoundException e)
        {
            return NotFoundResponse(detail: e.Message);
        }
        catch (UnauthorizedAccessException e)
        {
            return UnauthorizedResponse(detail: e.Message);
        }
        catch (Exception e)
        {
            logger.LogError(exception: e, message: "Filesystem request failed");
            return InternalServerErrorResponse(detail: "Something went wrong creating the folder");
        }
    }
}
