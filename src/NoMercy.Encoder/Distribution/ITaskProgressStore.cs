namespace NoMercy.Encoder.Distribution;

/// <summary>
/// Keeps the latest progress snapshot per task so the dashboard can read
/// live progress from remote workers without maintaining a persistent
/// connection to each worker. Remote workers POST updates via
/// <see cref="ITaskProgressSink"/>; the controller writes into this
/// store; the dashboard reads on demand.
///
/// Snapshots are LRU-capped so a crashed / disconnected worker's stale
/// tasks age out instead of growing the map forever.
/// </summary>
public interface ITaskProgressStore
{
    void Update(string taskId, TaskProgressSnapshot snapshot);

    TaskProgressSnapshot? Get(string taskId);

    IReadOnlyList<TaskProgressSnapshot> GetAll();
}

public record TaskProgressSnapshot(
    string TaskId,
    string WorkerId,
    double PercentComplete,
    double? CurrentFps,
    double? CurrentSpeed,
    string? CurrentStage,
    double ElapsedSeconds,
    double? EstimatedRemainingSeconds,
    double CurrentTimeSeconds,
    double DurationSeconds,
    DateTime ReceivedAtUtc
);
