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

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.DTOs.Common;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Api.Controllers.V1.Dashboard.Admin;

[ApiController]
[Tags(tags: "Dashboard Libraries")]
[ApiVersion(version: 1.0)]
[Authorize(Policy = "Moderator")]
[Route(template: "api/v{version:apiVersion}/dashboard/logs", Order = 10)]
public class LogController : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int limit = 50,
        [FromQuery] string[]? types = null,
        [FromQuery] string[]? levels = null,
        [FromQuery] string? filter = null
    )
    {
        List<LogEntry> logs = await Logger.GetLogs(
            limit: limit,
            filter: entry =>
            {
                bool typeMatch =
                    types == null
                    || types.Length == 0
                    || types.Any(predicate: t =>
                        string.Equals(a: t, b: entry.Type, comparisonType: StringComparison.OrdinalIgnoreCase)
                    );
                bool levelMatch =
                    levels == null
                    || levels.Length == 0
                    || levels.Contains(value: entry.Level.ToString(), comparer: StringComparer.OrdinalIgnoreCase);

                if (!string.IsNullOrEmpty(value: filter))
                {
                    return typeMatch
                        && levelMatch
                        && entry.Message.Contains(value: filter, comparisonType: StringComparison.OrdinalIgnoreCase);
                }

                return typeMatch && levelMatch;
            }
        );

        return Ok(value: new DataResponseDto<List<LogEntry>> { Data = logs });
    }

    [HttpGet]
    [Route(template: "levels")]
    public IActionResult GetLogLevels()
    {
        return Ok(
            value: new DataResponseDto<string[]>
            {
                Data =
                [
                    Enum.Parse<LogEventLevel>(value: nameof(LogEventLevel.Verbose)).ToString(),
                    Enum.Parse<LogEventLevel>(value: nameof(LogEventLevel.Debug)).ToString(),
                    Enum.Parse<LogEventLevel>(value: nameof(LogEventLevel.Information)).ToString(),
                    Enum.Parse<LogEventLevel>(value: nameof(LogEventLevel.Warning)).ToString(),
                    Enum.Parse<LogEventLevel>(value: nameof(LogEventLevel.Error)).ToString(),
                    Enum.Parse<LogEventLevel>(value: nameof(LogEventLevel.Fatal)).ToString(),
                ],
            }
        );
    }

    [HttpGet]
    [Route(template: "types")]
    public IActionResult GetLogTypes()
    {
        return Ok(
            value: new DataResponseDto<IEnumerable<Logger.LogType>> { Data = Logger.LogTypes.Values }
        );
    }
}
