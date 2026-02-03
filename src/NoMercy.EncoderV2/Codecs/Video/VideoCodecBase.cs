using NoMercy.EncoderV2.Abstractions;

namespace NoMercy.EncoderV2.Codecs.Video;

/// <summary>
/// Base class for video codecs with common functionality
/// </summary>
public abstract class VideoCodecBase : IVideoCodec
{
    public abstract string Name { get; }
    public abstract string DisplayName { get; }
    public CodecType Type => CodecType.Video;
    public virtual bool RequiresHardwareAcceleration => false;
    public virtual HardwareAcceleration? HardwareAccelerationType => null;

    public abstract IReadOnlyList<string> AvailablePresets { get; }
    public abstract IReadOnlyList<string> AvailableProfiles { get; }
    public abstract IReadOnlyList<string> AvailableTunes { get; }
    public abstract (int Min, int Max) CrfRange { get; }
    public abstract bool SupportsBFrames { get; }

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

    /// <summary>
    /// Additional codec-specific arguments
    /// </summary>
    protected Dictionary<string, string> AdditionalArguments { get; } = new();

    public virtual IReadOnlyList<string> BuildArguments()
    {
        List<string> args = ["-c:v", Name];

        // Preset
        if (!string.IsNullOrEmpty(Preset))
        {
            args.AddRange(["-preset", Preset]);
        }

        // Profile
        if (!string.IsNullOrEmpty(Profile))
        {
            args.AddRange(["-profile:v", Profile]);
        }

        // Tune
        if (!string.IsNullOrEmpty(Tune))
        {
            args.AddRange(["-tune", Tune]);
        }

        // Quality control - CRF or Bitrate
        if (Crf.HasValue)
        {
            args.AddRange(["-crf", Crf.Value.ToString()]);
        }
        else if (Bitrate.HasValue)
        {
            args.AddRange(["-b:v", $"{Bitrate.Value}k"]);
        }

        // Max bitrate and buffer size
        if (MaxBitrate.HasValue)
        {
            args.AddRange(["-maxrate", $"{MaxBitrate.Value}k"]);
        }

        if (BufferSize.HasValue)
        {
            args.AddRange(["-bufsize", $"{BufferSize.Value}k"]);
        }

        // Pixel format
        if (!string.IsNullOrEmpty(PixelFormat))
        {
            args.AddRange(["-pix_fmt", PixelFormat]);
        }

        // B-frames
        if (BFrames.HasValue && SupportsBFrames)
        {
            args.AddRange(["-bf", BFrames.Value.ToString()]);
        }

        // Keyframe interval
        if (KeyframeInterval.HasValue)
        {
            args.AddRange(["-g", KeyframeInterval.Value.ToString()]);
        }

        // Add codec-specific arguments
        foreach (KeyValuePair<string, string> kvp in AdditionalArguments)
        {
            args.AddRange([kvp.Key, kvp.Value]);
        }

        return args;
    }

    public virtual ValidationResult Validate()
    {
        List<string> errors = [];
        List<string> warnings = [];

        // Validate preset
        if (!string.IsNullOrEmpty(Preset) && !AvailablePresets.Contains(Preset))
        {
            errors.Add($"Invalid preset '{Preset}'. Available: {string.Join(", ", AvailablePresets)}");
        }

        // Validate profile
        if (!string.IsNullOrEmpty(Profile) && !AvailableProfiles.Contains(Profile))
        {
            errors.Add($"Invalid profile '{Profile}'. Available: {string.Join(", ", AvailableProfiles)}");
        }

        // Validate tune
        if (!string.IsNullOrEmpty(Tune) && !AvailableTunes.Contains(Tune))
        {
            errors.Add($"Invalid tune '{Tune}'. Available: {string.Join(", ", AvailableTunes)}");
        }

        // Validate CRF range
        if (Crf.HasValue && (Crf.Value < CrfRange.Min || Crf.Value > CrfRange.Max))
        {
            errors.Add($"CRF value {Crf.Value} is out of range ({CrfRange.Min}-{CrfRange.Max})");
        }

        // Validate B-frames
        if (BFrames.HasValue && !SupportsBFrames)
        {
            warnings.Add($"Codec {Name} does not support B-frames, setting will be ignored");
        }

        // Validate bitrate
        if (Bitrate.HasValue && Bitrate.Value <= 0)
        {
            errors.Add("Bitrate must be a positive value");
        }

        // Warning for both CRF and Bitrate
        if (Crf.HasValue && Bitrate.HasValue)
        {
            warnings.Add("Both CRF and bitrate are set. CRF will take precedence.");
        }

        if (errors.Count > 0)
        {
            return new ValidationResult { IsValid = false, Errors = errors, Warnings = warnings };
        }

        return warnings.Count > 0
            ? new ValidationResult { IsValid = true, Warnings = warnings }
            : ValidationResult.Success();
    }

    public abstract IVideoCodec Clone();

    protected void CopyPropertiesTo(VideoCodecBase target)
    {
        target.Preset = Preset;
        target.Profile = Profile;
        target.Tune = Tune;
        target.Crf = Crf;
        target.Bitrate = Bitrate;
        target.MaxBitrate = MaxBitrate;
        target.BufferSize = BufferSize;
        target.PixelFormat = PixelFormat;
        target.BFrames = BFrames;
        target.KeyframeInterval = KeyframeInterval;

        foreach (KeyValuePair<string, string> kvp in AdditionalArguments)
        {
            target.AdditionalArguments[kvp.Key] = kvp.Value;
        }
    }
}
