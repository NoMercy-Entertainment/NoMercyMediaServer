using NoMercy.Database.Models;

namespace NoMercy.EncoderV2.Workers;

/// <summary>
/// Interface for broadcasting encoding progress updates.
/// Implemented by SignalR hub service in the server project.
/// </summary>
public interface IEncodingProgressBroadcaster
{
    /// <summary>
    /// Send task progress update to connected clients
    /// </summary>
    Task SendTaskProgressAsync(Ulid jobId, Ulid taskId, EncodingProgress progress);

    /// <summary>
    /// Send task state change notification
    /// </summary>
    Task SendTaskStateChangeAsync(Ulid jobId, Ulid taskId, string previousState, string newState, string taskType, string? errorMessage);

    /// <summary>
    /// Send job state change notification
    /// </summary>
    Task SendJobStateChangeAsync(Ulid jobId, string previousState, string newState, string? errorMessage);

    /// <summary>
    /// Send node status change notification
    /// </summary>
    Task SendNodeStatusChangeAsync(Ulid nodeId, bool isHealthy, bool isEnabled, DateTime? lastHeartbeat);

    /// <summary>
    /// Send new job created notification
    /// </summary>
    Task SendJobCreatedAsync(EncodingJob job);

    /// <summary>
    /// Send job removed notification
    /// </summary>
    Task SendJobRemovedAsync(Ulid jobId, string reason);
}
