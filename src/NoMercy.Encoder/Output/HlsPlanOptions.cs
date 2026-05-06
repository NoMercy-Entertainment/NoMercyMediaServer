namespace NoMercy.Encoder.Output;

/// <summary>
/// Resolved HLS muxer parameters that the output strategies need at FFmpeg
/// command-build time. Derived from a V2 <see cref="Profiles.HlsConfig"/>
/// plus the V2 <see cref="Profiles.Container"/> at PlanStage. Carrying
/// the strings here (instead of an enum + Container in OutputPlan) keeps
/// HlsOutputStrategy, PlaylistGenerator and the master playlist writer all
/// consuming the same flat shape they used pre-V2.
/// </summary>
public record HlsPlanOptions(
    string SegmentType = "mpegts",
    string PlaylistType = "vod",
    bool IndependentSegments = true
);
