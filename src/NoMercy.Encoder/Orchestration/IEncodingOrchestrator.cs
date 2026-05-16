using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Storage;

namespace NoMercy.Encoder.Orchestration;

/// <summary>
/// Top-level entry point for an encode job. Resolves the strategy matching
/// the request's format + encode mode and hands the encode off to it.
/// This is what queue jobs call instead of <see cref="IEncoder"/> directly —
/// keeps dispatch logic out of the job class.
/// </summary>
public interface IEncodingOrchestrator
{
    /// <summary>
    /// Run the full encode for <paramref name="request"/>. Used for
    /// strategies that return a single <see cref="EncodeTaskKind.Whole"/>
    /// task and by the coordinator when it finalizes after all children complete.
    /// </summary>
    Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        IProgressObserver? progress = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Run one decomposed task from a prior <c>Decompose</c> call.
    /// The request carries the full <see cref="EncodingRequest"/> (input path,
    /// profile, storage refs). The <paramref name="task"/> narrows execution to
    /// a single output slice identified by its <see cref="DecomposedTask.Kind"/>
    /// and <see cref="DecomposedTask.OutputIndex"/>.
    ///
    /// Used by <c>EncodeTaskJob</c> — one queue job per decomposed task.
    /// </summary>
    Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        DecomposedTask task,
        IProgressObserver? progress = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Run the pipeline through PlanStage only (Analyze → Validate → Plan),
    /// then call <c>strategy.Decompose(plan, groupTag)</c> to get the list of
    /// child tasks. No ffmpeg process is launched.
    ///
    /// Used by the encode coordinator before enqueuing child jobs so it knows
    /// how many tasks to expect and which queue names to route them to.
    /// Returns a single <see cref="EncodeTaskKind.Whole"/> task when the
    /// resolved strategy does not override <c>Decompose</c>.
    /// </summary>
    Task<DecomposedTask[]> DecomposeAsync(
        EncodingRequest request,
        string groupTag,
        CancellationToken ct = default
    );

    /// <summary>
    /// Move all artifacts the decomposed child tasks wrote to their shared
    /// per-encode tempDir over to <paramref name="outputDirectory"/> on
    /// <paramref name="destinationStorage"/>, then clean up the tempDir.
    /// Called by the encode coordinator (VideoEncodeJob) exactly once after
    /// every child task has completed — per-task EncodeAsync calls
    /// intentionally skip the publish + cleanup steps so concurrent task
    /// finishers don't move each other's in-progress writes.
    /// </summary>
    Task PublishCachedArtifactsAsync(
        string outputDirectory,
        IStorage destinationStorage,
        IProgressObserver? progress = null,
        CancellationToken ct = default
    );
}
