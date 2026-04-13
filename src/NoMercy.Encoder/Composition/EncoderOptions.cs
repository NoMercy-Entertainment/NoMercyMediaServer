namespace NoMercy.Encoder.Composition;

public record EncoderOptions
{
    public string? FfmpegPathOverride { get; init; }
    public string? FfprobePathOverride { get; init; }
    public int DefaultSegmentDurationSeconds { get; init; } = 4;
    public int MaxBufferAheadSeconds { get; init; } = 30;
    public int MinBufferAheadSeconds { get; init; } = 15;
    public int ProgressThrottleMs { get; init; } = 500;
    public string? SpeedIndexCachePath { get; init; }
    public bool AutoCalibrate { get; init; } = true;
}
