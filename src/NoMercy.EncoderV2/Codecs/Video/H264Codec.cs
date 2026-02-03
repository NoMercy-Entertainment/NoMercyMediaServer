using NoMercy.EncoderV2.Abstractions;

namespace NoMercy.EncoderV2.Codecs.Video;

/// <summary>
/// H.264/AVC video codec (libx264)
/// </summary>
public sealed class H264Codec : VideoCodecBase
{
    public override string Name => "libx264";
    public override string DisplayName => "H.264 (AVC)";

    public override IReadOnlyList<string> AvailablePresets =>
    [
        "ultrafast", "superfast", "veryfast", "faster", "fast",
        "medium", "slow", "slower", "veryslow", "placebo"
    ];

    public override IReadOnlyList<string> AvailableProfiles =>
    [
        "baseline", "main", "high", "high10", "high422", "high444"
    ];

    public override IReadOnlyList<string> AvailableTunes =>
    [
        "film", "animation", "grain", "stillimage", "fastdecode", "zerolatency", "psnr", "ssim"
    ];

    public override (int Min, int Max) CrfRange => (0, 51);
    public override bool SupportsBFrames => true;

    /// <summary>
    /// x264 specific: Level (e.g., "4.0", "4.1", "5.0")
    /// </summary>
    public string? Level { get; set; }

    /// <summary>
    /// x264 specific: Reference frames
    /// </summary>
    public int? RefFrames { get; set; }

    /// <summary>
    /// x264 specific: Motion estimation method
    /// </summary>
    public string? MotionEstimation { get; set; }

    /// <summary>
    /// x264 specific: Subpixel motion estimation
    /// </summary>
    public int? SubpixelMotionEstimation { get; set; }

    public override IReadOnlyList<string> BuildArguments()
    {
        List<string> args = [.. base.BuildArguments()];

        if (!string.IsNullOrEmpty(Level))
        {
            args.AddRange(["-level", Level]);
        }

        if (RefFrames.HasValue)
        {
            args.AddRange(["-refs", RefFrames.Value.ToString()]);
        }

        if (!string.IsNullOrEmpty(MotionEstimation))
        {
            args.AddRange(["-me_method", MotionEstimation]);
        }

        if (SubpixelMotionEstimation.HasValue)
        {
            args.AddRange(["-subq", SubpixelMotionEstimation.Value.ToString()]);
        }

        return args;
    }

    public override IVideoCodec Clone()
    {
        H264Codec clone = new()
        {
            Level = Level,
            RefFrames = RefFrames,
            MotionEstimation = MotionEstimation,
            SubpixelMotionEstimation = SubpixelMotionEstimation
        };
        CopyPropertiesTo(clone);
        return clone;
    }
}

/// <summary>
/// H.264 NVENC (NVIDIA hardware encoder)
/// </summary>
public sealed class H264NvencCodec : VideoCodecBase
{
    public override string Name => "h264_nvenc";
    public override string DisplayName => "H.264 (NVENC)";
    public override bool RequiresHardwareAcceleration => true;
    public override HardwareAcceleration? HardwareAccelerationType => HardwareAcceleration.Nvenc;

    public override IReadOnlyList<string> AvailablePresets =>
    [
        "default", "slow", "medium", "fast", "hp", "hq", "bd", "ll", "llhq", "llhp",
        "lossless", "losslesshp", "p1", "p2", "p3", "p4", "p5", "p6", "p7"
    ];

    public override IReadOnlyList<string> AvailableProfiles =>
    [
        "baseline", "main", "high", "high444p"
    ];

    public override IReadOnlyList<string> AvailableTunes => [];

    public override (int Min, int Max) CrfRange => (0, 51);
    public override bool SupportsBFrames => true;

    /// <summary>
    /// NVENC specific: Rate control mode
    /// </summary>
    public string? RcMode { get; set; }

    /// <summary>
    /// NVENC specific: Constant quality mode (similar to CRF)
    /// </summary>
    public int? Cq { get; set; }

    /// <summary>
    /// NVENC specific: GPU index to use
    /// </summary>
    public int? GpuIndex { get; set; }

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

        if (KeyframeInterval.HasValue)
        {
            args.AddRange(["-g", KeyframeInterval.Value.ToString()]);
        }

        if (GpuIndex.HasValue)
        {
            args.AddRange(["-gpu", GpuIndex.Value.ToString()]);
        }

        return args;
    }

    public override IVideoCodec Clone()
    {
        H264NvencCodec clone = new()
        {
            RcMode = RcMode,
            Cq = Cq,
            GpuIndex = GpuIndex
        };
        CopyPropertiesTo(clone);
        return clone;
    }
}

/// <summary>
/// H.264 QSV (Intel Quick Sync)
/// </summary>
public sealed class H264QsvCodec : VideoCodecBase
{
    public override string Name => "h264_qsv";
    public override string DisplayName => "H.264 (QSV)";
    public override bool RequiresHardwareAcceleration => true;
    public override HardwareAcceleration? HardwareAccelerationType => HardwareAcceleration.Qsv;

    public override IReadOnlyList<string> AvailablePresets =>
    [
        "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"
    ];

    public override IReadOnlyList<string> AvailableProfiles =>
    [
        "baseline", "main", "high"
    ];

    public override IReadOnlyList<string> AvailableTunes => [];

    public override (int Min, int Max) CrfRange => (1, 51);
    public override bool SupportsBFrames => true;

    /// <summary>
    /// QSV specific: Global quality
    /// </summary>
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

        return args;
    }

    public override IVideoCodec Clone()
    {
        H264QsvCodec clone = new()
        {
            GlobalQuality = GlobalQuality
        };
        CopyPropertiesTo(clone);
        return clone;
    }
}

/// <summary>
/// H.264 VideoToolbox (macOS hardware encoder)
/// </summary>
public sealed class H264VideoToolboxCodec : VideoCodecBase
{
    public override string Name => "h264_videotoolbox";
    public override string DisplayName => "H.264 (VideoToolbox)";
    public override bool RequiresHardwareAcceleration => true;
    public override HardwareAcceleration? HardwareAccelerationType => HardwareAcceleration.VideoToolbox;

    public override IReadOnlyList<string> AvailablePresets => [];

    public override IReadOnlyList<string> AvailableProfiles =>
    [
        "baseline", "main", "high"
    ];

    public override IReadOnlyList<string> AvailableTunes => [];

    public override (int Min, int Max) CrfRange => (0, 51);
    public override bool SupportsBFrames => true;

    /// <summary>
    /// VideoToolbox specific: Quality (0-1)
    /// </summary>
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

        return args;
    }

    public override IVideoCodec Clone()
    {
        H264VideoToolboxCodec clone = new()
        {
            Quality = Quality
        };
        CopyPropertiesTo(clone);
        return clone;
    }
}
