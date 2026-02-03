using NoMercy.EncoderV2.Abstractions;

namespace NoMercy.EncoderV2.Codecs.Video;

/// <summary>
/// AV1 video codec (libaom-av1)
/// </summary>
public sealed class Av1Codec : VideoCodecBase
{
    public override string Name => "libaom-av1";
    public override string DisplayName => "AV1 (libaom)";

    public override IReadOnlyList<string> AvailablePresets => [];

    public override IReadOnlyList<string> AvailableProfiles =>
    [
        "main", "high", "professional"
    ];

    public override IReadOnlyList<string> AvailableTunes => [];

    // AV1 has extended CRF range
    public override (int Min, int Max) CrfRange => (0, 63);
    public override bool SupportsBFrames => false;

    /// <summary>
    /// AV1 specific: CPU usage/speed (0-8, lower is slower but better quality)
    /// </summary>
    public int? CpuUsed { get; set; }

    /// <summary>
    /// AV1 specific: Number of tile columns (power of 2)
    /// </summary>
    public int? TileColumns { get; set; }

    /// <summary>
    /// AV1 specific: Number of tile rows (power of 2)
    /// </summary>
    public int? TileRows { get; set; }

    /// <summary>
    /// AV1 specific: Enable row-based multithreading
    /// </summary>
    public bool RowMt { get; set; } = true;

    public override IReadOnlyList<string> BuildArguments()
    {
        List<string> args = ["-c:v", Name];

        if (Crf.HasValue)
        {
            args.AddRange(["-crf", Crf.Value.ToString()]);
        }
        else if (Bitrate.HasValue)
        {
            args.AddRange(["-b:v", $"{Bitrate.Value}k"]);
        }

        if (CpuUsed.HasValue)
        {
            args.AddRange(["-cpu-used", CpuUsed.Value.ToString()]);
        }

        if (TileColumns.HasValue)
        {
            args.AddRange(["-tile-columns", TileColumns.Value.ToString()]);
        }

        if (TileRows.HasValue)
        {
            args.AddRange(["-tile-rows", TileRows.Value.ToString()]);
        }

        if (RowMt)
        {
            args.AddRange(["-row-mt", "1"]);
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
        Av1Codec clone = new()
        {
            CpuUsed = CpuUsed,
            TileColumns = TileColumns,
            TileRows = TileRows,
            RowMt = RowMt
        };
        CopyPropertiesTo(clone);
        return clone;
    }
}

/// <summary>
/// AV1 SVT (SVT-AV1 encoder - faster than libaom)
/// </summary>
public sealed class Av1SvtCodec : VideoCodecBase
{
    public override string Name => "libsvtav1";
    public override string DisplayName => "AV1 (SVT-AV1)";

    public override IReadOnlyList<string> AvailablePresets =>
    [
        "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13"
    ];

    public override IReadOnlyList<string> AvailableProfiles =>
    [
        "main", "high", "professional"
    ];

    public override IReadOnlyList<string> AvailableTunes =>
    [
        "psnr", "ssim", "subjective"
    ];

    public override (int Min, int Max) CrfRange => (0, 63);
    public override bool SupportsBFrames => false;

    /// <summary>
    /// SVT-AV1 specific: Film grain synthesis level (0-50)
    /// </summary>
    public int? FilmGrain { get; set; }

    /// <summary>
    /// SVT-AV1 specific: Enable fast decode mode
    /// </summary>
    public bool FastDecode { get; set; }

    public override IReadOnlyList<string> BuildArguments()
    {
        List<string> args = ["-c:v", Name];

        if (!string.IsNullOrEmpty(Preset))
        {
            args.AddRange(["-preset", Preset]);
        }

        if (Crf.HasValue)
        {
            args.AddRange(["-crf", Crf.Value.ToString()]);
        }
        else if (Bitrate.HasValue)
        {
            args.AddRange(["-b:v", $"{Bitrate.Value}k"]);
        }

        // SVT-AV1 params
        List<string> svtParams = [];

        if (!string.IsNullOrEmpty(Tune))
        {
            svtParams.Add($"tune={Tune.ToLowerInvariant() switch
            {
                "psnr" => "0",
                "ssim" => "1",
                "subjective" => "2",
                _ => "1"
            }}");
        }

        if (FilmGrain.HasValue)
        {
            svtParams.Add($"film-grain={FilmGrain.Value}");
        }

        if (FastDecode)
        {
            svtParams.Add("fast-decode=1");
        }

        if (svtParams.Count > 0)
        {
            args.AddRange(["-svtav1-params", string.Join(":", svtParams)]);
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
        Av1SvtCodec clone = new()
        {
            FilmGrain = FilmGrain,
            FastDecode = FastDecode
        };
        CopyPropertiesTo(clone);
        return clone;
    }
}

/// <summary>
/// AV1 NVENC (NVIDIA hardware encoder)
/// </summary>
public sealed class Av1NvencCodec : VideoCodecBase
{
    public override string Name => "av1_nvenc";
    public override string DisplayName => "AV1 (NVENC)";
    public override bool RequiresHardwareAcceleration => true;
    public override HardwareAcceleration? HardwareAccelerationType => HardwareAcceleration.Nvenc;

    public override IReadOnlyList<string> AvailablePresets =>
    [
        "default", "slow", "medium", "fast", "hp", "hq", "p1", "p2", "p3", "p4", "p5", "p6", "p7"
    ];

    public override IReadOnlyList<string> AvailableProfiles =>
    [
        "main"
    ];

    public override IReadOnlyList<string> AvailableTunes => [];

    public override (int Min, int Max) CrfRange => (0, 63);
    public override bool SupportsBFrames => false;

    public string? RcMode { get; set; }
    public int? Cq { get; set; }
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
        Av1NvencCodec clone = new()
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
/// AV1 QSV (Intel Quick Sync)
/// </summary>
public sealed class Av1QsvCodec : VideoCodecBase
{
    public override string Name => "av1_qsv";
    public override string DisplayName => "AV1 (QSV)";
    public override bool RequiresHardwareAcceleration => true;
    public override HardwareAcceleration? HardwareAccelerationType => HardwareAcceleration.Qsv;

    public override IReadOnlyList<string> AvailablePresets =>
    [
        "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"
    ];

    public override IReadOnlyList<string> AvailableProfiles =>
    [
        "main"
    ];

    public override IReadOnlyList<string> AvailableTunes => [];

    public override (int Min, int Max) CrfRange => (1, 63);
    public override bool SupportsBFrames => false;

    public int? GlobalQuality { get; set; }

    public override IReadOnlyList<string> BuildArguments()
    {
        List<string> args = ["-c:v", Name];

        if (!string.IsNullOrEmpty(Preset))
        {
            args.AddRange(["-preset", Preset]);
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
        Av1QsvCodec clone = new()
        {
            GlobalQuality = GlobalQuality
        };
        CopyPropertiesTo(clone);
        return clone;
    }
}
