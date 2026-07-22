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
using NoMercy.Api.WebSockets;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Users;
using NoMercy.Networking.Http;
using NoMercy.Networking.Messaging;

namespace NoMercy.Api.Controllers.V1.Dashboard.Admin;

[ApiController]
[Tags(tags: "Dashboard Server Devices")]
[ApiVersion(version: 1.0)]
[Authorize(Policy = "Moderator")]
[Route(template: "api/v{version:apiVersion}/dashboard/devices", Order = 10)]
public class DevicesController(
    IDeviceRepository deviceRepository,
    DeviceBusRegistry busRegistry,
    ConnectedClients connectedClients
) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {

        List<Device> devices = await deviceRepository.GetDevices();

        DevicesDto[] devicesDtos = devices
            .Select(selector: x => new DevicesDto
            {
                Id = x.Id.ToString(),
                DeviceId = x.DeviceId,
                Browser = x.Browser,
                Os = x.Os,
                Device = x.Model,
                Type = x.Type,
                Online =
                    busRegistry.IsOnline(deviceId: x.Id)
                    || connectedClients.Clients.Values.Any(predicate: client => client.Id == x.Id),
                Name = x.Name,
                CustomName = x.CustomName,
                Version = x.Version,
                Ip = x.Ip,
                CreatedAt = x.CreatedAt,
                ActivityLogs = x.ActivityLogs.Select(selector: activityLog => new ActivityLogDto
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

        return Ok(value: new StatusResponseDto<DevicesDto[]> { Status = "ok", Data = devicesDtos });
    }

    [HttpPost]
    public IActionResult Create()
    {

        return Ok(value: new PlaceholderResponse { Data = [] });
    }

    [HttpDelete]
    public async Task<IActionResult> Destroy()
    {

        await deviceRepository.DeleteAllActivityLogsAsync();

        return Ok(value: new StatusResponseDto<object> { Status = "ok", Data = new { } });
    }

    [HttpDelete(template: "offline")]
    public async Task<IActionResult> DestroyOffline()
    {

        List<Device> all = await deviceRepository.GetAllAsync();

        List<Device> offline = all.Where(predicate: d =>
                !busRegistry.IsOnline(deviceId: d.Id)
                && !connectedClients.Clients.Values.Any(predicate: c => c.Id == d.Id)
            )
            .ToList();

        foreach (Device device in offline)
        {
            busRegistry.ForceClose(deviceId: device.Id);
            RemoveConnectedClientEntries(deviceId: device.Id);
            await deviceRepository.DeleteDeviceWithLogsAsync(deviceId: device.Id);
        }

        foreach (
            Guid ownerId in offline
                .Where(predicate: d => d.OwnerUserId is not null)
                .Select(selector: d => d.OwnerUserId!.Value)
                .Distinct()
        )
            await busRegistry.BroadcastChange(ownerUserId: ownerId);

        List<Device> remaining = await deviceRepository.GetAllAsync();

        return Ok(
            value: new StatusResponseDto<object>
            {
                Status = "ok",
                Data = new { removed = offline.Count, devices = remaining },
            }
        );
    }

    [HttpDelete(template: "{id}")]
    public async Task<IActionResult> DestroyOne(string id)
    {

        if (!Ulid.TryParse(base32: id, ulid: out Ulid deviceId))
            return BadRequestResponse(detail: "Invalid device id");

        Device? device = await deviceRepository.GetByIdAsync(deviceId: deviceId);
        if (device is null)
            return NotFoundResponse(detail: "Device not found");

        busRegistry.ForceClose(deviceId: device.Id);
        RemoveConnectedClientEntries(deviceId: device.Id);

        await deviceRepository.DeleteDeviceWithLogsAsync(deviceId: device.Id);

        if (device.OwnerUserId is not null)
            await busRegistry.BroadcastChange(ownerUserId: device.OwnerUserId.Value);

        List<Device> remaining = await deviceRepository.GetAllAsync();

        return Ok(value: new StatusResponseDto<object> { Status = "ok", Data = remaining });
    }

    private void RemoveConnectedClientEntries(Ulid deviceId)
    {
        List<string> staleKeys = connectedClients
            .Clients.Where(predicate: pair => pair.Value.Id == deviceId)
            .Select(selector: pair => pair.Key)
            .ToList();

        foreach (string key in staleKeys)
            connectedClients.Clients.TryRemove(key: key, value: out Client? _);
    }
}
