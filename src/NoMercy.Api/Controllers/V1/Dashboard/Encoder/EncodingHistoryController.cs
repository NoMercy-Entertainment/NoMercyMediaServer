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
using Newtonsoft.Json;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Media;

namespace NoMercy.Api.Controllers.V1.Dashboard.Encoder;

[ApiController]
[Tags(tags: "Dashboard Encoding History")]
[ApiVersion(version: 1.0)]
[Authorize]
[Route(template: "api/v{version:apiVersion}/dashboard/encoding/history", Order = 10)]
public class EncodingHistoryController(IEncodingHistoryRepository historyRepository)
    : BaseController
{
    /// <summary>
    /// Paginated encoding history. Newest encodes first.
    /// </summary>
    /// <param name="pageSize">Rows per page (1–500, default 50).</param>
    /// <param name="pageIndex">Zero-based page index (default 0).</param>
    [HttpGet]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Index(
        [FromQuery] int pageSize = 50,
        [FromQuery] int pageIndex = 0
    )
    {
        pageSize = Math.Clamp(value: pageSize, min: 1, max: 500);
        if (pageIndex < 0)
            pageIndex = 0;

        List<EncodingHistory> entries = await historyRepository.GetRecentAsync(pageSize: pageSize, pageIndex: pageIndex);
        int total = await historyRepository.GetTotalCountAsync();

        List<EncodingHistoryEntryDto> data = entries
            .Select(selector: e => new EncodingHistoryEntryDto(
                Id: e.Id.ToString(),
                InputPath: e.InputPath,
                OutputPath: e.OutputPath,
                ProfileId: e.ProfileId?.ToString(),
                ProfileName: e.ProfileName,
                EncoderUsed: e.EncoderUsed,
                GpuUsed: e.GpuUsed,
                DurationSeconds: e.DurationSeconds,
                InputSizeBytes: e.InputSizeBytes,
                OutputSizeBytes: e.OutputSizeBytes,
                CompressionRatio: e.CompressionRatio,
                AverageSpeed: e.AverageSpeed,
                AverageFps: e.AverageFps,
                CreatedAt: e.CreatedAt
            ))
            .ToList();

        return Ok(
            value: new HistoryListResponse(
                Data: data,
                Meta: new(
                    Total: total,
                    PageSize: pageSize,
                    PageIndex: pageIndex,
                    TotalPages: (int)Math.Ceiling(a: (double)total / pageSize)
                )
            )
        );
    }

    /// <summary>
    /// Aggregated stats across every history row (total encodes, bytes
    /// in / out, average speed / fps / compression ratio). One SQL
    /// round-trip, cached for 30 seconds.
    /// </summary>
    [HttpGet(template: "stats")]
    [ResponseCache(Duration = 30)]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Stats()
    {
        EncodingHistoryStats stats = await historyRepository.GetAggregateStatsAsync();
        return Ok(value: stats);
    }

    /// <summary>
    /// Delete a single history row. Users clean up individual rows from
    /// the dashboard; the encoded output on disk is untouched.
    /// </summary>
    [HttpDelete(template: "{id}")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!Ulid.TryParse(base32: id, ulid: out Ulid entryId))
            return BadRequestResponse(detail: "Invalid history id");

        bool removed = await historyRepository.DeleteAsync(id: entryId);
        return removed ? NoContent() : NotFoundResponse(detail: "History entry not found");
    }

    /// <summary>
    /// Bulk purge. <c>older_than_days</c> drops every row older than N days;
    /// omit it to clear the entire history. Owner-only because clearing the
    /// full history is a coarse change.
    /// </summary>
    [HttpPost(template: "purge")]
    [Authorize(Policy = "Owner")]
    public async Task<IActionResult> Purge([FromBody] PurgeHistoryRequest request)
    {
        int removed = request.OlderThanDays.HasValue
            ? await historyRepository.DeleteOlderThanAsync(
                olderThan: DateTime.UtcNow.AddDays(value: -Math.Max(val1: 0, val2: request.OlderThanDays.Value))
            )
            : await historyRepository.DeleteAllAsync();

        return Ok(value: new { removed });
    }
}

public record PurgeHistoryRequest(
    [property: JsonProperty(propertyName: "older_than_days")] int? OlderThanDays = null
);

public record HistoryListResponse(
    [property: JsonProperty(propertyName: "data")] List<EncodingHistoryEntryDto> Data,
    [property: JsonProperty(propertyName: "meta")] HistoryListMeta Meta
);

public record HistoryListMeta(
    [property: JsonProperty(propertyName: "total")] int Total,
    [property: JsonProperty(propertyName: "page_size")] int PageSize,
    [property: JsonProperty(propertyName: "page_index")] int PageIndex,
    [property: JsonProperty(propertyName: "total_pages")] int TotalPages
);

/// <summary>
/// Frontend-facing shape for a single history row. Snake_case via
/// JsonProperty matches the rest of the dashboard API surface.
/// </summary>
public record EncodingHistoryEntryDto(
    [property: JsonProperty(propertyName: "id")] string Id,
    [property: JsonProperty(propertyName: "input_path")] string InputPath,
    [property: JsonProperty(propertyName: "output_path")] string OutputPath,
    [property: JsonProperty(propertyName: "profile_id")] string? ProfileId,
    [property: JsonProperty(propertyName: "profile_name")] string ProfileName,
    [property: JsonProperty(propertyName: "encoder_used")] string EncoderUsed,
    [property: JsonProperty(propertyName: "gpu_used")] string? GpuUsed,
    [property: JsonProperty(propertyName: "duration_seconds")] double DurationSeconds,
    [property: JsonProperty(propertyName: "input_size_bytes")] long InputSizeBytes,
    [property: JsonProperty(propertyName: "output_size_bytes")] long OutputSizeBytes,
    [property: JsonProperty(propertyName: "compression_ratio")] double CompressionRatio,
    [property: JsonProperty(propertyName: "average_speed")] double AverageSpeed,
    [property: JsonProperty(propertyName: "average_fps")] double AverageFps,
    [property: JsonProperty(propertyName: "created_at")] DateTime CreatedAt
);
