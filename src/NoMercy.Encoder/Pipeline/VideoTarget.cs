namespace NoMercy.Encoder.Pipeline;

/// <summary>
/// Spec-shape for one video output variant. The dashboard renders each
/// <see cref="VideoTarget"/> as a card in the encoding plan preview so
/// users can confirm codec, resolution, and rate-control before a long
/// encode starts. Populated by <see cref="PlanResultProjector"/>; richer
/// fields (EncoderHandle, GpuIndex, FilterChain) fill in during Phase 3
/// when ExecutionPlan carries hardware-binding information.
/// </summary>
public sealed record VideoTarget(
    string Codec,
    string EncoderHandle,
    int? GpuIndex,
    int Width,
    int Height,
    RateControl RateControl,
    string Preset,
    string Profile,
    string Level,
    string PixelFormat,
    IReadOnlyList<string> FilterChain
);
