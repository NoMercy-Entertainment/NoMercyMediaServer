using NoMercy.EncoderV2.Abstractions;

namespace NoMercy.EncoderV2.Codecs.Video;

/// <summary>
/// H.265/HEVC video codec (libx265)
/// </summary>
public sealed class H265Codec : VideoCodecBase
{
    public override string Name => "libx265";
    public override string DisplayName => "H.265 (HEVC)";

    public override IReadOnlyList<string> AvailablePresets =>
    [
        "ultrafast", "superfast", "veryfast", "faster", "fast",
        "medium", "slow", "slower", "veryslow", "placebo"
    ];

    public override IReadOnlyList<string> AvailableProfiles =>
    [
        "main", "main10", "main12", "main422-10", "main422-12", "main444-8", "main444-10", "main444-12"
    ];

    public override IReadOnlyList<string> AvailableTunes =>
    [
        "grain", "animation", "psnr", "ssim", "fastdecode", "zerolatency"
    ];

    public override (int Min, int Max) CrfRange => (0, 51);
    public override bool SupportsBFrames => true;

    /// <summary>
    /// x265 specific: Level (e.g., "4.0", "5.0", "5.1")
    /// </summary>
    public string? Level { get; set; }

    /// <summary>
    /// x265 specific: x265-params for advanced settings
    /// </summary>
    public string? X265Params { get; set; }

    /// <summary>
    /// x265 specific: Enable HDR metadata passthrough
    /// </summary>
    public bool CopyHdrMetadata { get; set; }

    public override IReadOnlyList<string> BuildArguments()
    {
        List<string> args = ["-c:v", Name];

        if (!string.IsNullOrEmpty(Preset))
        {
            args.AddRange(["-preset", Preset]);
        }

        if (!string.IsNullOrEmpty(Profile))
        {
            args.AddRange(["-profile:v", Profile]);
        }

        if (!string.IsNullOrEmpty(Tune))
        {
            args.AddRange(["-tune", Tune]);
        }

        if (Crf.HasValue)
        {
            args.AddRange(["-crf", Crf.Value.ToString()]);
        }
        else if (Bitrate.HasValue)
        {
            args.AddRange(["-b:v", $"{Bitrate.Value}k"]);
        }

        if (MaxBitrate.HasValue)
        {
            args.AddRange(["-maxrate", $"{MaxBitrate.Value}k"]);
        }

        if (BufferSize.HasValue)
        {
            args.AddRange(["-bufsize", $"{BufferSize.Value}k"]);
        }

        if (!string.IsNullOrEmpty(PixelFormat))
        {
            args.AddRange(["-pix_fmt", PixelFormat]);
        }

        if (BFrames.HasValue)
        {
            args.AddRange(["-bf", BFrames.Value.ToString()]);
        }

        if (KeyframeInterval.HasValue)
        {
            args.AddRange(["-g", KeyframeInterval.Value.ToString()]);
        }

        // Build x265-params
        List<string> x265Params = [];

        if (!string.IsNullOrEmpty(Level))
        {
            x265Params.Add($"level={Level}");
        }

        if (CopyHdrMetadata)
        {
            x265Params.Add("hdr-opt=1");
            x265Params.Add("repeat-headers=1");
        }

        if (!string.IsNullOrEmpty(X265Params))
        {
            x265Params.Add(X265Params);
        }

        if (x265Params.Count > 0)
        {
            args.AddRange(["-x265-params", string.Join(":", x265Params)]);
        }

        // Required for web playback
        args.AddRange(["-tag:v", "hvc1"]);

        return args;
    }

    public override IVideoCodec Clone()
    {
        H265Codec clone = new()
        {
            Level = Level,
            X265Params = X265Params,
            CopyHdrMetadata = CopyHdrMetadata
        };
        CopyPropertiesTo(clone);
        return clone;
    }
}

/// <summary>
/// H.265 NVENC (NVIDIA hardware encoder)
/// </summary>
public sealed class H265NvencCodec : VideoCodecBase
{
    public override string Name => "hevc_nvenc";
    public override string DisplayName => "H.265 (NVENC)";
    public override bool RequiresHardwareAcceleration => true;
    public override HardwareAcceleration? HardwareAccelerationType => HardwareAcceleration.Nvenc;

    public override IReadOnlyList<string> AvailablePresets =>
    [
        "default", "slow", "medium", "fast", "hp", "hq", "bd", "ll", "llhq", "llhp",
        "lossless", "losslesshp", "p1", "p2", "p3", "p4", "p5", "p6", "p7"
    ];

    public override IReadOnlyList<string> AvailableProfiles =>
    [
        "main", "main10", "rext"
    ];

    public override IReadOnlyList<string> AvailableTunes => [];

    public override (int Min, int Max) CrfRange => (0, 51);
    public override bool SupportsBFrames => true;

    /// <summary>
    /// NVENC specific: Rate control mode
    /// </summary>
    public string? RcMode { get; set; }

    /// <summary>
    /// NVENC specific: Constant quality mode
    /// </summary>
    public int? Cq { get; set; }

    /// <summary>
    /// NVENC specific: GPU index to use
    /// </summary>
    public int? GpuIndex { get; set; }

    /// <summary>
    /// NVENC specific: Enable B-frame as reference
    /// </summary>
    public bool BRefMode { get; set; }

    public override IReadOnlyList<string> BuildArguments()
    {
        List<string> args = ["-c:v", Name];

        if (!string.IsNullOrEmpty(Preset))
        {
            args.AddRange(["-preset", Preset]);
        }

        if (!string.IsNullOrEmpty(Profile))
        {
            args.AddRange(["-profile:v", Profile]);
        }

        if (!string.IsNullOrEmpty(RcMode))
        {
            args.AddRange(["-rc", RcMode]);
        }

        if (Cq.HasValue)
        {
            args.AddRange(["-cq", Cq.Value.ToString()]);
        }
        else if (Crf.HasValue)
        {
            args.AddRange(["-cq", Crf.Value.ToString()]);
        }

        if (Bitrate.HasValue)
        {
            args.AddRange(["-b:v", $"{Bitrate.Value}k"]);
        }

        if (MaxBitrate.HasValue)
        {
            args.AddRange(["-maxrate", $"{MaxBitrate.Value}k"]);
        }

        if (BufferSize.HasValue)
        {
            args.AddRange(["-bufsize", $"{BufferSize.Value}k"]);
        }

        if (!string.IsNullOrEmpty(PixelFormat))
        {
            args.AddRange(["-pix_fmt", PixelFormat]);
        }

        if (BFrames.HasValue)
        {
            args.AddRange(["-bf", BFrames.Value.ToString()]);
        }

        if (BRefMode)
        {
            args.AddRange(["-b_ref_mode", "middle"]);
        }

        if (KeyframeInterval.HasValue)
        {
            args.AddRange(["-g", KeyframeInterval.Value.ToString()]);
        }

        if (GpuIndex.HasValue)
        {
            args.AddRange(["-gpu", GpuIndex.Value.ToString()]);
        }

        // Required for web playback
        args.AddRange(["-tag:v", "hvc1"]);

        return args;
    }

    public override IVideoCodec Clone()
    {
        H265NvencCodec clone = new()
        {
            RcMode = RcMode,
            Cq = Cq,
            GpuIndex = GpuIndex,
            BRefMode = BRefMode
        };
        CopyPropertiesTo(clone);
        return clone;
    }
}

/// <summary>
/// H.265 QSV (Intel Quick Sync)
/// </summary>
public sealed class H265QsvCodec : VideoCodecBase
{
    public override string Name => "hevc_qsv";
    public override string DisplayName => "H.265 (QSV)";
    public override bool RequiresHardwareAcceleration => true;
    public override HardwareAcceleration? HardwareAccelerationType => HardwareAcceleration.Qsv;

    public override IReadOnlyList<string> AvailablePresets =>
    [
        "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"
    ];

    public override IReadOnlyList<string> AvailableProfiles =>
    [
        "main", "main10"
    ];

    public override IReadOnlyList<string> AvailableTunes => [];

    public override (int Min, int Max) CrfRange => (1, 51);
    public override bool SupportsBFrames => true;

    public int? GlobalQuality { get; set; }

    public override IReadOnlyList<string> BuildArguments()
    {
        List<string> args = ["-c:v", Name];

        if (!string.IsNullOrEmpty(Preset))
        {
            args.AddRange(["-preset", Preset]);
        }

        if (!string.IsNullOrEmpty(Profile))
        {
            args.AddRange(["-profile:v", Profile]);
        }

        if (GlobalQuality.HasValue)
        {
            args.AddRange(["-global_quality", GlobalQuality.Value.ToString()]);
        }
        else if (Crf.HasValue)
        {
            args.AddRange(["-global_quality", Crf.Value.ToString()]);
        }

        if (Bitrate.HasValue)
        {
            args.AddRange(["-b:v", $"{Bitrate.Value}k"]);
        }

        if (MaxBitrate.HasValue)
        {
            args.AddRange(["-maxrate", $"{MaxBitrate.Value}k"]);
        }

        if (!string.IsNullOrEmpty(PixelFormat))
        {
            args.AddRange(["-pix_fmt", PixelFormat]);
        }

        if (KeyframeInterval.HasValue)
        {
            args.AddRange(["-g", KeyframeInterval.Value.ToString()]);
        }

        // Required for web playback
        args.AddRange(["-tag:v", "hvc1"]);

        return args;
    }

    public override IVideoCodec Clone()
    {
        H265QsvCodec clone = new()
        {
            GlobalQuality = GlobalQuality
        };
        CopyPropertiesTo(clone);
        return clone;
    }
}

/// <summary>
/// H.265 VideoToolbox (macOS hardware encoder)
/// </summary>
public sealed class H265VideoToolboxCodec : VideoCodecBase
{
    public override string Name => "hevc_videotoolbox";
    public override string DisplayName => "H.265 (VideoToolbox)";
    public override bool RequiresHardwareAcceleration => true;
    public override HardwareAcceleration? HardwareAccelerationType => HardwareAcceleration.VideoToolbox;

    public override IReadOnlyList<string> AvailablePresets => [];

    public override IReadOnlyList<string> AvailableProfiles =>
    [
        "main", "main10"
    ];

    public override IReadOnlyList<string> AvailableTunes => [];

    public override (int Min, int Max) CrfRange => (0, 51);
    public override bool SupportsBFrames => true;

    public double? Quality { get; set; }

    public override IReadOnlyList<string> BuildArguments()
    {
        List<string> args = ["-c:v", Name];

        if (!string.IsNullOrEmpty(Profile))
        {
            args.AddRange(["-profile:v", Profile]);
        }

        if (Quality.HasValue)
        {
            args.AddRange(["-q:v", Quality.Value.ToString("F2")]);
        }

        if (Bitrate.HasValue)
        {
            args.AddRange(["-b:v", $"{Bitrate.Value}k"]);
        }

        if (!string.IsNullOrEmpty(PixelFormat))
        {
            args.AddRange(["-pix_fmt", PixelFormat]);
        }

        if (KeyframeInterval.HasValue)
        {
            args.AddRange(["-g", KeyframeInterval.Value.ToString()]);
        }

        // Required for web playback
        args.AddRange(["-tag:v", "hvc1"]);

        return args;
    }

    public override IVideoCodec Clone()
    {
        H265VideoToolboxCodec clone = new()
        {
            Quality = Quality
        };
        CopyPropertiesTo(clone);
        return clone;
    }
}
