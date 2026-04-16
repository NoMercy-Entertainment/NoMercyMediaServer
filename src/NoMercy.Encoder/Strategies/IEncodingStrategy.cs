namespace NoMercy.Encoder.Strategies;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Progress;

/// <summary>
/// Owns the full encode lifecycle for a single {OutputFormat, EncodeMode}
/// combination. The orchestrator resolves exactly one strategy per job based
/// on the profile's format + encode mode and hands the encode off to it.
///
/// Each strategy composes injectable building blocks (filter graph builder,
/// playlist generator, subtitle extractor, …) rather than baking format-specific
/// logic into the shared stages.
/// </summary>
public interface IEncodingStrategy
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
