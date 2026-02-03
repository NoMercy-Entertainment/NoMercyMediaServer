using NoMercy.EncoderV2.Abstractions;

namespace NoMercy.EncoderV2.Codecs.Audio;

/// <summary>
/// AAC audio codec (native FFmpeg AAC encoder)
/// </summary>
public sealed class AacCodec : AudioCodecBase
{
    public override string Name => "aac";
    public override string DisplayName => "AAC";

    public override IReadOnlyList<string> AvailableChannelLayouts =>
    [
        "mono", "stereo", "2.1", "3.0", "3.1", "4.0", "quad", "4.1",
        "5.0", "5.1", "6.0", "6.1", "7.0", "7.1"
    ];

    public override IReadOnlyList<int> AvailableSampleRates =>
    [
        8000, 11025, 16000, 22050, 32000, 44100, 48000, 64000, 88200, 96000
    ];

    /// <summary>
    /// AAC profile (aac_low, aac_he, aac_he_v2, aac_ld, aac_eld)
    /// </summary>
    public string? Profile { get; set; }

    /// <summary>
    /// Use variable bitrate mode (1-5, higher = better quality)
    /// </summary>
    public int? Vbr { get; set; }

    public override IReadOnlyList<string> BuildArguments()
    {
        List<string> args = ["-c:a", Name];

        if (!string.IsNullOrEmpty(Profile))
        {
            args.AddRange(["-profile:a", Profile]);
        }

        if (Vbr.HasValue)
        {
            args.AddRange(["-q:a", Vbr.Value.ToString()]);
        }
        else if (Bitrate.HasValue)
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

        return args;
    }

    public override ValidationResult Validate()
    {
        ValidationResult baseResult = base.Validate();
        List<string> errors = [.. baseResult.Errors];
        List<string> warnings = [.. baseResult.Warnings];

        string[] validProfiles = ["aac_low", "aac_he", "aac_he_v2", "aac_ld", "aac_eld"];
        if (!string.IsNullOrEmpty(Profile) && !validProfiles.Contains(Profile))
        {
            errors.Add($"Invalid AAC profile '{Profile}'. Valid: {string.Join(", ", validProfiles)}");
        }

        if (Vbr.HasValue && (Vbr.Value < 1 || Vbr.Value > 5))
        {
            errors.Add("VBR quality must be between 1 and 5");
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
        AacCodec clone = new()
        {
            Profile = Profile,
            Vbr = Vbr
        };
        CopyPropertiesTo(clone);
        return clone;
    }
}

/// <summary>
/// Fraunhofer AAC encoder (libfdk_aac) - higher quality
/// </summary>
public sealed class FdkAacCodec : AudioCodecBase
{
    public override string Name => "libfdk_aac";
    public override string DisplayName => "AAC (FDK)";

    public override IReadOnlyList<string> AvailableChannelLayouts =>
    [
        "mono", "stereo", "2.1", "3.0", "3.1", "4.0", "quad", "4.1",
        "5.0", "5.1", "6.0", "6.1", "7.0", "7.1"
    ];

    public override IReadOnlyList<int> AvailableSampleRates =>
    [
        8000, 11025, 16000, 22050, 32000, 44100, 48000, 64000, 88200, 96000
    ];

    /// <summary>
    /// AAC profile (aac_low, aac_he, aac_he_v2, aac_ld, aac_eld)
    /// </summary>
    public string? Profile { get; set; }

    /// <summary>
    /// VBR mode (1-5)
    /// </summary>
    public int? Vbr { get; set; }

    /// <summary>
    /// Enable afterburner (improves quality at cost of speed)
    /// </summary>
    public bool Afterburner { get; set; } = true;

    public override IReadOnlyList<string> BuildArguments()
    {
        List<string> args = ["-c:a", Name];

        if (!string.IsNullOrEmpty(Profile))
        {
            args.AddRange(["-profile:a", Profile]);
        }

        if (Vbr.HasValue)
        {
            args.AddRange(["-vbr", Vbr.Value.ToString()]);
        }
        else if (Bitrate.HasValue)
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

        if (Afterburner)
        {
            args.AddRange(["-afterburner", "1"]);
        }

        return args;
    }

    public override IAudioCodec Clone()
    {
        FdkAacCodec clone = new()
        {
            Profile = Profile,
            Vbr = Vbr,
            Afterburner = Afterburner
        };
        CopyPropertiesTo(clone);
        return clone;
    }
}
