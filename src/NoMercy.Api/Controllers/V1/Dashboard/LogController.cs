using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1.Dashboard.DTO;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.NmSystem;
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
    public async Task<IActionResult> GetLogs([FromQuery] GetLogsRequestDto request)
    {
        List<LogEntry> logs = await Logger.GetLogs(request.Limit, entry =>
        {
            bool typeMatch = request.Types == null || request.Types.Length == 0 ||
                             request.Types.Any(t => string.Equals(t, entry.Type, StringComparison.OrdinalIgnoreCase));
            bool levelMatch = request.Levels == null || request.Levels.Length == 0 ||
                              request.Levels.Contains(entry.Level.ToString(), StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(request.Filter))
            {
                string filter = request.Filter.ToLower();
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
                Enum.Parse<LogEventLevel>(LogEventLevel.Verbose.ToString()).ToString(),
                Enum.Parse<LogEventLevel>(LogEventLevel.Debug.ToString()).ToString(),
                Enum.Parse<LogEventLevel>(LogEventLevel.Information.ToString()).ToString(),
                Enum.Parse<LogEventLevel>(LogEventLevel.Error.ToString()).ToString(),
                Enum.Parse<LogEventLevel>(LogEventLevel.Fatal.ToString()).ToString()
            ]
        });
    }

    [HttpGet]
    [Route("types")]
    public IActionResult GetLogTypes()
    {
        return Ok(new DataResponseDto<IEnumerable<Logger.LogType>>
        {
            Data = Logger.LogTypes
        });
    }
}