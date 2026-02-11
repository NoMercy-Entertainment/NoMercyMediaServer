using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.Controllers.V1.Music;
using NoMercy.Database;
using NoMercy.Helpers;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Server Activity")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/activity", Order = 10)]
public class ServerActivityController(MediaContext mediaContext) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ServerActivityRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view activity");

        ServerActivityDto[] activityDtos = mediaContext.ActivityLogs
            .OrderByDescending(x => x.CreatedAt)
            .Take((request.Take ?? 10) + 1)
            .Select(x => new ServerActivityDto
            {
                Id = x.Id,
                Type = x.Type,
                Time = x.Time,
                CreatedAt = x.CreatedAt,
                UserId = x.UserId,
                DeviceId = x.DeviceId,
                Device = x.Device.Name,
                User = x.User.Name
            })
            .ToArray();

        return Ok(new StatusResponseDto<ServerActivityDto[]>
        {
            Status = "ok",
            Data = activityDtos,
        });
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