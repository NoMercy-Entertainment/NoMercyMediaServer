using NoMercy.EncoderV2.Abstractions;

namespace NoMercy.EncoderV2.Codecs.Audio;

/// <summary>
/// AC-3 (Dolby Digital) audio codec
/// </summary>
public sealed class Ac3Codec : AudioCodecBase
{
    public override string Name => "ac3";
    public override string DisplayName => "AC-3 (Dolby Digital)";

    public override IReadOnlyList<string> AvailableChannelLayouts =>
    [
        "mono", "stereo", "2.1", "3.0", "3.1", "4.0", "quad", "4.1", "5.0", "5.1"
    ];

    public override IReadOnlyList<int> AvailableSampleRates =>
    [
        32000, 44100, 48000
    ];

    /// <summary>
    /// Room type (notindicated, large, small)
    /// </summary>
    public string? RoomType { get; set; }

    /// <summary>
    /// Mixing level (-1 to 111)
    /// </summary>
    public int? MixingLevel { get; set; }

    /// <summary>
    /// Copyright bit
    /// </summary>
    public bool? Copyright { get; set; }

    /// <summary>
    /// Dialog normalization (-31 to -1)
    /// </summary>
    public int? DialogNormalization { get; set; }

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

        if (!string.IsNullOrEmpty(RoomType))
        {
            args.AddRange(["-room_type", RoomType]);
        }

        if (MixingLevel.HasValue)
        {
            args.AddRange(["-mixing_level", MixingLevel.Value.ToString()]);
        }

        if (Copyright.HasValue)
        {
            args.AddRange(["-copyright", Copyright.Value ? "1" : "0"]);
        }

        if (DialogNormalization.HasValue)
        {
            args.AddRange(["-dialnorm", DialogNormalization.Value.ToString()]);
        }

        return args;
    }

    public override IAudioCodec Clone()
    {
        Ac3Codec clone = new()
        {
            RoomType = RoomType,
            MixingLevel = MixingLevel,
            Copyright = Copyright,
            DialogNormalization = DialogNormalization
        };
        CopyPropertiesTo(clone);
        return clone;
    }
}

/// <summary>
/// E-AC-3 (Dolby Digital Plus) audio codec
/// </summary>
public sealed class Eac3Codec : AudioCodecBase
{
    public override string Name => "eac3";
    public override string DisplayName => "E-AC-3 (Dolby Digital Plus)";

    public override IReadOnlyList<string> AvailableChannelLayouts =>
    [
        "mono", "stereo", "2.1", "3.0", "3.1", "4.0", "quad", "4.1",
        "5.0", "5.1", "6.0", "6.1", "7.0", "7.1"
    ];

    public override IReadOnlyList<int> AvailableSampleRates =>
    [
        32000, 44100, 48000
    ];

    /// <summary>
    /// Room type (notindicated, large, small)
    /// </summary>
    public string? RoomType { get; set; }

    /// <summary>
    /// Dialog normalization (-31 to -1)
    /// </summary>
    public int? DialogNormalization { get; set; }

    /// <summary>
    /// Surround downmix mode
    /// </summary>
    public string? SurroundDownmix { get; set; }

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

        if (!string.IsNullOrEmpty(RoomType))
        {
            args.AddRange(["-room_type", RoomType]);
        }

        if (DialogNormalization.HasValue)
        {
            args.AddRange(["-dialnorm", DialogNormalization.Value.ToString()]);
        }

        if (!string.IsNullOrEmpty(SurroundDownmix))
        {
            args.AddRange(["-dsurex_mode", SurroundDownmix]);
        }

        return args;
    }

    public override IAudioCodec Clone()
    {
        Eac3Codec clone = new()
        {
            RoomType = RoomType,
            DialogNormalization = DialogNormalization,
            SurroundDownmix = SurroundDownmix
        };
        CopyPropertiesTo(clone);
        return clone;
    }
}
