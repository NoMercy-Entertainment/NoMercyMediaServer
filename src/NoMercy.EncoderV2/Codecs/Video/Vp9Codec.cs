using NoMercy.EncoderV2.Abstractions;

namespace NoMercy.EncoderV2.Codecs.Video;

/// <summary>
/// VP9 video codec (libvpx-vp9)
/// </summary>
public sealed class Vp9Codec : VideoCodecBase
{
    public override string Name => "libvpx-vp9";
    public override string DisplayName => "VP9";

    public override IReadOnlyList<string> AvailablePresets => [];

    public override IReadOnlyList<string> AvailableProfiles =>
    [
        "0", "1", "2", "3"
    ];

    public override IReadOnlyList<string> AvailableTunes => [];

    public override (int Min, int Max) CrfRange => (0, 63);
    public override bool SupportsBFrames => false;

    /// <summary>
    /// VP9 specific: Speed/quality tradeoff (0-5, lower is slower)
    /// </summary>
    public int? Speed { get; set; }

    /// <summary>
    /// VP9 specific: Deadline (good, best, realtime)
    /// </summary>
    public string? Deadline { get; set; }

    /// <summary>
    /// VP9 specific: Number of tile columns (power of 2)
    /// </summary>
    public int? TileColumns { get; set; }

    /// <summary>
    /// VP9 specific: Enable frame parallel decoding
    /// </summary>
    public bool FrameParallel { get; set; }

    /// <summary>
    /// VP9 specific: Enable row-based multithreading
    /// </summary>
    public bool RowMt { get; set; } = true;

    /// <summary>
    /// VP9 specific: Automatic alt reference frames
    /// </summary>
    public int? AutoAltRef { get; set; }

    /// <summary>
    /// VP9 specific: Number of lag frames for auto-alt-ref
    /// </summary>
    public int? LagInFrames { get; set; }

    public override IReadOnlyList<string> BuildArguments()
    {
        List<string> args = ["-c:v", Name];

        if (Crf.HasValue)
        {
            args.AddRange(["-crf", Crf.Value.ToString()]);
            // VP9 needs -b:v 0 to enable pure CRF mode
            args.AddRange(["-b:v", "0"]);
        }
        else if (Bitrate.HasValue)
        {
            args.AddRange(["-b:v", $"{Bitrate.Value}k"]);
        }

        if (Speed.HasValue)
        {
            args.AddRange(["-speed", Speed.Value.ToString()]);
        }

        if (!string.IsNullOrEmpty(Deadline))
        {
            args.AddRange(["-deadline", Deadline]);
        }

        if (TileColumns.HasValue)
        {
            args.AddRange(["-tile-columns", TileColumns.Value.ToString()]);
        }

        if (FrameParallel)
        {
            args.AddRange(["-frame-parallel", "1"]);
        }

        if (RowMt)
        {
            args.AddRange(["-row-mt", "1"]);
        }

        if (AutoAltRef.HasValue)
        {
            args.AddRange(["-auto-alt-ref", AutoAltRef.Value.ToString()]);
        }

        if (LagInFrames.HasValue)
        {
            args.AddRange(["-lag-in-frames", LagInFrames.Value.ToString()]);
        }

        if (!string.IsNullOrEmpty(PixelFormat))
        {
            args.AddRange(["-pix_fmt", PixelFormat]);
        }

        if (KeyframeInterval.HasValue)
        {
            args.AddRange(["-g", KeyframeInterval.Value.ToString()]);
        }

        return args;
    }

    public override ValidationResult Validate()
    {
        ValidationResult baseResult = base.Validate();
        List<string> errors = [.. baseResult.Errors];
        List<string> warnings = [.. baseResult.Warnings];

        // Validate speed
        if (Speed.HasValue && (Speed.Value < 0 || Speed.Value > 5))
        {
            errors.Add("Speed must be between 0 and 5");
        }

        // Validate deadline
        string[] validDeadlines = ["good", "best", "realtime"];
        if (!string.IsNullOrEmpty(Deadline) && !validDeadlines.Contains(Deadline.ToLowerInvariant()))
        {
            errors.Add($"Invalid deadline '{Deadline}'. Valid: good, best, realtime");
        }

        if (errors.Count > 0)
        {
            return new ValidationResult { IsValid = false, Errors = errors, Warnings = warnings };
        }

        return warnings.Count > 0
            ? new ValidationResult { IsValid = true, Warnings = warnings }
            : ValidationResult.Success();
    }

    public override IVideoCodec Clone()
    {
        Vp9Codec clone = new()
        {
            Speed = Speed,
            Deadline = Deadline,
            TileColumns = TileColumns,
            FrameParallel = FrameParallel,
            RowMt = RowMt,
            AutoAltRef = AutoAltRef,
            LagInFrames = LagInFrames
        };
        CopyPropertiesTo(clone);
        return clone;
    }
}

/// <summary>
/// Copy video codec (stream copy, no re-encoding)
/// </summary>
public sealed class VideoCopyCodec : IVideoCodec
{
    public string Name => "copy";
    public string DisplayName => "Copy (No Re-encoding)";
    public CodecType Type => CodecType.Video;
    public bool RequiresHardwareAcceleration => false;
    public HardwareAcceleration? HardwareAccelerationType => null;

    public IReadOnlyList<string> AvailablePresets => [];
    public IReadOnlyList<string> AvailableProfiles => [];
    public IReadOnlyList<string> AvailableTunes => [];
    public (int Min, int Max) CrfRange => (0, 0);
    public bool SupportsBFrames => false;

    public string? Preset { get; set; }
    public string? Profile { get; set; }
    public string? Tune { get; set; }
    public int? Crf { get; set; }
    public int? Bitrate { get; set; }
    public int? MaxBitrate { get; set; }
    public int? BufferSize { get; set; }
    public string? PixelFormat { get; set; }
    public int? BFrames { get; set; }
    public int? KeyframeInterval { get; set; }

    public IReadOnlyList<string> BuildArguments() => ["-c:v", "copy"];

    public ValidationResult Validate() => ValidationResult.Success();

    public IVideoCodec Clone() => new VideoCopyCodec();
}
