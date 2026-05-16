using NoMercy.Encoder.Output;
using NoMercy.Resources;

namespace NoMercy.Encoder.Decomposition;

/// <summary>
/// One decomposed unit of work produced by <see cref="IDecompositionStrategy.Decompose"/>.
///
/// A coordinator job creates one <see cref="DecomposedTask"/> per logical work unit,
/// enqueues each as a child job in the <c>encoder-task</c> queue, and tracks
/// completion via <see cref="EncodeTaskCompletedEvent"/> on the event bus.
///
/// Payload design: child jobs store the <see cref="ParentJobId"/> plus these
/// slim indices rather than embedding the full <see cref="OutputPlan"/>. At
/// execution time the child deserializes the parent job's <see cref="Encoder.Pipeline.EncodingRequest"/>
/// and selects only the slice it owns — this keeps the child payload well under
/// the 4096-byte queue column limit.
/// </summary>
public record DecomposedTask(
    /// <summary>Unique identifier for this task within the encode run.</summary>
    string TaskId,
    /// <summary>
    /// Queue-job ID of the coordinator that spawned this task.
    /// Set by the coordinator after persisting itself; not set during Decompose.
    /// </summary>
    int ParentJobId,
    /// <summary>
    /// Shared tag grouping all tasks spawned by the same coordinator run.
    /// Allows queries like "find all tasks for encode run X" without a full
    /// parent-id walk.
    /// </summary>
    string GroupTag,
    /// <summary>What kind of work this task performs.</summary>
    EncodeTaskKind Kind,
    /// <summary>
    /// Zero-based index into the relevant output array of the parent's OutputPlan.
    /// Meaning depends on <see cref="Kind"/>:
    ///   <see cref="EncodeTaskKind.Video"/>    → VideoOutputs[OutputIndex]
    ///   <see cref="EncodeTaskKind.Audio"/>    → AudioOutputs[OutputIndex]
    ///   <see cref="EncodeTaskKind.Subtitle"/> → SubtitleOutputs[OutputIndex]
    ///   <see cref="EncodeTaskKind.Pass1"/>    → VideoOutputs[OutputIndex] (the variant to analyze)
    ///   <see cref="EncodeTaskKind.Pass2"/>    → VideoOutputs[OutputIndex]
    ///   <see cref="EncodeTaskKind.Thumbnails"/>, <see cref="EncodeTaskKind.Chapters"/>,
    ///   <see cref="EncodeTaskKind.Whole"/>    → ignored (always 0)
    /// </summary>
    int OutputIndex,
    /// <summary>
    /// Hardware requirements for this task. Used by Task 2's resource
    /// scheduler to route GPU tasks to GPU workers and CPU tasks to CPU workers.
    /// </summary>
    ResourceRequirement? Resources,
    /// <summary>
    /// Relative compute weight for worker assignment. Higher = heavier job.
    /// Unitless — compare across tasks in the same run, not against wall-clock.
    /// </summary>
    int EstimatedCostUnits = 1,
    /// <summary>
    /// For <see cref="EncodeTaskKind.Pass2"/> tasks: absolute path to the
    /// stats file produced by the corresponding Pass1. The coordinator sets
    /// this after Pass1 completes and before enqueuing the Pass2 children.
    /// Null for all other task kinds.
    /// </summary>
    string? StatsFilePath = null,
    /// <summary>
    /// Human-readable label for dashboard rows, e.g. "1080p H.264 SDR",
    /// "eng 5.1 AC3", "subtitles/eng.vtt", "thumbnails".
    /// </summary>
    string Label = "",
    /// <summary>
    /// When set, this task batches multiple outputs into one ffmpeg run via
    /// filter_complex split — the indexes identify which entries in the
    /// parent's OutputPlan.{Video,Audio,Subtitle}Outputs array this task
    /// covers. Null / empty means "use OutputIndex as a single-output slice"
    /// (the legacy one-rung-per-task path).
    /// </summary>
    int[]? SourceIndexes = null,
    /// <summary>
    /// Dispatch-time bundle: when the coordinator packs N per-stream tasks
    /// into one ffmpeg invocation, it dispatches a single synthetic Whole
    /// task carrying the original stream-task IDs here. After the bundled
    /// encode succeeds the executor writes one <see cref="Database.Models.Encoder.EncodeTaskOutcome"/>
    /// row per ID so the coordinator's WaitChildren phase sees each stream
    /// completed (separation of streams preserved for tracking even though
    /// one process did the work).
    /// </summary>
    string[]? BundledTaskIds = null,
    /// <summary>
    /// For Whole-kind bundles, the subset of <see cref="OutputPlan.VideoOutputs"/>
    /// indexes this bundle covers. Null means "all video outputs"; empty
    /// array means "no video in this bundle" (aux-only bundle).
    /// </summary>
    int[]? VideoSliceIndexes = null,
    /// <summary>
    /// For Whole-kind bundles, the subset of <see cref="OutputPlan.AudioOutputs"/>
    /// indexes this bundle covers. Null means "all audio outputs".
    /// </summary>
    int[]? AudioSliceIndexes = null,
    /// <summary>
    /// For Whole-kind bundles, the subset of <see cref="OutputPlan.SubtitleOutputs"/>
    /// indexes this bundle covers. Null means "all subtitle outputs".
    /// </summary>
    int[]? SubtitleSliceIndexes = null,
    /// <summary>
    /// For Whole-kind bundles, whether to include the thumbnail strip. Null
    /// means "include if the plan has one"; false omits it from this bundle.
    /// </summary>
    bool? IncludeThumbnails = null
);
