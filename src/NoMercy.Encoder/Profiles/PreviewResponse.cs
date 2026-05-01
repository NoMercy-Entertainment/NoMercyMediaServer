using NoMercy.Encoder.Errors;

namespace NoMercy.Encoder.Profiles;

/// <summary>
/// Full preview payload returned by <c>POST /api/v1/encoder/profiles/{id}/preview</c>.
/// The dashboard renders this as a pre-flight summary: stream table, warnings chip list,
/// and estimated encode time — letting the user validate the profile against a real
/// source file before committing to a full encode job.
/// </summary>
public sealed record PreviewResponse(
    /// <summary>Profile identifier echoed back so the dashboard can correlate the response.</summary>
    string ProfileId,
    /// <summary>Source path or video-file ID used for the analysis.</summary>
    string SourceVideoFileId,
    /// <summary>Key facts extracted from the source file by ffprobe.</summary>
    SourceAnalysisDto SourceAnalysis,
    /// <summary>Per-stream decisions the encoder will take when this profile is applied.</summary>
    PerStreamPlan PerStreamPlan,
    /// <summary>
    /// Source-level warnings (variable frame rate, Dolby Vision strip, upscaling).
    /// Non-blocking — the encode can still proceed; these surface in the dashboard warning chips.
    /// </summary>
    IReadOnlyList<EncoderRule> SourceWarnings,
    /// <summary>
    /// Estimated encode speed in frames per second.
    /// 0 until Phase 3.1 wires the real IQualityScaler + speed-index lookup.
    /// </summary>
    int EstimatedFps,
    /// <summary>
    /// Estimated output duration in seconds (equals source duration until Phase 3 estimates land).
    /// </summary>
    int EstimatedDurationSeconds,
    /// <summary>
    /// Encoder handle that will be selected for this job.
    /// <c>"auto"</c> until Phase 3 picks the real hardware/software encoder.
    /// </summary>
    string EncoderHandle
);

/// <summary>
/// Concise summary of the source file's technical properties.
/// Derived from <c>ffprobe</c> output via <see cref="NoMercy.Encoder.Analysis.MediaInfo"/>.
/// </summary>
public sealed record SourceAnalysisDto(
    /// <summary>Container format string as reported by ffprobe (e.g. <c>matroska,webm</c>).</summary>
    string Format,
    /// <summary>Total playback duration in seconds.</summary>
    double DurationSeconds,
    /// <summary>File size on disk in bytes.</summary>
    long FileSizeBytes,
    /// <summary>Overall container bitrate in kilobits per second.</summary>
    long OverallBitRateKbps,
    /// <summary>Number of video streams in the source container.</summary>
    int VideoStreamCount,
    /// <summary>Number of audio streams in the source container.</summary>
    int AudioStreamCount,
    /// <summary>Number of subtitle streams in the source container.</summary>
    int SubtitleStreamCount,
    /// <summary>Number of chapter markers embedded in the source.</summary>
    int ChapterCount,
    /// <summary>Number of attachment streams (fonts, cover art) in the source.</summary>
    int AttachmentCount,
    /// <summary>True when ffprobe detected Dolby Vision RPU metadata.</summary>
    bool HasDolbyVision,
    /// <summary>Codec name of the first video stream (e.g. <c>h264</c>, <c>hevc</c>).</summary>
    string? PrimaryVideoCodec,
    /// <summary>Display width of the first video stream in pixels.</summary>
    int? PrimaryVideoWidth,
    /// <summary>Display height of the first video stream in pixels.</summary>
    int? PrimaryVideoHeight
);
