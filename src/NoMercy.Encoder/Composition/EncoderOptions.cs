namespace NoMercy.Encoder.Composition;

public class EncoderOptions
{
    public string? FfmpegPathOverride { get; set; }
    public string? FfprobePathOverride { get; set; }

    /// <summary>
    /// Resolved FFmpeg path: uses the configured override (set via AppFiles.FfmpegPath at startup).
    /// Throws if not configured — callers must register via AddNoMercyEncoder with the path set.
    /// </summary>
    public string FfmpegPath =>
        FfmpegPathOverride
        ?? throw new InvalidOperationException(
            "FfmpegPathOverride not configured. Set it via AddNoMercyEncoder()."
        );

    /// <summary>
    /// Resolved FFprobe path: uses the configured override (set via AppFiles.FfProbePath at startup).
    /// Throws if not configured — callers must register via AddNoMercyEncoder with the path set.
    /// </summary>
    public string FfprobePath =>
        FfprobePathOverride
        ?? throw new InvalidOperationException(
            "FfprobePathOverride not configured. Set it via AddNoMercyEncoder()."
        );
    public int DefaultSegmentDurationSeconds { get; set; } = 4;
    public int MaxBufferAheadSeconds { get; set; } = 30;
    public int MinBufferAheadSeconds { get; set; } = 15;
    public int ProgressThrottleMs { get; set; } = 500;
    public string? SpeedIndexCachePath { get; set; }
    public bool AutoCalibrate { get; set; } = true;
}
