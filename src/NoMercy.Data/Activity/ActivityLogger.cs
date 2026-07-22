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
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(seconds: 1);

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
            row: new()
            {
                Category = ActivityCategory.Auth,
                Type = type,
                Time = DateTime.UtcNow,
                UserId = userId,
                DeviceId = deviceId,
                Success = success,
                ErrorCode = errorCode,
                Metadata = Serialize(metadata: metadata),
            },
            ct: ct
        );

    public Task LogConnectionAsync(
        string type,
        Guid userId,
        Ulid deviceId,
        CancellationToken ct = default
    ) =>
        WriteAsync(
            row: new()
            {
                Category = ActivityCategory.Connection,
                Type = type,
                Time = DateTime.UtcNow,
                UserId = userId,
                DeviceId = deviceId,
            },
            ct: ct
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
            row: new()
            {
                Category = ActivityCategory.Playback,
                Type = type,
                Time = DateTime.UtcNow,
                UserId = userId,
                DeviceId = deviceId,
                MediaId = mediaId,
                Metadata = Serialize(metadata: metadata),
            },
            ct: ct
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
            row: new()
            {
                Category = ActivityCategory.Configuration,
                Type = type,
                Time = DateTime.UtcNow,
                UserId = userId,
                DeviceId = deviceId,
                Metadata = Serialize(
                    metadata: new
                    {
                        key = configKey,
                        old_value = oldValue,
                        new_value = newValue,
                    }
                ),
            },
            ct: ct
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
            row: new()
            {
                Category = ActivityCategory.Failure,
                Type = type,
                Time = DateTime.UtcNow,
                UserId = userId,
                DeviceId = deviceId,
                MediaId = mediaId,
                Success = false,
                ErrorCode = errorCode,
                Metadata = Serialize(metadata: new { message, extra = metadata }),
            },
            ct: ct
        );

    private async Task WriteAsync(ActivityLog row, CancellationToken ct)
    {
        // Drop rows that would violate the FK constraints rather than retry-and-log-noise.
        // Auth callbacks fall back to Ulid.Empty / Guid.Empty when the device or user
        // can't be resolved (e.g. failed login, device-less OAuth redirect). Those IDs
        // don't match any Devices/Users row, so the FK fails on every retry.
        if (row.DeviceId == Ulid.Empty || row.UserId == Guid.Empty)
        {
            _logger.LogDebug(
                message: "Skipping activity log {Type}: missing DeviceId or UserId (device={DeviceId}, user={UserId})", args: [row.Type, row.DeviceId, row.UserId]
            );
            return;
        }

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await using MediaContext ctx = await _contextFactory.CreateDbContextAsync(cancellationToken: ct);
                await ctx.ActivityLogs.AddAsync(entity: row, cancellationToken: ct);
                await ctx.SaveChangesAsync(cancellationToken: ct);
                BroadcastSafe(row: row);
                return;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                // Fold the exception (incl. inner) into the template so the cause
                // shows up in the structured-log JSON; the @x exception field is
                // dropped by the upstream enricher pipeline.
                _logger.LogWarning(
                    message: "Activity log write failed (attempt {Attempt}/{Max}) for {Type}: {ErrorChain}; retrying", args: [attempt, MaxRetries, row.Type, FlattenError(ex: ex)]
                );
                await Task.Delay(delay: RetryDelay, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    message: "Activity log write failed after {Max} attempts; dropping row {Type}: {ErrorChain}", args: [MaxRetries, row.Type, FlattenError(ex: ex)]
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
            _ = _hubBroadcaster.BroadcastAsync(row: row);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(exception: ex, message: "Activity hub broadcast failed for {Type}", args: row.Type);
        }
    }

    private static string FlattenError(Exception ex)
    {
        StringBuilder sb = new();
        Exception? current = ex;
        while (current is not null)
        {
            if (sb.Length > 0)
                sb.Append(value: " -> ");
            sb.Append(value: current.GetType().Name).Append(value: ": ").Append(value: current.Message);
            current = current.InnerException;
        }
        return sb.ToString();
    }

    private static string? Serialize(object? metadata)
    {
        if (metadata is null)
            return null;
        return JsonConvert.SerializeObject(value: metadata);
    }
}
