using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.NmSystem.Information;
using NoMercy.Setup;

namespace NoMercy.Api.Controllers;

/// <summary>
/// Health check endpoint for container orchestration and load balancers
/// </summary>
[ApiController]
[Route("[controller]")]
[AllowAnonymous]
public class HealthController(MediaContext mediaContext) : ControllerBase
{
    /// <summary>
    /// Basic liveness probe — returns 200 if the server process is running
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public IActionResult GetLiveness()
    {
        return Ok(new HealthResponse
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Readiness probe — returns 200 when the server can handle requests, 503 if not ready
    /// </summary>
    [HttpGet("ready")]
    [ProducesResponseType(typeof(ReadinessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ReadinessResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetReadiness()
    {
        bool databaseHealthy = await CheckDatabase();

        bool isReady = Config.Started && databaseHealthy;
        string status = isReady ? "ready" : "not_ready";

        ReadinessResponse response = new()
        {
            Status = status,
            Timestamp = DateTime.UtcNow,
            Database = databaseHealthy ? "ok" : "unavailable",
            ServerStarted = Config.Started
        };

        return isReady
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }

    /// <summary>
    /// Detailed health check with component status and degraded mode info
    /// </summary>
    [HttpGet("detailed")]
    [ProducesResponseType(typeof(DetailedHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DetailedHealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetDetailed()
    {
        bool databaseHealthy = await CheckDatabase();
        bool isDegraded = Start.IsDegradedMode;

        string status = DetermineStatus(databaseHealthy, isDegraded);

        DetailedHealthResponse response = new()
        {
            Status = status,
            Timestamp = DateTime.UtcNow,
            Version = Software.GetReleaseVersion(),
            Environment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            UptimeSeconds = (long)(DateTime.UtcNow - Info.StartTime).TotalSeconds,
            Components = new ComponentStatus
            {
                Database = databaseHealthy ? "ok" : "unavailable",
                Authentication = isDegraded ? "degraded" : "ok",
                Network = isDegraded ? "degraded" : "ok",
                Registration = isDegraded ? "degraded" : "ok"
            },
            IsDegraded = isDegraded
        };

        return databaseHealthy
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }

    private static string DetermineStatus(bool databaseHealthy, bool isDegraded)
    {
        if (!Config.Started) return "starting";
        if (!databaseHealthy) return "unhealthy";
        if (isDegraded) return "degraded";
        return "healthy";
    }

    private async Task<bool> CheckDatabase()
    {
        try
        {
            await mediaContext.Database.ExecuteSqlRawAsync("SELECT 1");
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public record HealthResponse
{
    [JsonProperty("status")] public required string Status { get; init; }
    [JsonProperty("timestamp")] public required DateTime Timestamp { get; init; }
}

public record ReadinessResponse : HealthResponse
{
    [JsonProperty("database")] public required string Database { get; init; }
    [JsonProperty("server_started")] public required bool ServerStarted { get; init; }
}

public record DetailedHealthResponse : HealthResponse
{
    [JsonProperty("version")] public required string Version { get; init; }
    [JsonProperty("environment")] public required string Environment { get; init; }
    [JsonProperty("uptime_seconds")] public required long UptimeSeconds { get; init; }
    [JsonProperty("components")] public required ComponentStatus Components { get; init; }
    [JsonProperty("is_degraded")] public required bool IsDegraded { get; init; }
}

public record ComponentStatus
{
    [JsonProperty("database")] public required string Database { get; init; }
    [JsonProperty("authentication")] public required string Authentication { get; init; }
    [JsonProperty("network")] public required string Network { get; init; }
    [JsonProperty("registration")] public required string Registration { get; init; }
}
