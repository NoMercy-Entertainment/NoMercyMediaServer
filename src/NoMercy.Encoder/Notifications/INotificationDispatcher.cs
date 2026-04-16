namespace NoMercy.Encoder.Notifications;

/// <summary>
/// Sends a notification about an encoder lifecycle event to one or more
/// external endpoints. The default implementation is a webhook POST; plugins
/// can replace or compose this (Discord, Slack, email).
///
/// Notifications are best-effort — a dispatcher failure must never fail the
/// encode itself.
/// </summary>
public interface INotificationDispatcher
{
    Task NotifyStartedAsync(EncodingStartedNotification notification, CancellationToken ct);
    Task NotifyCompletedAsync(EncodingCompletedNotification notification, CancellationToken ct);
    Task NotifyFailedAsync(EncodingFailedNotification notification, CancellationToken ct);
}

public record EncodingStartedNotification(
    int JobId,
    string InputPath,
    string OutputPath,
    string ProfileName
);

public record EncodingCompletedNotification(int JobId, string OutputPath, TimeSpan Duration);

public record EncodingFailedNotification(
    int JobId,
    string InputPath,
    string ErrorMessage,
    string? ExceptionType
);
