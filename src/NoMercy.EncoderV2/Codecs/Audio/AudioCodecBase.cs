using NoMercy.EncoderV2.Abstractions;

namespace NoMercy.EncoderV2.Codecs.Audio;

/// <summary>
/// Base class for audio codecs with common functionality
/// </summary>
public abstract class AudioCodecBase : IAudioCodec
{
    public abstract string Name { get; }
    public abstract string DisplayName { get; }
    public CodecType Type => CodecType.Audio;
    public virtual bool RequiresHardwareAcceleration => false;
    public virtual HardwareAcceleration? HardwareAccelerationType => null;

    public abstract IReadOnlyList<string> AvailableChannelLayouts { get; }
    public abstract IReadOnlyList<int> AvailableSampleRates { get; }

    public int? Bitrate { get; set; }
    public int? Channels { get; set; }
    public int? SampleRate { get; set; }
    public int? Quality { get; set; }

    /// <summary>
    /// Additional codec-specific arguments
    /// </summary>
    protected Dictionary<string, string> AdditionalArguments { get; } = new();

    public virtual IReadOnlyList<string> BuildArguments()
    {
        List<string> args = ["-c:a", Name];

        if (Bitrate.HasValue)
        {
            args.AddRange(["-b:a", $"{Bitrate.Value}k"]);
        }

        if (Channels.HasValue)
        {
            args.AddRange(["-ac", Channels.Value.ToString()]);
        }

        if (SampleRate.HasValue)
        {
            args.AddRange(["-ar", SampleRate.Value.ToString()]);
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

        // Validate sample rate
        if (SampleRate.HasValue && !AvailableSampleRates.Contains(SampleRate.Value))
        {
            warnings.Add($"Sample rate {SampleRate.Value} may not be optimal. Common rates: {string.Join(", ", AvailableSampleRates)}");
        }

        // Validate channels
        if (Channels.HasValue && Channels.Value <= 0)
        {
            errors.Add("Channels must be a positive value");
        }

        // Validate bitrate
        if (Bitrate.HasValue && Bitrate.Value <= 0)
        {
            errors.Add("Bitrate must be a positive value");
        }

        if (errors.Count > 0)
        {
            return new ValidationResult { IsValid = false, Errors = errors, Warnings = warnings };
        }

        return warnings.Count > 0
            ? new ValidationResult { IsValid = true, Warnings = warnings }
            : ValidationResult.Success();
    }

    public abstract IAudioCodec Clone();

    protected void CopyPropertiesTo(AudioCodecBase target)
    {
        target.Bitrate = Bitrate;
        target.Channels = Channels;
        target.SampleRate = SampleRate;
        target.Quality = Quality;

        foreach (KeyValuePair<string, string> kvp in AdditionalArguments)
        {
            target.AdditionalArguments[kvp.Key] = kvp.Value;
        }
    }
}
