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
            new ActivityLog
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
            new ActivityLog
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
            new ActivityLog
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
            new ActivityLog
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
            new ActivityLog
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

    private async Task WriteAsync(ActivityLog row, CancellationToken ct)
    {
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
                _logger.LogWarning(
                    ex,
                    "Activity log write failed (attempt {Attempt}/{Max}); retrying",
                    attempt,
                    MaxRetries
                );
                await Task.Delay(RetryDelay, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Activity log write failed after {Max} attempts; dropping row {Type}",
                    MaxRetries,
                    row.Type
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

    private static string? Serialize(object? metadata)
    {
        if (metadata is null)
            return null;
        return JsonConvert.SerializeObject(metadata);
    }
}

public interface IActivityHubBroadcaster
{
    Task BroadcastAsync(ActivityLog row, CancellationToken ct = default);
}
