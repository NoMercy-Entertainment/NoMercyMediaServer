namespace NoMercy.Encoder.Pipeline;

/// <summary>
/// One ABR variant in the encoding plan — combines the video target,
/// its paired audio tracks, and the HLS segmentation parameters. The
/// dashboard renders each <see cref="VariantPlan"/> as a row in the
/// variant table (e.g. "1080p · H.264 · CRF 23 · en/aac@192").
/// </summary>
public sealed record VariantPlan(
    string VariantId,
    VideoTarget Video,
    IReadOnlyList<AudioTarget> Audio,
    int SegmentDurationSeconds,
    int KeyframeIntervalSeconds
);
