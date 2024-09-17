#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1.Music;
using NoMercy.Database;
using NoMercy.Networking;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Server Activity")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/activity", Order = 10)]
public class ServerActivityController : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ServerActivityRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view activity");

        await using MediaContext mediaContext = new();
        ServerActivityDto[] activityDtos = mediaContext.ActivityLogs
            .OrderByDescending(x => x.CreatedAt)
            .Take((request.Take ?? 10) + 1)
            .Select(x => new ServerActivityDto
            {
                Id = x.Id,
                Type = x.Type,
                Time = x.Time,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                UserId = x.UserId,
                DeviceId = x.DeviceId,
                Device = x.Device.Name,
                User = x.User.Name
            })
            .ToArray();

        return Ok(activityDtos);
    }

    [HttpPost]
    public IActionResult Create()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to create activity");

        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }

    [HttpDelete]
    public IActionResult Destroy()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to delete activity");

        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }
}