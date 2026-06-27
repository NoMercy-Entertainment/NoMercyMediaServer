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
using NoMercy.Api.Controllers.V1.Music;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers.Extensions;
using NoMercy.Authorization;

namespace NoMercy.Api.Controllers.V1.Dashboard.Admin;

[ApiController]
[Tags("Dashboard Server Activity")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/activity", Order = 10)]
public class ServerActivityController(IActivityRepository activityRepository) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ServerActivityRequest request)
    {
        if (!AuthPolicy.IsModerator(User))
            return UnauthorizedResponse("You do not have permission to view activity");

        int take = request.Take ?? 50;
        int skip = request.Skip ?? 0;

        List<ActivityLog> activityLogs = await activityRepository.GetPagedAsync(
            request.Category,
            request.UserId,
            request.DeviceId,
            request.MediaId,
            request.From,
            request.To,
            request.Success,
            skip,
            take
        );

        ServerActivityDto[] activityDtos = activityLogs
            .Select(activityLog => new ServerActivityDto
            {
                Id = activityLog.Id,
                Category = activityLog.Category,
                Type = activityLog.Type,
                Time = activityLog.Time,
                CreatedAt = activityLog.CreatedAt,
                UpdatedAt = activityLog.UpdatedAt,
                UserId = activityLog.UserId,
                DeviceId = activityLog.DeviceId,
                MediaId = activityLog.MediaId,
                Success = activityLog.Success,
                ErrorCode = activityLog.ErrorCode,
                Metadata = activityLog.Metadata,
                Device = activityLog.Device.Name,
                User = activityLog.User.Name,
            })
            .ToArray();

        return Ok(
            new StatusResponseDto<ServerActivityDto[]> { Status = "ok", Data = activityDtos }
        );
    }

    [HttpPost]
    public IActionResult Create()
    {
        if (!AuthPolicy.IsAllowed(User))
            return UnauthorizedResponse("You do not have permission to create activity");

        return Ok(new PlaceholderResponse { Data = [] });
    }

    [HttpDelete]
    public async Task<IActionResult> Destroy(
        [FromQuery] ActivityCategory? category,
        [FromQuery] DateTime? before
    )
    {
        if (!AuthPolicy.IsModerator(User))
            return UnauthorizedResponse("You do not have permission to delete activity");

        int deleted = await activityRepository.DeleteAsync(category, before);
        return Ok(new StatusResponseDto<object> { Status = "ok", Data = new { deleted } });
    }
}
