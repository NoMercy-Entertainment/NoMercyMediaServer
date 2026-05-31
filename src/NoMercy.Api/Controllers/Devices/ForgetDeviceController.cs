using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.WebSockets;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers.Extensions;

namespace NoMercy.Api.Controllers.Devices;

[ApiController]
[Authorize]
[Route("api/devices/{deviceId}/forget")]
public sealed class ForgetDeviceController : ControllerBase
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
        User? user = HttpContext.User.User();
        if (user is null)
            return Unauthorized();
        if (!Ulid.TryParse(deviceId, out Ulid id))
            return BadRequest();

        await using MediaContext ctx = await _contextFactory.CreateDbContextAsync();
        Device? device = await ctx.Devices.FindAsync(id);
        if (device is null || device.OwnerUserId != user.Id)
            return NotFound();

        Guid ownerUserId = device.OwnerUserId!.Value;

        ctx.Devices.Remove(device);
        await ctx.SaveChangesAsync();

        // Force-close the WS if still alive (best-effort); device is already gone from DB.
        _registry.ForceClose(id);

        // Broadcast updated device list to all the user's other clients.
        await _registry.BroadcastChange(ownerUserId);

        return NoContent();
    }
}
