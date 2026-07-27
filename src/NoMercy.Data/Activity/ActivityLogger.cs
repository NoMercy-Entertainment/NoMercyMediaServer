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

using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Users;

namespace NoMercy.Data.Activity;

public class ActivityLogger : IActivityLogger
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    private readonly IDbContextFactory<MediaContext> _contextFactory;
    private readonly ILogger<ActivityLogger> _logger;
    private readonly IActivityHubBroadcaster? _hubBroadcaster;

    public ActivityLogger(
        IDbContextFactory<MediaContext> contextFactory,
        ILogger<ActivityLogger> logger,
        IActivityHubBroadcaster? hubBroadcaster
    )
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _hubBroadcaster = hubBroadcaster;
    }

    public Task LogAuthAsync(
        string type,
        Guid userId,
        Ulid deviceId,
        bool success,
        string? errorCode = null,
        object? metadata = null,
        CancellationToken ct = default
    ) =>
        WriteAsync(
            new()
            {
                Category = ActivityCategory.Auth,
                Type = type,
                Time = DateTime.UtcNow,
                UserId = userId,
                DeviceId = deviceId,
                Success = success,
                ErrorCode = errorCode,
                Metadata = Serialize(metadata),
            },
            ct
        );

    public Task LogConnectionAsync(
        string type,
        Guid userId,
        Ulid deviceId,
        CancellationToken ct = default
    ) =>
        WriteAsync(
            new()
            {
                Category = ActivityCategory.Connection,
                Type = type,
                Time = DateTime.UtcNow,
                UserId = userId,
                DeviceId = deviceId,
            },
            ct
        );

    public Task LogPlaybackAsync(
        string type,
        Guid userId,
        Ulid deviceId,
        Ulid mediaId,
        object? metadata = null,
        CancellationToken ct = default
    ) =>
        WriteAsync(
            new()
            {
                Category = ActivityCategory.Playback,
                Type = type,
                Time = DateTime.UtcNow,
                UserId = userId,
                DeviceId = deviceId,
                MediaId = mediaId,
                Metadata = Serialize(metadata),
            },
            ct
        );

    public Task LogConfigurationAsync(
        string type,
        Guid userId,
        Ulid deviceId,
        string configKey,
        object? oldValue,
        object? newValue,
        CancellationToken ct = default
    ) =>
        WriteAsync(
            new()
            {
                Category = ActivityCategory.Configuration,
                Type = type,
                Time = DateTime.UtcNow,
                UserId = userId,
                DeviceId = deviceId,
                Metadata = Serialize(
                    new
                    {
                        key = configKey,
                        old_value = oldValue,
                        new_value = newValue,
                    }
                ),
            },
            ct
        );

    public Task LogFailureAsync(
        string type,
        Guid userId,
        Ulid deviceId,
        string errorCode,
        string message,
        Ulid? mediaId = null,
        object? metadata = null,
        CancellationToken ct = default
    ) =>
        WriteAsync(
            new()
            {
                Category = ActivityCategory.Failure,
                Type = type,
                Time = DateTime.UtcNow,
                UserId = userId,
                DeviceId = deviceId,
                MediaId = mediaId,
                Success = false,
                ErrorCode = errorCode,
                Metadata = Serialize(new { message, extra = metadata }),
            },
            ct
        );

    public Task LogSystemAsync(
        ActivityCategory category,
        string type,
        Ulid? mediaId = null,
        bool success = true,
        string? errorCode = null,
        object? metadata = null,
        CancellationToken ct = default
    ) =>
        WriteAsync(
            new()
            {
                Category = category,
                Type = type,
                Time = DateTime.UtcNow,
                MediaId = mediaId,
                Success = success,
                ErrorCode = errorCode,
                Metadata = Serialize(metadata),
            },
            ct
        );

    private async Task WriteAsync(ActivityLog row, CancellationToken ct)
    {
        // Empty is what the callers reach for when they could not resolve a device or user —
        // a failed login, a device-less OAuth redirect, or anything the server did on its own.
        // No such row exists to point at, so the FK would fail on every retry. Null says the
        // same thing and the column accepts it, which is how a system event gets recorded at
        // all rather than being quietly dropped.
        if (row.DeviceId == Ulid.Empty)
            row.DeviceId = null;
        if (row.UserId == Guid.Empty)
            row.UserId = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await using MediaContext ctx = await _contextFactory.CreateDbContextAsync(ct);
                await ctx.ActivityLogs.AddAsync(row, ct);
                await ctx.SaveChangesAsync(ct);
                BroadcastSafe(row);
                return;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                // Fold the exception (incl. inner) into the template so the cause
                // shows up in the structured-log JSON; the @x exception field is
                // dropped by the upstream enricher pipeline.
                _logger.LogWarning(
                    "Activity log write failed (attempt {Attempt}/{Max}) for {Type}: {ErrorChain}; retrying",
                    [attempt, MaxRetries, row.Type, FlattenError(ex)]
                );
                await Task.Delay(RetryDelay, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    "Activity log write failed after {Max} attempts; dropping row {Type}: {ErrorChain}",
                    [MaxRetries, row.Type, FlattenError(ex)]
                );
                return;
            }
        }
    }

    private void BroadcastSafe(ActivityLog row)
    {
        if (_hubBroadcaster is null)
            return;

        try
        {
            _ = _hubBroadcaster.BroadcastAsync(row);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Activity hub broadcast failed for {Type}", row.Type);
        }
    }

    private static string FlattenError(Exception ex)
    {
        StringBuilder sb = new();
        Exception? current = ex;
        while (current is not null)
        {
            if (sb.Length > 0)
                sb.Append(" -> ");
            sb.Append(current.GetType().Name).Append(": ").Append(current.Message);
            current = current.InnerException;
        }
        return sb.ToString();
    }

    private static string? Serialize(object? metadata)
    {
        if (metadata is null)
            return null;
        return JsonConvert.SerializeObject(metadata);
    }
}
