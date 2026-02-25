// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable MemberCanBePrivate.Global

using NoMercy.Encoder.Format.Rules;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Encoder.Format.Video;

public abstract class BaseVideo : Classes
{
    #region Properties

    public virtual CodecDto VideoCodec { get; set; } = VideoCodecs.H264;

    protected internal FfProbeVideoStream? VideoStream;

    internal List<FfProbeVideoStream> VideoStreams { get; set; } = [];

    protected internal virtual bool BFramesSupport => false;

    protected internal virtual int Modulus { get; set; }

    protected internal virtual int[] CrfRange { get; set; } = [0, 51];
    protected internal int Bitrate { get; set; }
    internal int BufferSize { get; set; }
    internal string Tune { get; set; } = string.Empty;
    internal string Profile { get; set; } = string.Empty;
    internal string Preset { get; set; } = string.Empty;
    internal string PixelFormat { get; set; } = string.Empty;
    internal string Level { get; set; } = string.Empty;
    internal int ConstantRateFactor { get; set; }
    internal int FrameRate { get; set; }
    internal int KeyIntMin { get; set; }
    internal int KeyInt { get; set; }
    internal int OutputWidth { get; set; }
    internal int? OutputHeight { get; set; }
    public int StreamIndex { get; set; }
    internal int MaxRate { get; set; }
    public bool HdrAllowed { get; set; }
    public bool ConvertToSdr { get; set; }

    protected internal virtual string[] AvailableContainers =>
    [
        VideoContainers.Hls, VideoContainers.Mkv,
        VideoContainers.Mp4, VideoContainers.Webm
    ];

    public virtual string[] AvailablePresets => [];
    public virtual string[] AvailableProfiles => [];
    public virtual string[] AvailableTune => [];
    public virtual string[] AvailableColorSpaces => [];
    public virtual string[] AvailableLevels => [];

    protected virtual CodecDto[] AvailableCodecs =>
    [
        VideoCodecs.H264, VideoCodecs.H264Nvenc, VideoCodecs.H265,
        VideoCodecs.H265Nvenc, VideoCodecs.Vp9, VideoCodecs.Vp9Nvenc,
        VideoCodecs.Av1
    ];

    internal readonly Dictionary<string, dynamic> _extraParameters = [];
    internal readonly Dictionary<string, dynamic> _filters = [];
    internal readonly Dictionary<string, dynamic> _ops = [];

    public static VideoQualityDto[] AvailableVideoSizes =>
    [
        FrameSizes._240p, FrameSizes._360p,
        FrameSizes._480p, FrameSizes._720p,
        FrameSizes._1080p, FrameSizes._1440p,
        FrameSizes._4k, FrameSizes._8k
    ];

    internal string _hlsPlaylistType = "event";

    private string _hlsSegmentFilename = "";

    private string HlsSegmentFilename
    {
        get => _hlsSegmentFilename
            .Replace(":framesize:", $"{Scale.W}x{Scale.H}")
            .Replace(":filename:", FileName)
            .Replace(":type:", Type);
        set => _hlsSegmentFilename = value;
    }

    // ReSharper disable once InconsistentNaming
    internal string _hlsPlaylistFilename = string.Empty;

    public bool IsHdr => VideoIsHdr();

    internal string HlsPlaylistFilename
    {
        get => _hlsPlaylistFilename
            .Replace(":framesize:", $"{Scale.W}x{Scale.H}")
            .Replace(":filename:", FileName)
            .Replace(":type:", Type);
        private set => _hlsPlaylistFilename = value;
    }

    #endregion

    #region Setters

    public BaseVideo SetKiloBitrate(int? kiloBitrate = 0)
    {
        if (kiloBitrate is null) return this;

        if (kiloBitrate < 0)
            throw new("Wrong bitrate value");

        Bitrate = kiloBitrate.Value;

        return this;
    }

    private bool VideoIsHdr()
    {
        if (VideoStream is null)
            throw new("Video stream is null");
        if (VideoStream.PixFmt?.Contains("hdr") == true) return true;
        if (string.IsNullOrEmpty(VideoStream.ColorSpace)) return false;
        if (VideoStream.ColorSpace.Contains(ColorSpaces.Bt2020)) return true;
        return false;
    }

    protected BaseVideo SetVideoCodec(string videoCodec)
    {
        CodecDto[] availableCodecs = AvailableCodecs;
        if (availableCodecs.All(codec => codec.Value != videoCodec))
            throw new(
                $"Wrong video codec value for {videoCodec}, available formats are {string.Join(", ", AvailableCodecs.Select(codec => codec.Value))}");

        VideoCodec = availableCodecs.First(codec => codec.Value == videoCodec);

        return this;
    }

    public BaseVideo AddCustomArgument(string key, dynamic value)
    {
        _extraParameters[key] = $"\"{value}\"";
        return this;
    }

    public BaseVideo AddCustomArguments((string key, string val)[] profileCustomArguments)
    {
        foreach ((string key, string val) in profileCustomArguments)
            AddCustomArgument(key, val);
        return this;
    }

    internal BaseVideo AddFilter(string key, dynamic value)
    {
        _filters[key] = $"\"{value}\"";

        return this;
    }

    public BaseVideo AddOpt(string key, dynamic value)
    {
        _ops.Add(key, $"\"{value}\"");
        return this;
    }

    public BaseVideo SetMaxRate(int value)
    {
        MaxRate = value;
        return this;
    }

    public BaseVideo SetFrameRate(int value)
    {
        FrameRate = value;
        return this;
    }

    public BaseVideo SetKeyInt(int value)
    {
        KeyInt = value;
        return this;
    }

    public BaseVideo SetBufferSize(int value)
    {
        BufferSize = value;
        return this;
    }

    public BaseVideo SetScale(string scale)
    {
        OutputWidth = scale.Split(":")[0].ToInt();
        ScaleValue = scale;
        return this;
    }

    public BaseVideo SetScale(int value)
    {
        OutputWidth = value;
        ScaleValue = $"{value}:-2";
        return this;
    }

    public BaseVideo SetScale(int width, int? height)
    {
        OutputWidth = width;

        if (height is 0)
        {
            ScaleValue = $"{width}:-2";
        }
        else
        {
            OutputHeight = height;
            ScaleValue = $"{width}:{height}";
        }

        return this;
    }

    public BaseVideo SetConstantRateFactor(int value)
    {
        if (value < 0 || value > 51)
            throw new("Wrong constant rate factor value");
        ConstantRateFactor = value;
        return this;
    }

    public BaseVideo SetFps(int value)
    {
        FrameRate = value;
        return this;
    }

    public BaseVideo SetColorSpace(string value)
    {
        PixelFormat = value;
        return this;
    }

    public BaseVideo SetPreset(string value)
    {
        if (string.IsNullOrEmpty(value))
            return this;
        if (!AvailablePresets.Contains(value))
        {
            Logger.Encoder($"Skipping preset '{value}' for {VideoCodec.Name}, available presets are {string.Join(", ", AvailablePresets)}", LogEventLevel.Warning);
            return this;
        }
        Preset = value;
        return this;
    }

    public BaseVideo SetProfile(string value)
    {
        if (string.IsNullOrEmpty(value))
            return this;
        if (!AvailableProfiles.Contains(value))
        {
            Logger.Encoder($"Skipping profile '{value}' for {VideoCodec.Name}, available profiles are {string.Join(", ", AvailableProfiles)}", LogEventLevel.Warning);
            return this;
        }
        Profile = value;
        return this;
    }

    public BaseVideo SetTune(string value)
    {
        if (string.IsNullOrEmpty(value))
            return this;
        if (!AvailableTune.Contains(value))
        {
            Logger.Encoder($"Skipping tune '{value}' for {VideoCodec.Name}, available tunes are {string.Join(", ", AvailableTune)}", LogEventLevel.Warning);
            return this;
        }
        Tune = value;
        return this;
    }

    public BaseVideo SetLevel(string value)
    {
        if (string.IsNullOrEmpty(value))
            return this;
        if (!AvailableLevels.Contains(value))
        {
            Logger.Encoder($"Skipping level '{value}' for {VideoCodec.Name}, available levels are {string.Join(", ", AvailableLevels)}", LogEventLevel.Warning);
            return this;
        }
        Level = value;
        return this;
    }

    public BaseVideo AddOpts(string value)
    {
        // AddFilter(value, "");
        return this;
    }

    public BaseVideo AddOpts(string[] profileOpts)
    {
        foreach (string opt in profileOpts)
            AddOpts(opt);
        return this;
    }

    public BaseVideo SetHlsSegmentFilename(string value)
    {
        HlsSegmentFilename = value;
        return this;
    }

    public BaseVideo SetHlsPlaylistFilename(string value)
    {
        HlsPlaylistFilename = value;
        return this;
    }

    public BaseVideo FromStream(int value)
    {
        StreamIndex = value;
        return this;
    }

    public BaseVideo AllowHdr()
    {
        HdrAllowed = true;
        return this;
    }

    public BaseVideo ConvertHdrToSdr(bool value = true)
    {
        ConvertToSdr = value;
        return this;
    }

    public override BaseVideo ApplyFlags()
    {
        // Set keyframe interval: use explicit KeyInt if provided (> 0), otherwise auto-calculate
        if (KeyInt > 0)
        {
            // Use explicitly set keyframe interval from profile
            AddCustomArgument("-g", KeyInt);
            AddCustomArgument("-keyint_min", KeyInt);
        }
        else if (Container?.ContainerDto.Name == VideoContainers.Hls && FrameRate > 0)
        {
            // For HLS streaming, auto-calculate keyframe interval to match HLS segment time
            // This ensures each segment has at least one keyframe at its boundary
            // HlsTime defaults to 4 seconds in Classes base class
            int keyframeInterval = (int)Math.Ceiling((double)HlsTime * FrameRate);
            AddCustomArgument("-g", keyframeInterval);
            AddCustomArgument("-keyint_min", keyframeInterval);
        }
        else if (FrameRate > 0)
        {
            // For non-HLS formats, use 1-second keyframe interval
            AddCustomArgument("-g", FrameRate);
            AddCustomArgument("-keyint_min", FrameRate);
        }

        if (!string.IsNullOrEmpty(Profile))
            AddCustomArgument("-profile:v", Profile);

        if (Bitrate > 0)
            AddCustomArgument("-b:v", $"{Bitrate}k");
        if (MaxRate > 0)
            AddCustomArgument("-maxrate", $"{MaxRate}k");
        if (BufferSize > 0)
            AddCustomArgument("-bufsize", $"{BufferSize}k");

        if (!string.IsNullOrEmpty(Level))
            AddCustomArgument("-level:v", Level);

        // Apply codec-specific quality and rate control flags
        ApplyCodecSpecificFlags();

        if (!string.IsNullOrEmpty(Preset))
            AddCustomArgument("-preset", Preset);
        if (!string.IsNullOrEmpty(Tune))
            AddCustomArgument("-tune:v", Tune);

        return this;
    }

    /// <summary>
    /// Applies quality control flags conditionally based on the selected video codec.
    /// Different encoders support different rate control methods and quality parameters.
    /// For HLS streaming, we prioritize speed over advanced rate control to maximize throughput.
    /// </summary>
    private void ApplyCodecSpecificFlags()
    {
        bool isNvenc = VideoCodec.Value.Contains("nvenc");
        bool isAmf = VideoCodec.Value.Contains("amf");
        bool isQsv = VideoCodec.Value.Contains("qsv");
        bool isVideotoolbox = VideoCodec.Value.Contains("videotoolbox");
        bool isSoftware = !isNvenc && !isAmf && !isQsv && !isVideotoolbox;

        if (isSoftware)
        {
            // libx264, libx265, libvpx-vp9, librav1e use CRF (Constant Rate Factor)
            if (ConstantRateFactor > 0)
            {
                AddCustomArgument("-crf", ConstantRateFactor);
                AddCustomArgument("-rc", "vbr");
                AddCustomArgument("-cq:v", Convert.ToInt32(ConstantRateFactor * 1.12));
            }
        }
        else if (isNvenc)
        {
            // NVIDIA NVENC: For HLS streaming, skip -rc flag for maximum encoding speed
            // The bitrate (-b:v) alone provides sufficient quality control without rate control overhead
            // Only use -cq when CRF is explicitly specified (quality mode)
            if (ConstantRateFactor > 0)
            {
                AddCustomArgument("-cq:v", ConstantRateFactor);
            }
            // Don't add -rc flag for HLS - it's slower and not necessary for streaming
        }
        else if (isAmf)
        {
            // AMD AMF: Same optimization as NVENC for HLS
            if (ConstantRateFactor > 0)
            {
                AddCustomArgument("-cq", ConstantRateFactor);
            }
        }
        else if (isQsv)
        {
            // Intel QSV: Use ICQ only if CRF specified
            if (ConstantRateFactor > 0)
            {
                int icqQuality = Math.Max(1, Math.Min(51, ConstantRateFactor));
                AddCustomArgument("-global_quality", icqQuality);
                AddCustomArgument("-rc", "icq");
            }
        }
        else if (isVideotoolbox)
        {
            // Apple VideoToolbox
            if (ConstantRateFactor > 0)
            {
                int quality = 100 - (ConstantRateFactor * 2);
                quality = Math.Max(0, Math.Min(100, quality));
                AddCustomArgument("-q:v", quality);
            }
        }
    }

    public BaseVideo Build()
    {
        BaseVideo newStream = (BaseVideo)MemberwiseClone();

        newStream.IsVideo = true;

        newStream.VideoStream = VideoStreams.First();

        return newStream;
    }

    #endregion

    public abstract int GetPasses();

    public void AddToDictionary(Dictionary<string, dynamic> commandDictionary, int index)
    {
        commandDictionary["-map"] = $"[v{index}_hls_0]";
        commandDictionary["-c:v"] = VideoCodec.Value;

        // Bitstream filter for HLS
        if (Container?.ContainerDto.Name == VideoContainers.Hls)
        {
            commandDictionary["-bsf:v"] = VideoCodec.Value.ToLower() switch
            {
                "libx264" or "h264_nvenc" or "h264_qsv" or "h264_amf" or "h264_videotoolbox" => "h264_mp4toannexb",
                "libx265" or "hevc_nvenc" or "hevc_qsv" or "hevc_amf" or "hevc_videotoolbox" => "hevc_mp4toannexb",
                _ => ""
            };
            
            if (commandDictionary["-bsf:v"] == "")
                commandDictionary.Remove("-bsf:v");
        }

        commandDictionary["-metadata"] = $"title=\"{Title.EscapeQuotes()}\"";

        if (Container?.ContainerDto.Type == "mp4")
        {
            commandDictionary["-movflags"] = "faststart";
        }
        
        bool isUhd = (VideoStream?.Width ?? 0) >= 3840 || (VideoStream?.Height ?? 0) >= 2160;
        bool isHdr = PixelFormat is VideoPixelFormats.Yuv444P10Le or VideoPixelFormats.Yuv420P10Le;
        
        if (isUhd)
        {
            commandDictionary["-color_primaries"] = "bt2020";
            commandDictionary["-colorspace"] = "bt2020nc";
            commandDictionary["-color_trc"] = isHdr ? "smpte2084" : "bt709";
        }
        else
        {
            commandDictionary["-color_primaries"] = "bt709";
            commandDictionary["-color_trc"] = "bt709";
            commandDictionary["-colorspace"] = "bt709";
        }

        commandDictionary["-color_range"] = "tv";

        if (isHdr)
        {
            const string masterDisplay = "G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L(10000000,1)";
            const string contentLight = "10000,4000";

            switch (VideoCodec.Value.ToLower())
            {
                case "libx265":
                    commandDictionary["-x265-params"] = $"master-display={masterDisplay}:max-cll={contentLight}:hdr-opt=1:repeat-headers=1";
                    break;

                case "libsvtav1":
                    commandDictionary["-svtav1-params"] = $"master-display={masterDisplay}:content-light={contentLight}";
                    break;
            }
        }

        foreach (KeyValuePair<string, dynamic> extraParameter in _extraParameters)
            commandDictionary[extraParameter.Key] = extraParameter.Value;
    }

    public void CreateFolder()
    {
        string path = Path.Combine(BasePath, HlsSegmentFilename.Split("/").First());

        if (!Directory.Exists(path))
        {
            Logger.Encoder($"Creating folder {path}", LogEventLevel.Verbose);
            Directory.CreateDirectory(path);
        }
    }

    public static BaseVideo Create(string profileCodec)
    {
        return profileCodec switch
        {
            "libx264" or "h264_nvenc" or "h264_amf" or "h264_qsv" or "h264_videotoolbox" => new X264(profileCodec),
            "libx265" or "hevc_nvenc" or "hevc_amf" or "hevc_qsv" or "hevc_videotoolbox" => new X265(profileCodec),
            "vp9" or "libvpx-vp9" or "vp9_nvenc" or "vp9_amf" or "vp9_qsv" or "vp9_videotoolbox" => new Vp9(profileCodec),
            "librav1e" or "av1_nvenc" or "av1_amf" or "av1_qsv" or "av1_videotoolbox" => new Av1(profileCodec),
            _ => throw new($"Video codec {profileCodec} is not supported")
        };
    }
}