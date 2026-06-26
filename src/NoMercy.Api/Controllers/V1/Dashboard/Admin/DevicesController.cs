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
using NoMercy.Api.Controllers.V1.Music;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Api.WebSockets;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers.Extensions;
using NoMercy.Networking.Http;
using NoMercy.Networking.Messaging;

namespace NoMercy.Api.Controllers.V1.Dashboard.Admin;

[ApiController]
[Tags("Dashboard Server Devices")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/devices", Order = 10)]
public class DevicesController(
    IDeviceRepository deviceRepository,
    DeviceBusRegistry busRegistry,
    ConnectedClients connectedClients
) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view devices");

        List<Device> devices = await deviceRepository.GetDevices().ToListAsync();

        DevicesDto[] devicesDtos = devices
            .Select(x => new DevicesDto
            {
                Id = x.Id.ToString(),
                DeviceId = x.DeviceId,
                Browser = x.Browser,
                Os = x.Os,
                Device = x.Model,
                Type = x.Type,
                Online =
                    busRegistry.IsOnline(x.Id)
                    || connectedClients.Clients.Values.Any(client => client.Id == x.Id),
                Name = x.Name,
                CustomName = x.CustomName,
                Version = x.Version,
                Ip = x.Ip,
                CreatedAt = x.CreatedAt,
                ActivityLogs = x.ActivityLogs.Select(activityLog => new ActivityLogDto
                {
                    Id = activityLog.Id,
                    Type = activityLog.Type,
                    Time = activityLog.Time,
                    CreatedAt = activityLog.CreatedAt,
                    UserId = activityLog.UserId,
                    DeviceId = activityLog.DeviceId.ToString(),
                }),
            })
            .ToArray();

        return Ok(new StatusResponseDto<DevicesDto[]> { Status = "ok", Data = devicesDtos });
    }

    [HttpPost]
    public IActionResult Create()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to create devices");

        return Ok(new PlaceholderResponse { Data = [] });
    }

    [HttpDelete]
    public async Task<IActionResult> Destroy()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to clear activity logs");

        await deviceRepository.DeleteAllActivityLogsAsync();

        return Ok(new StatusResponseDto<object> { Status = "ok", Data = new { } });
    }

    [HttpDelete("offline")]
    public async Task<IActionResult> DestroyOffline()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to delete devices");

        List<Device> all = await deviceRepository.GetAllAsync();

        List<Device> offline = all.Where(d =>
                !busRegistry.IsOnline(d.Id)
                && !connectedClients.Clients.Values.Any(c => c.Id == d.Id)
            )
            .ToList();

        foreach (Device device in offline)
        {
            busRegistry.ForceClose(device.Id);
            RemoveConnectedClientEntries(device.Id);
            await deviceRepository.DeleteDeviceWithLogsAsync(device.Id);
        }

        foreach (
            Guid ownerId in offline
                .Where(d => d.OwnerUserId is not null)
                .Select(d => d.OwnerUserId!.Value)
                .Distinct()
        )
            await busRegistry.BroadcastChange(ownerId);

        List<Device> remaining = await deviceRepository.GetAllAsync();

        return Ok(
            new StatusResponseDto<object>
            {
                Status = "ok",
                Data = new { removed = offline.Count, devices = remaining },
            }
        );
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DestroyOne(string id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to delete devices");

        if (!Ulid.TryParse(id, out Ulid deviceId))
            return BadRequestResponse("Invalid device id");

        Device? device = await deviceRepository.GetByIdAsync(deviceId);
        if (device is null)
            return NotFoundResponse("Device not found");

        busRegistry.ForceClose(device.Id);
        RemoveConnectedClientEntries(device.Id);

        await deviceRepository.DeleteDeviceWithLogsAsync(device.Id);

        if (device.OwnerUserId is not null)
            await busRegistry.BroadcastChange(device.OwnerUserId.Value);

        List<Device> remaining = await deviceRepository.GetAllAsync();

        return Ok(new StatusResponseDto<object> { Status = "ok", Data = remaining });
    }

    private void RemoveConnectedClientEntries(Ulid deviceId)
    {
        List<string> staleKeys = connectedClients
            .Clients.Where(pair => pair.Value.Id == deviceId)
            .Select(pair => pair.Key)
            .ToList();

        foreach (string key in staleKeys)
            connectedClients.Clients.TryRemove(key, out Client? _);
    }
}
