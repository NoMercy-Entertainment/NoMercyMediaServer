namespace NoMercy.NmSystem.Lifecycle;

/// <summary>
/// Tracks which boot stages have completed so consumers can gate work on a
/// specific subset of the boot sequence. Replaces the boolean
/// <c>IServerReadinessGate</c> with a granular, queryable phase model — the
/// encoder queue can wait on <see cref="BootStage.Binaries"/> alone, while
/// the broader queue runner can wait on <see cref="BootStage.All"/>.
/// </summary>
public interface IServerPhaseTracker
{
    /// <summary>Bitmask of every stage that has been marked complete so far.</summary>
    BootStage CompletedStages { get; }

    /// <summary>True iff every flag in <paramref name="stage"/> is in
    /// <see cref="CompletedStages"/>. Useful for composite stages
    /// (e.g. <c>IsComplete(BootStage.Binaries | BootStage.Hardware)</c>).</summary>
    bool IsComplete(BootStage stage);

    /// <summary>Mark a stage as complete. Idempotent — subsequent calls for the same
    /// stage are no-ops. Resolves any awaiters waiting on this stage.</summary>
    void MarkComplete(BootStage stage);

    /// <summary>Resolves once every flag in <paramref name="stage"/> is complete.
    /// Pass <see cref="BootStage.All"/> for "wait until fully idle".</summary>
    Task WhenReachedAsync(BootStage stage, CancellationToken ct);

    /// <summary>Fires whenever any stage transitions to complete. Argument is the
    /// individual stage that just completed (not the cumulative mask).</summary>
    event Action<BootStage> StageCompleted;
}
