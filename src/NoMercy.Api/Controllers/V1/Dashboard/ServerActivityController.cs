using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Controllers.V1.Music;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers.Extensions;

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

        int take = request.Take ?? 50;
        int skip = request.Skip ?? 0;

        IQueryable<ActivityLog> query = mediaContext.ActivityLogs.AsQueryable();

        if (request.Category is { } category)
            query = query.Where(x => x.Category == category);
        if (request.UserId is { } userId)
            query = query.Where(x => x.UserId == userId);
        if (request.DeviceId is { } deviceId)
            query = query.Where(x => x.DeviceId == deviceId);
        if (request.MediaId is { } mediaId)
            query = query.Where(x => x.MediaId == mediaId);
        if (request.From is { } from)
            query = query.Where(x => x.CreatedAt >= from);
        if (request.To is { } to)
            query = query.Where(x => x.CreatedAt <= to);
        if (request.Success is { } success)
            query = query.Where(x => x.Success == success);

        ServerActivityDto[] activityDtos = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(x => new ServerActivityDto
            {
                Id = x.Id,
                Category = x.Category,
                Type = x.Type,
                Time = x.Time,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                UserId = x.UserId,
                DeviceId = x.DeviceId,
                MediaId = x.MediaId,
                Success = x.Success,
                ErrorCode = x.ErrorCode,
                Metadata = x.Metadata,
                Device = x.Device.Name,
                User = x.User.Name,
            })
            .ToArrayAsync();

        return Ok(
            new StatusResponseDto<ServerActivityDto[]> { Status = "ok", Data = activityDtos }
        );
    }

    [HttpPost]
    public IActionResult Create()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to create activity");

        return Ok(new PlaceholderResponse { Data = [] });
    }

    [HttpDelete]
    public async Task<IActionResult> Destroy(
        [FromQuery] ActivityCategory? category,
        [FromQuery] DateTime? before
    )
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to delete activity");

        IQueryable<ActivityLog> query = mediaContext.ActivityLogs.AsQueryable();
        if (category is { } cat)
            query = query.Where(x => x.Category == cat);
        if (before is { } cutoff)
            query = query.Where(x => x.CreatedAt < cutoff);

        int deleted = await query.ExecuteDeleteAsync();
        return Ok(new StatusResponseDto<object> { Status = "ok", Data = new { deleted } });
    }
}
