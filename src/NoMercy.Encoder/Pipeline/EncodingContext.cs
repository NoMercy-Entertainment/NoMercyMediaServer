using NoMercy.Encoder.Analysis;
using NoMercy.Storage;

namespace NoMercy.Encoder.Pipeline;

/// <summary>
/// Per-encode context threaded through every <see cref="IPipelineStage{TIn, TOut}"/>.
/// Carries the correlation ID, the analysed source metadata once available,
/// per-folder storage instances, and the <see cref="IDecisionLogSink"/> the
/// stages append to so the dashboard can render the per-job decision trace.
/// </summary>
/// <param name="CorrelationId">Stable per-encode ID used for log correlation.</param>
/// <param name="MediaInfo">Set by AnalyzeStage on success; null until then.</param>
/// <param name="Decisions">Sink for <see cref="DecisionLog"/> entries. Defaults to null (= no-op via <see cref="DecisionsOrNoOp"/>) so legacy callers don't have to opt in.</param>
/// <param name="SourceStorage">
/// Per-folder storage for the source input file. When null the stage falls
/// back to the app-level singleton injected through DI.
/// </param>
/// <param name="DestinationStorage">
/// Per-folder storage for the encode output directory. When null the stage
/// falls back to SourceStorage (if set) or the DI singleton.
/// TODO: cross-backend — when source and destination differ, stages that
/// read from source and write to destination must acquire a local path
/// from SourceStorage, encode, then stream-write to DestinationStorage.
/// </param>
public record EncodingContext(
    string CorrelationId,
    MediaInfo? MediaInfo = null,
    IDecisionLogSink? Decisions = null,
    IStorage? SourceStorage = null,
    IStorage? DestinationStorage = null
)
{
    /// <summary>
    /// Always non-null — falls back to a no-op sink when the caller
    /// passed nothing for <see cref="Decisions"/>.
    /// </summary>
    public IDecisionLogSink DecisionsOrNoOp => Decisions ?? NullDecisionLogSink.Instance;

    public static EncodingContext Create() =>
        new(CorrelationId: Ulid.NewUlid().ToString(), Decisions: new ScopedDecisionLog());
}
