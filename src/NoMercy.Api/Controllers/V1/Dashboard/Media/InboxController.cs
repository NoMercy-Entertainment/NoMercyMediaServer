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

/*
 * This file is part of the NoMercy Entertainment application.
 * Copyright (c) NoMercy Entertainment. All rights reserved.
 * Licensed under the MIT License.
 */

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Libraries;
using NoMercy.Events;
using NoMercy.Events.Inbox;
using NoMercy.MediaProcessing.Inbox;

namespace NoMercy.Api.Controllers.V1.Dashboard.Media;

[ApiController]
[Tags(tags: "Dashboard Inbox")]
[ApiVersion(version: 1.0)]
[Authorize(Policy = "Moderator")]
[Route(template: "api/v{version:apiVersion}/dashboard/inbox", Order = 10)]
public class InboxController(IInboxRepository inboxRepository, IInboxMetadataProbe metadataProbe)
    : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? status)
    {

        List<InboxItem> items = await inboxRepository.GetAllAsync(
            status: status,
            ct: HttpContext.RequestAborted
        );

        return Ok(value: new { Data = items.Select(selector: item => new InboxItemDto(item: item)) });
    }

    [HttpGet(template: "{id:ulid}")]
    public async Task<IActionResult> Show(Ulid id)
    {

        InboxItem? item = await inboxRepository.GetByIdAsync(id: id, ct: HttpContext.RequestAborted);

        if (item is null)
            return NotFoundResponse(detail: "Inbox item not found");

        return Ok(value: new InboxItemDto(item: item));
    }

    [HttpGet(template: "{id:ulid}/matches")]
    public async Task<IActionResult> Matches(
        Ulid id,
        [FromQuery] string type,
        [FromQuery] string query
    )
    {

        if (string.IsNullOrWhiteSpace(value: type))
            return BadRequestResponse(detail: "type is required");

        if (string.IsNullOrWhiteSpace(value: query))
            return BadRequestResponse(detail: "query is required");

        CandidateMatch[] candidates;

        switch (type)
        {
            case "movie":
                candidates = await metadataProbe.SearchMoviesAsync(
                    title: query,
                    year: null,
                    ct: HttpContext.RequestAborted
                );
                break;

            case "tv":
            case "anime":
                candidates = await metadataProbe.SearchTvAsync(
                    title: query,
                    year: null,
                    ct: HttpContext.RequestAborted
                );
                break;

            case "music":
                candidates = [];
                break;

            default:
                return BadRequestResponse(
                    detail: $"Unsupported type '{type}'. Expected: movie, tv, anime, music"
                );
        }

        return Ok(value: new { Data = candidates });
    }

    [HttpPost(template: "{id:ulid}/assign")]
    public async Task<IActionResult> Assign(Ulid id, [FromBody] InboxAssignRequest request)
    {

        InboxItem? item = await inboxRepository.GetTrackedByIdAsync(id: id, ct: HttpContext.RequestAborted);

        if (item is null)
            return NotFoundResponse(detail: "Inbox item not found");

        Folder? folder = await inboxRepository.GetFolderByIdAsync(
            folderId: request.TargetFolderId,
            ct: HttpContext.RequestAborted
        );

        if (folder is null)
            return NotFoundResponse(detail: "Target folder not found");

        InboxDestination destination = new()
        {
            LibraryId = request.TargetLibraryId,
            FolderId = request.TargetFolderId,
            ProfileId = request.TargetProfileId,
            DriverId = folder.DriverId,
            FolderPath = folder.Path,
        };

        item.DetectedType = request.Type;
        item.TargetLibraryId = request.TargetLibraryId;
        item.TargetFolderId = request.TargetFolderId;
        item.TargetProfileId = request.TargetProfileId;

        try
        {
            await inboxRepository.ExecuteAssignmentAsync(
                item: item,
                match: request.Match,
                destination: destination,
                ct: HttpContext.RequestAborted
            );
        }
        catch (Exception ex)
        {
            return InternalServerErrorResponse(
                detail: $"Failed to assign inbox item: {ex.GetType().Name}: {ex.Message}"
            );
        }

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                @event: new InboxItemUpdatedEvent { Id = item.Id.ToString(), Status = item.Status }
            );
        }

        return Ok(
            value: new StatusResponseDto<InboxItemDto>
            {
                Status = "ok",
                Message = "Successfully assigned inbox item.",
                Data = new(item: item),
            }
        );
    }

    [HttpPost(template: "{id:ulid}/dismiss")]
    public async Task<IActionResult> Dismiss(Ulid id)
    {

        InboxItem? item = await inboxRepository.GetTrackedByIdAsync(id: id, ct: HttpContext.RequestAborted);

        if (item is null)
            return NotFoundResponse(detail: "Inbox item not found");

        await inboxRepository.DismissAsync(item: item, ct: HttpContext.RequestAborted);

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                @event: new InboxItemUpdatedEvent { Id = item.Id.ToString(), Status = item.Status }
            );
        }

        return Ok(
            value: new StatusResponseDto<string> { Status = "ok", Message = "Inbox item dismissed." }
        );
    }

    [HttpDelete(template: "{id:ulid}")]
    public async Task<IActionResult> Delete(Ulid id)
    {

        InboxItem? item = await inboxRepository.GetTrackedByIdAsync(id: id, ct: HttpContext.RequestAborted);

        if (item is null)
            return NotFoundResponse(detail: "Inbox item not found");

        await inboxRepository.DeleteAsync(item: item, ct: HttpContext.RequestAborted);

        return Ok(value: new StatusResponseDto<string> { Status = "ok", Message = "Inbox item removed." });
    }
}
