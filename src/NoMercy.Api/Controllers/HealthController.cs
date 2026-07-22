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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Status;
using NoMercy.Setup.Boot;

namespace NoMercy.Api.Controllers;

/// <summary>
/// Health check endpoint for container orchestration and load balancers
/// </summary>
[ApiController]
[Route(template: "[controller]")]
[AllowAnonymous]
public class HealthController(MediaContext mediaContext, IBootStatus bootStatus) : ControllerBase
{
    /// <summary>
    /// Basic liveness probe — returns 200 if the server process is running
    /// </summary>
    [HttpGet]
    [ProducesResponseType(type: typeof(HealthResponse), statusCode: StatusCodes.Status200OK)]
    public IActionResult GetLiveness()
    {
        return Ok(value: new HealthResponse { Status = "healthy", Timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Readiness probe — returns 200 when the server can handle requests, 503 if not ready
    /// </summary>
    [HttpGet(template: "ready")]
    [ProducesResponseType(type: typeof(ReadinessResponse), statusCode: StatusCodes.Status200OK)]
    [ProducesResponseType(type: typeof(ReadinessResponse), statusCode: StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetReadiness()
    {
        bool databaseHealthy = await CheckDatabase();

        bool isReady = bootStatus.IsStarted && databaseHealthy;
        string status = isReady ? "ready" : "not_ready";

        ReadinessResponse response = new()
        {
            Status = status,
            Timestamp = DateTime.UtcNow,
            Database = databaseHealthy ? "ok" : "unavailable",
            ServerStarted = bootStatus.IsStarted,
        };

        return isReady
            ? Ok(value: response)
            : StatusCode(statusCode: StatusCodes.Status503ServiceUnavailable, value: response);
    }

    /// <summary>
    /// Detailed health check with component status and degraded mode info
    /// </summary>
    [HttpGet(template: "detailed")]
    [ProducesResponseType(type: typeof(DetailedHealthResponse), statusCode: StatusCodes.Status200OK)]
    [ProducesResponseType(type: typeof(DetailedHealthResponse), statusCode: StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetDetailed()
    {
        bool databaseHealthy = await CheckDatabase();
        bool isDegraded = Start.IsDegradedMode;

        string status = DetermineStatus(serverStarted: bootStatus.IsStarted, databaseHealthy: databaseHealthy, isDegraded: isDegraded);

        DetailedHealthResponse response = new()
        {
            Status = status,
            Timestamp = DateTime.UtcNow,
            Version = Software.GetReleaseVersion(),
            Environment =
                Environment.GetEnvironmentVariable(variable: "ASPNETCORE_ENVIRONMENT") ?? "Production",
            UptimeSeconds = (long)(DateTime.UtcNow - Info.StartTime).TotalSeconds,
            Components = new()
            {
                Database = databaseHealthy ? "ok" : "unavailable",
                Authentication = isDegraded ? "degraded" : "ok",
                Network = isDegraded ? "degraded" : "ok",
                Registration = isDegraded ? "degraded" : "ok",
            },
            IsDegraded = isDegraded,
        };

        return databaseHealthy
            ? Ok(value: response)
            : StatusCode(statusCode: StatusCodes.Status503ServiceUnavailable, value: response);
    }

    private static string DetermineStatus(bool serverStarted, bool databaseHealthy, bool isDegraded)
    {
        if (!serverStarted)
            return "starting";
        if (!databaseHealthy)
            return "unhealthy";
        if (isDegraded)
            return "degraded";
        return "healthy";
    }

    private async Task<bool> CheckDatabase()
    {
        try
        {
            await mediaContext.Database.ExecuteSqlRawAsync(sql: "SELECT 1");
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
    [JsonProperty(propertyName: "status")]
    public required string Status { get; init; }

    [JsonProperty(propertyName: "timestamp")]
    public required DateTime Timestamp { get; init; }
}

public record ReadinessResponse : HealthResponse
{
    [JsonProperty(propertyName: "database")]
    public required string Database { get; init; }

    [JsonProperty(propertyName: "server_started")]
    public required bool ServerStarted { get; init; }
}

public record DetailedHealthResponse : HealthResponse
{
    [JsonProperty(propertyName: "version")]
    public required string Version { get; init; }

    [JsonProperty(propertyName: "environment")]
    public required string Environment { get; init; }

    [JsonProperty(propertyName: "uptime_seconds")]
    public required long UptimeSeconds { get; init; }

    [JsonProperty(propertyName: "components")]
    public required ComponentStatus Components { get; init; }

    [JsonProperty(propertyName: "is_degraded")]
    public required bool IsDegraded { get; init; }
}

public record ComponentStatus
{
    [JsonProperty(propertyName: "database")]
    public required string Database { get; init; }

    [JsonProperty(propertyName: "authentication")]
    public required string Authentication { get; init; }

    [JsonProperty(propertyName: "network")]
    public required string Network { get; init; }

    [JsonProperty(propertyName: "registration")]
    public required string Registration { get; init; }
}
