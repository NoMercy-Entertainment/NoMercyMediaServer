using NoMercy.EncoderV2.Abstractions;

namespace NoMercy.EncoderV2.Codecs.Audio;

/// <summary>
/// FLAC audio codec (lossless compression)
/// </summary>
public sealed class FlacCodec : AudioCodecBase
{
    public override string Name => "flac";
    public override string DisplayName => "FLAC (Lossless)";

    public override IReadOnlyList<string> AvailableChannelLayouts =>
    [
        "mono", "stereo", "2.1", "3.0", "3.1", "4.0", "quad", "4.1",
        "5.0", "5.1", "6.0", "6.1", "7.0", "7.1"
    ];

    public override IReadOnlyList<int> AvailableSampleRates =>
    [
        8000, 11025, 16000, 22050, 32000, 44100, 48000, 88200, 96000, 176400, 192000
    ];

    /// <summary>
    /// Compression level (0-12, higher = smaller but slower)
    /// </summary>
    public int? CompressionLevel { get; set; }

    /// <summary>
    /// Bits per sample (16, 24, 32)
    /// </summary>
    public int? BitsPerSample { get; set; }

    /// <summary>
    /// Block size in samples
    /// </summary>
    public int? BlockSize { get; set; }

    /// <summary>
    /// Enable precise LPC coefficient quantization
    /// </summary>
    public bool? PreciseLpc { get; set; }

    public override IReadOnlyList<string> BuildArguments()
    {
        List<string> args = ["-c:a", Name];

        if (CompressionLevel.HasValue)
        {
            args.AddRange(["-compression_level", CompressionLevel.Value.ToString()]);
        }

        if (Channels.HasValue)
        {
            args.AddRange(["-ac", Channels.Value.ToString()]);
        }

        if (SampleRate.HasValue)
        {
            args.AddRange(["-ar", SampleRate.Value.ToString()]);
        }

        if (BitsPerSample.HasValue)
        {
            args.AddRange(["-sample_fmt", BitsPerSample.Value switch
            {
                16 => "s16",
                24 => "s32", // FFmpeg uses s32 for 24-bit
                32 => "s32",
                _ => "s16"
            }]);
        }

        if (BlockSize.HasValue)
        {
            args.AddRange(["-block_size", BlockSize.Value.ToString()]);
        }

        if (PreciseLpc.HasValue)
        {
            args.AddRange(["-lpc_type", PreciseLpc.Value ? "cholesky" : "levinson"]);
        }

        return args;
    }

    public override ValidationResult Validate()
    {
        ValidationResult baseResult = base.Validate();
        List<string> errors = [.. baseResult.Errors];
        List<string> warnings = [.. baseResult.Warnings];

        if (CompressionLevel.HasValue && (CompressionLevel.Value < 0 || CompressionLevel.Value > 12))
        {
            errors.Add("Compression level must be between 0 and 12");
        }

        int[] validBits = [16, 24, 32];
        if (BitsPerSample.HasValue && !validBits.Contains(BitsPerSample.Value))
        {
            errors.Add($"Bits per sample must be one of: {string.Join(", ", validBits)}");
        }

        if (Bitrate.HasValue)
        {
            warnings.Add("FLAC is a lossless codec; bitrate setting will be ignored");
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
        FlacCodec clone = new()
        {
            CompressionLevel = CompressionLevel,
            BitsPerSample = BitsPerSample,
            BlockSize = BlockSize,
            PreciseLpc = PreciseLpc
        };
        CopyPropertiesTo(clone);
        return clone;
    }
}

/// <summary>
/// MP3 audio codec (libmp3lame)
/// </summary>
public sealed class Mp3Codec : AudioCodecBase
{
    public override string Name => "libmp3lame";
    public override string DisplayName => "MP3";

    public override IReadOnlyList<string> AvailableChannelLayouts =>
    [
        "mono", "stereo"
    ];

    public override IReadOnlyList<int> AvailableSampleRates =>
    [
        8000, 11025, 12000, 16000, 22050, 24000, 32000, 44100, 48000
    ];

    /// <summary>
    /// VBR quality (0-9, lower = better quality, higher bitrate)
    /// </summary>
    public int? VbrQuality { get; set; }

    /// <summary>
    /// Joint stereo mode
    /// </summary>
    public bool JointStereo { get; set; } = true;

    /// <summary>
    /// Compression level (0-9, lower = better quality but slower)
    /// </summary>
    public int? CompressionLevel { get; set; }

    public override IReadOnlyList<string> BuildArguments()
    {
        List<string> args = ["-c:a", Name];

        if (VbrQuality.HasValue)
        {
            args.AddRange(["-q:a", VbrQuality.Value.ToString()]);
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

        if (!JointStereo)
        {
            args.AddRange(["-joint_stereo", "0"]);
        }

        if (CompressionLevel.HasValue)
        {
            args.AddRange(["-compression_level", CompressionLevel.Value.ToString()]);
        }

        return args;
    }

    public override ValidationResult Validate()
    {
        ValidationResult baseResult = base.Validate();
        List<string> errors = [.. baseResult.Errors];
        List<string> warnings = [.. baseResult.Warnings];

        if (VbrQuality.HasValue && (VbrQuality.Value < 0 || VbrQuality.Value > 9))
        {
            errors.Add("VBR quality must be between 0 and 9");
        }

        if (CompressionLevel.HasValue && (CompressionLevel.Value < 0 || CompressionLevel.Value > 9))
        {
            errors.Add("Compression level must be between 0 and 9");
        }

        if (Channels.HasValue && Channels.Value > 2)
        {
            errors.Add("MP3 only supports mono or stereo audio");
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
        Mp3Codec clone = new()
        {
            VbrQuality = VbrQuality,
            JointStereo = JointStereo,
            CompressionLevel = CompressionLevel
        };
        CopyPropertiesTo(clone);
        return clone;
    }
}

/// <summary>
/// Vorbis audio codec (libvorbis)
/// </summary>
public sealed class VorbisCodec : AudioCodecBase
{
    public override string Name => "libvorbis";
    public override string DisplayName => "Vorbis";

    public override IReadOnlyList<string> AvailableChannelLayouts =>
    [
        "mono", "stereo", "2.1", "3.0", "3.1", "4.0", "quad", "4.1",
        "5.0", "5.1", "6.0", "6.1", "7.0", "7.1"
    ];

    public override IReadOnlyList<int> AvailableSampleRates =>
    [
        8000, 11025, 16000, 22050, 32000, 44100, 48000, 88200, 96000, 176400, 192000
    ];

    /// <summary>
    /// VBR quality (-1 to 10, higher = better quality)
    /// </summary>
    public double? VbrQuality { get; set; }

    public override IReadOnlyList<string> BuildArguments()
    {
        List<string> args = ["-c:a", Name];

        if (VbrQuality.HasValue)
        {
            args.AddRange(["-q:a", VbrQuality.Value.ToString("F1")]);
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

        if (VbrQuality.HasValue && (VbrQuality.Value < -1 || VbrQuality.Value > 10))
        {
            errors.Add("VBR quality must be between -1 and 10");
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
        VorbisCodec clone = new()
        {
            VbrQuality = VbrQuality
        };
        CopyPropertiesTo(clone);
        return clone;
    }
}

/// <summary>
/// Copy audio codec (stream copy, no re-encoding)
/// </summary>
public sealed class AudioCopyCodec : IAudioCodec
{
    public string Name => "copy";
    public string DisplayName => "Copy (No Re-encoding)";
    public CodecType Type => CodecType.Audio;
    public bool RequiresHardwareAcceleration => false;
    public HardwareAcceleration? HardwareAccelerationType => null;

    public IReadOnlyList<string> AvailableChannelLayouts => [];
    public IReadOnlyList<int> AvailableSampleRates => [];

    public int? Bitrate { get; set; }
    public int? Channels { get; set; }
    public int? SampleRate { get; set; }
    public int? Quality { get; set; }

    public IReadOnlyList<string> BuildArguments() => ["-c:a", "copy"];

    public ValidationResult Validate() => ValidationResult.Success();

    public IAudioCodec Clone() => new AudioCopyCodec();
}
