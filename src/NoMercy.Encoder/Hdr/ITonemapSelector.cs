using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Pipeline;
using NoMercy.Storage;
using ProfileHdrOptions = NoMercy.Encoder.Profiles.HdrOptions;

namespace NoMercy.Encoder.Hdr;

public interface ITonemapSelector
{
    TonemapStrategy SelectBest(IHardwareCapabilities hardware, IFfmpegCapabilities? ffmpeg = null);

    /// <summary>
    /// Resolves the per-profile tonemap plan, honouring <see cref="ProfileHdrOptions"/>
    /// over the short-hand <paramref name="profileTonemapAlgorithm"/> field.
    /// Emits structured decisions to <paramref name="decisions"/> so the
    /// dashboard can show exactly which algorithm + nits were chosen and why.
    /// When <paramref name="storage"/> is non-null, a <see cref="ProfileHdrOptions.LutPath"/>
    /// is validated via <see cref="IStorage.AcquireLocalPath"/>; rejected paths
    /// fall back to the algorithm-based filter and emit a
    /// <c>plan.tonemap_lut_path_rejected</c> decision.
    /// </summary>
    TonemapPlan Build(
        ProfileHdrOptions? options,
        string? profileTonemapAlgorithm,
        IDecisionLogSink decisions,
        IStorage? storage = null
    );
}

/// <summary>
/// The full per-profile tonemap decision — ready to paste into a filter graph.
/// </summary>
/// <param name="Algorithm">Resolved algorithm name (always a known-good value).</param>
/// <param name="PeakNits">Nominal peak luminance passed to the <c>npl=</c> / <c>peak=</c> parameter.</param>
/// <param name="LutFilterChain">Non-null when a 3D LUT replaces the tonemap filter.</param>
/// <param name="FilterStringFragment">
/// Ready-to-paste FFmpeg filter fragment. Either a LUT chain or the full
/// <c>zscale…,tonemap=…</c> chain.
/// </param>
public sealed record TonemapPlan(
    string Algorithm,
    int PeakNits,
    string? LutFilterChain,
    string FilterStringFragment
);

public record TonemapStrategy(
    TonemapMethod Method,
    string FfmpegFilterChain,
    bool IsGpuAccelerated
);

public enum TonemapMethod
{
    Libplacebo,
    TonemapOpencl,
    ZscaleTonemap,
    CustomLut,
}
