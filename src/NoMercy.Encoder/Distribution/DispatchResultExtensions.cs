using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Pipeline;

namespace NoMercy.Encoder.Distribution;

/// <summary>
/// Projection helpers that map between <see cref="DispatchResult"/> (the
/// worker→coordinator wire response) and <see cref="EncodingResult"/> (the
/// orchestration-layer envelope). These exist so the dashboard sees the same
/// structured shape regardless of whether the encode ran locally or on a
/// remote worker.
///
/// <para>Mapping is ADDITIVE — fields present on <see cref="EncodingResult"/>
/// but absent from <see cref="DispatchResult"/> receive safe defaults, and the
/// wire format of <see cref="DispatchResult"/> is not changed.</para>
/// </summary>
public static class DispatchResultExtensions
{
    /// <summary>
    /// Projects a <see cref="DispatchResult"/> received from a remote worker
    /// into an <see cref="EncodingResult"/> so the orchestrator can treat local
    /// and remote outcomes uniformly. Enrichment fields (Artifacts, Stats,
    /// Plan, Warnings) are not available over the wire and are left at their
    /// defaults — the coordinator enriches them post-dispatch using the output
    /// directory it controls.
    /// </summary>
    public static EncodingResult ToEncodingResult(this DispatchResult dispatch)
    {
        bool succeeded = dispatch.Success;
        string status = succeeded ? "success" : "failed";

        EncoderErrorShape? enrichedError = null;
        EncodingError? legacyError = null;

        if (!succeeded && dispatch.Error is not null)
        {
            enrichedError = new EncoderErrorShape(
                Id: EncoderRuleId.EncoderInitFailed,
                Message: dispatch.Error,
                Suggestion: null,
                Details: dispatch.WorkerId is null ? null : new { Worker = dispatch.WorkerId }
            );
            legacyError = new EncodingError(
                Kind: EncodingErrorKind.Unknown,
                Message: dispatch.Error,
                FfmpegStderr: null,
                StageName: dispatch.WorkerId ?? "remote",
                Recoverable: false
            );
        }

        return new EncodingResult(
            Success: succeeded,
            OutputPath: dispatch.OutputPath,
            Duration: dispatch.Duration,
            Error: legacyError,
            Metrics: null
        )
        {
            Status = status,
            JobId = dispatch.TaskId,
            EnrichedError = enrichedError,
        };
    }

    /// <summary>
    /// Projects an <see cref="EncodingResult"/> back into a
    /// <see cref="DispatchResult"/> for the worker→coordinator response leg.
    /// The worker populates <paramref name="taskId"/> and optionally
    /// <paramref name="workerId"/> from its own context before returning.
    /// </summary>
    public static DispatchResult ToDispatchResult(
        this EncodingResult result,
        string taskId,
        string? workerId = null
    ) =>
        new(
            TaskId: taskId,
            Success: result.Success,
            OutputPath: result.OutputPath,
            Duration: result.Duration,
            Error: result.EnrichedError?.Message ?? result.Error?.Message,
            WorkerId: workerId
        );
}
