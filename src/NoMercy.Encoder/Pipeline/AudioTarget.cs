namespace NoMercy.Encoder.Pipeline;

/// <summary>
/// Spec-shape for one audio output within a variant. The dashboard shows
/// language, codec, and bitrate alongside the video card so users can see
/// the full per-variant stream list at a glance.
/// </summary>
public sealed record AudioTarget(
    int SourceIndex,
    string Codec,
    int Channels,
    int BitrateKbps,
    string? Language
);
