namespace NoMercy.Encoder.Profiles;

/// <summary>
/// Profile-level HDR tonemapping options. Algorithms map to the FFmpeg zscale/tonemap filter chain.
/// Supported algorithm names: hable | mobius | reinhard | clip | bt2390. Default is hable.
/// </summary>
public record HdrOptions(string TonemapAlgorithm, int TonemapPeakNits, string? LutPath = null);
