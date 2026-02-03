using NoMercy.EncoderV2.Abstractions;

namespace NoMercy.EncoderV2.Codecs.Audio;

/// <summary>
/// Opus audio codec (libopus) - excellent quality at low bitrates
/// </summary>
public sealed class OpusCodec : AudioCodecBase
{
    public override string Name => "libopus";
    public override string DisplayName => "Opus";

    public override IReadOnlyList<string> AvailableChannelLayouts =>
    [
        "mono", "stereo", "3.0", "quad", "5.0", "5.1", "6.1", "7.1"
    ];

    public override IReadOnlyList<int> AvailableSampleRates =>
    [
        8000, 12000, 16000, 24000, 48000
    ];

    /// <summary>
    /// Application type (voip, audio, lowdelay)
    /// </summary>
    public string? Application { get; set; }

    /// <summary>
    /// Enable variable bitrate (default: on)
    /// </summary>
    public bool? Vbr { get; set; }

    /// <summary>
    /// Constrained VBR (cvbr)
    /// </summary>
    public bool ConstrainedVbr { get; set; }

    /// <summary>
    /// Compression level (0-10, higher = slower but better)
    /// </summary>
    public int? CompressionLevel { get; set; }

    /// <summary>
    /// Frame duration in ms (2.5, 5, 10, 20, 40, 60)
    /// </summary>
    public double? FrameDuration { get; set; }

    /// <summary>
    /// Packet loss percentage for encoding optimization (0-100)
    /// </summary>
    public int? PacketLoss { get; set; }

    public override IReadOnlyList<string> BuildArguments()
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

        if (!string.IsNullOrEmpty(Application))
        {
            args.AddRange(["-application", Application]);
        }

        if (Vbr.HasValue)
        {
            args.AddRange(["-vbr", Vbr.Value ? "on" : "off"]);
        }

        if (ConstrainedVbr)
        {
            args.AddRange(["-vbr", "constrained"]);
        }

        if (CompressionLevel.HasValue)
        {
            args.AddRange(["-compression_level", CompressionLevel.Value.ToString()]);
        }

        if (FrameDuration.HasValue)
        {
            args.AddRange(["-frame_duration", FrameDuration.Value.ToString("F1")]);
        }

        if (PacketLoss.HasValue)
        {
            args.AddRange(["-packet_loss", PacketLoss.Value.ToString()]);
        }

        return args;
    }

    public override ValidationResult Validate()
    {
        ValidationResult baseResult = base.Validate();
        List<string> errors = [.. baseResult.Errors];
        List<string> warnings = [.. baseResult.Warnings];

        string[] validApplications = ["voip", "audio", "lowdelay"];
        if (!string.IsNullOrEmpty(Application) && !validApplications.Contains(Application.ToLowerInvariant()))
        {
            errors.Add($"Invalid application '{Application}'. Valid: voip, audio, lowdelay");
        }

        if (CompressionLevel.HasValue && (CompressionLevel.Value < 0 || CompressionLevel.Value > 10))
        {
            errors.Add("Compression level must be between 0 and 10");
        }

        double[] validFrameDurations = [2.5, 5, 10, 20, 40, 60];
        if (FrameDuration.HasValue && !validFrameDurations.Contains(FrameDuration.Value))
        {
            errors.Add($"Frame duration must be one of: {string.Join(", ", validFrameDurations)}");
        }

        if (PacketLoss.HasValue && (PacketLoss.Value < 0 || PacketLoss.Value > 100))
        {
            errors.Add("Packet loss must be between 0 and 100");
        }

        if (errors.Count > 0)
        {
            return new ValidationResult { IsValid = false, Errors = errors, Warnings = warnings };
        }

        return warnings.Count > 0
            ? new ValidationResult { IsValid = true, Warnings = warnings }
            : ValidationResult.Success();
    }

    public override IAudioCodec Clone()
    {
        OpusCodec clone = new()
        {
            Application = Application,
            Vbr = Vbr,
            ConstrainedVbr = ConstrainedVbr,
            CompressionLevel = CompressionLevel,
            FrameDuration = FrameDuration,
            PacketLoss = PacketLoss
        };
        CopyPropertiesTo(clone);
        return clone;
    }
}
