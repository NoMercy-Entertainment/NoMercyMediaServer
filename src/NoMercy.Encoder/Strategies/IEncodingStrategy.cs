namespace NoMercy.Encoder.Strategies;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.Progress;

/// <summary>
/// Optional per-strategy stage overrides. Return null for a role to keep the
/// pipeline default; return a custom instance to replace that stage for this
/// strategy's encodes only. Default implementations return null for everything
/// so existing strategies require no changes.
/// </summary>
public interface IStageOverrides
{
    /// <summary>Gets a custom validation stage, or null to use the pipeline default.</summary>
    IValidationStage? Validation => null;

    /// <summary>Gets a custom analysis stage, or null to use the pipeline default.</summary>
    IAnalysisStage? Analysis => null;

    /// <summary>Gets a custom planning stage, or null to use the pipeline default.</summary>
    IPlanStage? Plan => null;

    /// <summary>Gets a custom build stage, or null to use the pipeline default.</summary>
    IBuildStage? Build => null;

    /// <summary>Gets a custom execution stage, or null to use the pipeline default.</summary>
    IExecutionStage? Execution => null;

    /// <summary>Gets a custom finalization stage, or null to use the pipeline default.</summary>
    IFinalizeStage? Finalize => null;
}

/// <summary>
/// Owns the full encode lifecycle for a single {OutputFormat, EncodeMode}
/// combination. The orchestrator resolves exactly one strategy per job based
/// on the profile's format + encode mode and hands the encode off to it.
///
/// Each strategy composes injectable building blocks (filter graph builder,
/// playlist generator, subtitle extractor, …) rather than baking format-specific
/// logic into the shared stages.
///
/// Extends <see cref="IStageOverrides"/> so every strategy implicitly carries
/// the hook. All override properties default to null — existing strategies
/// require zero changes.
/// </summary>
public interface IEncodingStrategy : IStageOverrides
{
    /// <summary>
    /// Output container this strategy produces (HLS, MKV, MP4, DASH, …).
    /// </summary>
    OutputFormat Format { get; }

    /// <summary>
    /// Encode mode this strategy handles (SinglePass, TwoPass).
    /// </summary>
    EncodeMode EncodeMode { get; }

    /// <summary>
    /// Execute the full encode. Strategies MAY early-return with a
    /// <see cref="StreamAction.Drop"/>-style result when the request is
    /// incompatible — validation lives upstream in the orchestrator.
    /// </summary>
    Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        IProgressObserver? progress,
        CancellationToken ct
    );
}
