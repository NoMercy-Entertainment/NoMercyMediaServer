using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Music;
using NoMercy.Networking;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Tasks")]
[ApiVersion("1")]
[Authorize]
[Route("api/v{Version:apiVersion}/dashboard/tasks", Order = 10)]
public class TasksController : BaseController
{
    [HttpGet]
    public IActionResult Index()
    {
        if (!HttpContext.User.IsModerator())
            return Unauthorized(new StatusResponseDto<string>
            {
                Status = "error",
                Message = "You do not have permission to view tasks"
            });

        List<TaskDto> list =
        [
            new TaskDto
            {
                Id = "pqiilkpnf8lmwrcxn0l8tngf",
                Title = "Scan media library",
                Value = 0,
                Type = "library",
                CreatedAt = DateTime.Parse("2024-01-25 09:26:56"),
                UpdatedAt = DateTime.Parse("2024-01-25 09:26:56")
            }
        ];

        return Ok(list);
    }

    [HttpPost]
    public IActionResult Store()
    {
        if (!HttpContext.User.IsModerator())
            return UnauthorizedResponse("You do not have permission to create tasks");

        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }

    [HttpPatch]
    public IActionResult Update()
    {
        if (!HttpContext.User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update tasks");

        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }

    [HttpDelete]
    public IActionResult Destroy()
    {
        if (!HttpContext.User.IsModerator())
            return UnauthorizedResponse("You do not have permission to delete tasks");

        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }

    [HttpPost]
    [Route("pause")]
    public IActionResult PauseTask()
    {
        if (!HttpContext.User.IsModerator())
            return UnauthorizedResponse("You do not have permission to pause tasks");

        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }

    [HttpPost]
    [Route("resume")]
    public IActionResult ResumeTask()
    {
        if (!HttpContext.User.IsModerator())
            return UnauthorizedResponse("You do not have permission to resume tasks");

        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }

    [HttpGet]
    [Route("runners")]
    public IActionResult RunningTaskWorkers()
    {
        if (!HttpContext.User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view task workers");

        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }

    [HttpGet]
    [Route("queue")]
    public IActionResult EncoderQueue()
    {
        if (!HttpContext.User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view encoder queue");

        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }
}

public class TaskDto
{
    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("title")] public string Title { get; set; }
    [JsonProperty("value")] public int Value { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("created_at")] public DateTime CreatedAt { get; set; }
    [JsonProperty("updated_at")] public DateTime UpdatedAt { get; set; }
}