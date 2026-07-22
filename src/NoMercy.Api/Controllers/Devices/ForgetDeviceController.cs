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
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Api.WebSockets;
using NoMercy.Authorization;
using NoMercy.Database;
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.Controllers.Devices;

[ApiController]
[Authorize]
[ApiVersion(version: 1.0)]
[Route(template: "api/v{version:apiVersion}/devices/{deviceId}/forget")]
public sealed class ForgetDeviceController : BaseController
{
    private readonly IDbContextFactory<MediaContext> _contextFactory;
    private readonly DeviceBusRegistry _registry;

    public ForgetDeviceController(
        IDbContextFactory<MediaContext> contextFactory,
        DeviceBusRegistry registry
    )
    {
        _contextFactory = contextFactory;
        _registry = registry;
    }

    [HttpPost]
    public async Task<IActionResult> Forget(string deviceId)
    {
        User? user = HttpContext
            .RequestServices.GetRequiredService<IUserCache>()
            .GetUser(userId: HttpContext.User.UserId());
        if (user is null)
            return UnauthenticatedResponse(detail: "Authentication required.");
        if (!Ulid.TryParse(base32: deviceId, ulid: out Ulid id))
            return BadRequestResponse(detail: "Invalid device id.");

        await using MediaContext ctx = await _contextFactory.CreateDbContextAsync();
        Device? device = await ctx.Devices.FindAsync(keyValues: id);
        if (device is null || device.OwnerUserId != user.Id)
            return NotFoundResponse(detail: "Device not found.");

        Guid ownerUserId = device.OwnerUserId!.Value;

        ctx.Devices.Remove(entity: device);
        await ctx.SaveChangesAsync();

        // Force-close the WS if still alive (best-effort); device is already gone from DB.
        _registry.ForceClose(deviceId: id);

        // Broadcast updated device list to all the user's other clients.
        await _registry.BroadcastChange(ownerUserId: ownerUserId);

        return NoContent();
    }
}
