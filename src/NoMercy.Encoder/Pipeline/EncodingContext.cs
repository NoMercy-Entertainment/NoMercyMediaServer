using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Metadata;
using NoMercy.Encoder.Naming;
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
/// falls back to SourceStorage (if set) or the DI singleton. Cross-backend
/// encodes (e.g. NFS source, S3 destination) are handled by EncodingOrchestrator
/// which stages the source via <c>AcquireLocalPathAsync</c> and stream-writes
/// outputs to the destination storage.
/// </param>
public record EncodingContext(
    string CorrelationId,
    MediaInfo? MediaInfo = null,
    IDecisionLogSink? Decisions = null,
    IStorage? SourceStorage = null,
    IStorage? DestinationStorage = null,
    MediaItemRef? MediaItem = null,
    /// <summary>
    /// Source-file stream metadata for copy-mode encodes. Populated by the
    /// caller (typically the job that dispatches the encode) from the
    /// MediaInfo stream objects. When null the merger falls back to DB-only.
    /// </summary>
    IReadOnlyList<SourceTrackMetadata>? SourceTracks = null,
    /// <summary>
    /// DB-side per-stream metadata rows. When set and SourceTracks is also set
    /// and the encode is copy-mode, BuildStage runs MetadataMerger before
    /// passing the result to MetadataInjector.
    /// </summary>
    IReadOnlyList<TrackMetadata>? DbTracks = null,
    string? OutputDirectory = null,
    string? InputPath = null
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
