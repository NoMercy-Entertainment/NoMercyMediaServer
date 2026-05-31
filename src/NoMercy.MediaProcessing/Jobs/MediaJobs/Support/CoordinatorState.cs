using NoMercy.Encoder.Decomposition;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs.Support;

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
    int ExpectedFinalCount,
    /// <summary>
    /// Ordered list of bundles for sequential dispatch. The coordinator
    /// dispatches one bundle, waits for its <see cref="DecomposedTask.BundledTaskIds"/>
    /// to all land in <c>EncodeTaskOutcomes</c>, then dispatches the next.
    /// Null for two-pass runs (their Pass1/Pass2 dispatch path predates this).
    /// </summary>
    DecomposedTask[]? Bundles = null,
    /// <summary>
    /// Index of the bundle currently being executed (in <see cref="Bundles"/>).
    /// On wake-up the coordinator checks whether this bundle's task IDs have
    /// completed; if yes, increments and dispatches the next.
    /// </summary>
    int CurrentBundleIndex = 0,
    /// <summary>
    /// Monotonic counter bumped on every <c>VideoEncodeJob.ReEnqueueSelf</c>.
    /// Exists solely to keep the serialized payload byte-distinct across
    /// successive wake-ups — <c>JobQueue.Enqueue</c> dedups by payload string,
    /// and <c>ReEnqueueSelf</c> runs from inside the worker BEFORE the original
    /// (still-reserved) coordinator row is deleted, so identical payloads were
    /// silently dropped and the coordinator died after one wake-up.
    /// </summary>
    int WakeSequence = 0,
    /// <summary>
    /// Last <c>doneCount</c> the coordinator logged for the current bundle.
    /// Used to throttle the WaitChildren progress line so a long-running
    /// bundle doesn't stamp the log on every 200ms wake-up (the coordinator
    /// re-enqueues itself faster than children complete). -1 means "no value
    /// logged yet" so the first poll always emits.
    /// </summary>
    int LastLoggedDoneCount = -1,
    /// <summary>
    /// Storage-relative output directory for this encode run. Captured at
    /// initial dispatch so coordinator wake-ups can populate child job
    /// payloads without a DB round-trip, enabling orphan-recovery to locate
    /// crash checkpoints by output directory.
    /// </summary>
    string? OutputDirectory = null
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
