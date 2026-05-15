namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

/// <summary>
/// Serialized into the <see cref="VideoEncodeJob"/> payload on every re-enqueue.
/// Presence of this field on a job instance signals the state machine that the
/// current run is a coordinator wake-up, not an initial decomposition.
/// </summary>
public sealed record CoordinatorState(
    /// <summary>Shared run tag matching all <c>EncodeTaskOutcome</c> rows for this encode.</summary>
    string GroupTag,
    /// <summary>TaskIds for every decomposed task in this run (Pass1 + Pass2 + aux).</summary>
    string[] TaskIds,
    /// <summary>Current phase of the coordinator state machine.</summary>
    CoordinatorPhase Phase,
    /// <summary>UTC time when Pass1 children were dispatched. Null for single-pass.</summary>
    DateTime? Pass1DispatchedAt,
    /// <summary>UTC time when Pass2 children were dispatched. Null for single-pass.</summary>
    DateTime? Pass2DispatchedAt,
    /// <summary>Stats file path resolved after all Pass1 tasks completed. Null for single-pass.</summary>
    string? Pass1StatsPath,
    /// <summary>Preset ID used to rebuild child jobs on Pass2 dispatch.</summary>
    Ulid PresetId,
    /// <summary>
    /// Total number of non-Pass1 tasks expected. Used to detect completion in
    /// <see cref="CoordinatorPhase.WaitChildren"/>.
    /// </summary>
    int ExpectedFinalCount
);

/// <summary>
/// Phases of the <see cref="VideoEncodeJob"/> durable coordinator state machine.
/// </summary>
public enum CoordinatorPhase
{
    /// <summary>
    /// Waiting for all Pass1 child tasks to complete before dispatching Pass2.
    /// Only used in two-pass encode runs.
    /// </summary>
    WaitPass1,

    /// <summary>
    /// All required children have been dispatched. Coordinator polls
    /// <c>EncodeTaskOutcomes</c> until every non-Pass1 TaskId is present.
    /// </summary>
    WaitChildren,

    /// <summary>
    /// All children are done. Coordinator opens a fresh context, runs
    /// post-encode work (OCR, library refresh), then completes.
    /// </summary>
    Finalize,
}
