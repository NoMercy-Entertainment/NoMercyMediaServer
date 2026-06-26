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

using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Media;

namespace NoMercy.Data.Repositories;

/// <summary>
/// Append-only repository for encoding history entries. The encoder writes
/// one row per successful encode; the dashboard reads it paginated.
/// </summary>
public class EncodingHistoryRepository(MediaContext context) : IEncodingHistoryRepository
{
    public Task AddAsync(EncodingHistory entry)
    {
        context.EncodingHistory.Add(entry);
        return context.SaveChangesAsync();
    }

    public Task<List<EncodingHistory>> GetRecentAsync(int pageSize = 50, int pageIndex = 0)
    {
        if (pageSize <= 0)
            pageSize = 50;
        if (pageIndex < 0)
            pageIndex = 0;

        return context
            .EncodingHistory.AsNoTracking()
            .OrderByDescending(h => h.CreatedAt)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public Task<int> GetTotalCountAsync() => context.EncodingHistory.CountAsync();

    public Task<EncodingHistory?> GetByIdAsync(Ulid id) =>
        context.EncodingHistory.AsNoTracking().FirstOrDefaultAsync(h => h.Id == id);

    public async Task<bool> DeleteAsync(Ulid id)
    {
        EncodingHistory? existing = await context.EncodingHistory.FirstOrDefaultAsync(h =>
            h.Id == id
        );
        if (existing is null)
            return false;

        context.EncodingHistory.Remove(existing);
        await context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Bulk purge every row strictly older than <paramref name="olderThan"/>.
    /// Users clean up the dashboard view; the orchestrator keeps writing
    /// fresh rows. Returns the removed-row count so the API response can
    /// surface it.
    /// </summary>
    public Task<int> DeleteOlderThanAsync(DateTime olderThan) =>
        context.EncodingHistory.Where(h => h.CreatedAt < olderThan).ExecuteDeleteAsync();

    public Task<int> DeleteAllAsync() => context.EncodingHistory.ExecuteDeleteAsync();

    /// <summary>
    /// Aggregated stats for the dashboard summary card — total rows,
    /// bytes in / out, average speed, average compression ratio, average
    /// fps. Computed in one SQL round-trip so the dashboard stays snappy
    /// even with tens of thousands of history rows.
    /// </summary>
    public async Task<EncodingHistoryStats> GetAggregateStatsAsync()
    {
        var raw = await context
            .EncodingHistory.GroupBy(_ => 1)
            .Select(g => new
            {
                TotalEncodes = g.Count(),
                TotalInputBytes = g.Sum(h => h.InputSizeBytes),
                TotalOutputBytes = g.Sum(h => h.OutputSizeBytes),
                AverageSpeed = g.Average(h => h.AverageSpeed),
                AverageCompressionRatio = g.Average(h => h.CompressionRatio),
                AverageFps = g.Average(h => h.AverageFps),
            })
            .FirstOrDefaultAsync();

        return raw is null
            ? new(0, 0, 0, 0, 0, 0)
            : new EncodingHistoryStats(
                TotalEncodes: raw.TotalEncodes,
                TotalInputBytes: raw.TotalInputBytes,
                TotalOutputBytes: raw.TotalOutputBytes,
                AverageSpeed: raw.AverageSpeed,
                AverageCompressionRatio: raw.AverageCompressionRatio,
                AverageFps: raw.AverageFps
            );
    }
}

public record EncodingHistoryStats(
    [property: JsonProperty("total_encodes")] int TotalEncodes,
    [property: JsonProperty("total_input_bytes")] long TotalInputBytes,
    [property: JsonProperty("total_output_bytes")] long TotalOutputBytes,
    [property: JsonProperty("average_speed")] double AverageSpeed,
    [property: JsonProperty("average_compression_ratio")] double AverageCompressionRatio,
    [property: JsonProperty("average_fps")] double AverageFps
);
