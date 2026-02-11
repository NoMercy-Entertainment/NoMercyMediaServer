using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Query;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.Controllers.V1.Music;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers;
using NoMercy.Helpers.Extensions;


namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Server Devices")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/devices", Order = 10)]
public class DevicesController : BaseController
{
    private readonly DeviceRepository _deviceRepository;

    public DevicesController(DeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    [HttpGet]
    public Task<IActionResult> Index()
    {
        if (!User.IsModerator())
            return Task.FromResult(UnauthorizedResponse("You do not have permission to view devices"));

        IIncludableQueryable<Device, ICollection<ActivityLog>> devices = _deviceRepository.GetDevices();

        DevicesDto[] devicesDtos = devices
            .Select(x => new DevicesDto
            {
                Id = x.Id.ToString(),
                DeviceId = x.DeviceId,
                Browser = x.Browser,
                Os = x.Os,
                Device = x.Model,
                Type = x.Type,
                Name = x.Name,
                CustomName = x.CustomName,
                Version = x.Version,
                Ip = x.Ip,
                CreatedAt = x.CreatedAt,
                ActivityLogs = x.ActivityLogs
                    .Select(activityLog => new ActivityLogDto
                    {
                        Id = activityLog.Id,
                        Type = activityLog.Type,
                        Time = activityLog.Time,
                        CreatedAt = activityLog.CreatedAt,
                        UserId = activityLog.UserId,
                        DeviceId = activityLog.DeviceId.ToString()
                    })
            })
            .ToArray();


        return Task.FromResult<IActionResult>(Ok(new StatusResponseDto<DevicesDto[]>
        {
            Status = "ok",
            Data = devicesDtos,
        }));
    }

    [HttpPost]
    public IActionResult Create()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to create devices");

        // Add device creation logic here
        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }

    [HttpDelete]
    public IActionResult Destroy()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to delete devices");

        // Add device deletion logic here
        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }
}