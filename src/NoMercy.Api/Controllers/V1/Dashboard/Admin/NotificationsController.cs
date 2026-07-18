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
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Database;
using NoMercy.Events;
using NoMercy.Events.Media;

namespace NoMercy.Api.Controllers.V1.Dashboard.Admin;

// -----------------------------------------------------------------------------
// POST /api/v1/dashboard/notifications/broadcast
// Server owner/moderator sends a one-off message to every user allowed on the
// server. Delivery reuses the existing UserNotifiedEvent -> SignalR "Notify"
// pipeline (SignalRNotificationEventHandler) that every other server-wide
// notice (e.g. RefreshLibrary) already relies on — no new table, no new hub
// method. Reach is therefore real-time only: users without a live connection
// at broadcast time do not receive it retroactively.
// -----------------------------------------------------------------------------

[ApiController]
[Tags("Dashboard Notifications")]
[ApiVersion(1.0)]
[Authorize(Policy = "Moderator")]
[Route("api/v{version:apiVersion}/dashboard/notifications", Order = 10)]
public class NotificationsController(MediaContext mediaContext, IEventBus eventBus) : BaseController
{
    private const string DefaultNotificationType = "info";

    [HttpPost]
    [Route("broadcast")]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastNotificationRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequestResponse("title is required.");

        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequestResponse("body is required.");

        string type = string.IsNullOrWhiteSpace(request.Type)
            ? DefaultNotificationType
            : request.Type;

        int notifiedUsers = await mediaContext
            .Users.AsNoTracking()
            .CountAsync(user => user.Allowed || user.Owner);

        await eventBus.PublishAsync(
            new UserNotifiedEvent
            {
                Title = request.Title,
                Message = request.Body,
                Type = type,
            }
        );

        return Ok(
            new DataResponseDto<BroadcastNotificationResponseDto>
            {
                Data = new()
                {
                    NotifiedUsers = notifiedUsers,
                    Title = request.Title,
                    Body = request.Body,
                    Type = type,
                },
            }
        );
    }
}
