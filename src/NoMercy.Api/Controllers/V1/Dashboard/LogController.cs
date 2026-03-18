using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.DTOs.Common;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Libraries")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/logs", Order = 10)]
public class LogController : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int limit = 50,
        [FromQuery] string[]? types = null,
        [FromQuery] string[]? levels = null,
        [FromQuery] string? filter = null)
    {
        List<LogEntry> logs = await Logger.GetLogs(limit, entry =>
        {
            bool typeMatch = types == null || types.Length == 0 ||
                             types.Any(t => string.Equals(t, entry.Type, StringComparison.OrdinalIgnoreCase));
            bool levelMatch = levels == null || levels.Length == 0 ||
                              levels.Contains(entry.Level.ToString(), StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(filter))
            {
                return typeMatch && levelMatch &&
                       entry.Message.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
            }

            return typeMatch && levelMatch;
        });

        return Ok(new DataResponseDto<List<LogEntry>>
        {
            Data = logs
        });
    }

    [HttpGet]
    [Route("levels")]
    public IActionResult GetLogLevels()
    {
        return Ok(new DataResponseDto<string[]>
        {
            Data =
            [
                Enum.Parse<LogEventLevel>(nameof(LogEventLevel.Verbose)).ToString(),
                Enum.Parse<LogEventLevel>(nameof(LogEventLevel.Debug)).ToString(),
                Enum.Parse<LogEventLevel>(nameof(LogEventLevel.Information)).ToString(),
                Enum.Parse<LogEventLevel>(nameof(LogEventLevel.Error)).ToString(),
                Enum.Parse<LogEventLevel>(nameof(LogEventLevel.Fatal)).ToString()
            ]
        });
    }

    [HttpGet]
    [Route("types")]
    public IActionResult GetLogTypes()
    {
        return Ok(new DataResponseDto<IEnumerable<Logger.LogType>>
        {
            Data = Logger.LogTypes.Values
        });
    }
}