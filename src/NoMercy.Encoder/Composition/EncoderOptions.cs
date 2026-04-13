namespace NoMercy.Encoder.Composition;

public class EncoderOptions
{
    public string? FfmpegPathOverride { get; set; }
    public string? FfprobePathOverride { get; set; }
    public int DefaultSegmentDurationSeconds { get; set; } = 4;
    public int MaxBufferAheadSeconds { get; set; } = 30;
    public int MinBufferAheadSeconds { get; set; } = 15;
    public int ProgressThrottleMs { get; set; } = 500;
    public string? SpeedIndexCachePath { get; set; }
    public bool AutoCalibrate { get; set; } = true;
}
