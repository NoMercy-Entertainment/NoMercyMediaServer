using FFMpegCore;
using NoMercy.Encoder.Format.Rules;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using Serilog.Events;

namespace NoMercy.Encoder.Format.Video;

public abstract class BaseVideo : Classes
{
    #region Properties

    public virtual CodecDto VideoCodec { get; set; } = VideoCodecs.H264;

    protected internal VideoStream? VideoStream;

    internal List<VideoStream> VideoStreams { get; set; } = [];

    protected internal virtual bool BFramesSupport => false;

    protected internal virtual int Modulus { get; set; }
    
    protected internal virtual int[] CrfRange { get; set; } = [0, 51];
    protected internal int Bitrate { get; set; }
    internal int BufferSize { get; set; }
    internal string Tune { get; set; } = string.Empty;
    internal string Profile { get; set; } = string.Empty;
    internal string Preset { get; set; } = string.Empty;
    internal string PixelFormat { get; set; } = string.Empty;
    internal int ConstantRateFactor { get; set; }
    internal int FrameRate { get; set; }
    internal int KeyIntMin { get; set; }
    internal int OutputWidth { get; set; }
    internal int? OutputHeight { get; set; }
    public int StreamIndex { get; set; }
    internal int MaxRate { get; set; }
    public bool HdrAllowed { get; set; }
    public bool ConvertToSdr { get; set; }

    protected internal virtual string[] AvailableContainers => [
        VideoContainers.Hls, VideoContainers.Mkv,
        VideoContainers.Mp4, VideoContainers.Webm
    ];

    public virtual string[] AvailablePresets => [];
    public virtual string[] AvailableProfiles => [];
    public virtual string[] AvailableTune => [];
    public virtual string[] AvailableColorSpaces => [];
    public virtual string[] AvailableLevels => [];
    protected virtual CodecDto[] AvailableCodecs => [
        VideoCodecs.H264, VideoCodecs.H264Nvenc, VideoCodecs.H265,
        VideoCodecs.H265Nvenc, VideoCodecs.Vp9, VideoCodecs.Vp9Nvenc,
        VideoCodecs.Av1,
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

    public BaseVideo SetKiloBitrate(int?  kiloBitrate = 0)
    {
        if (kiloBitrate is null) return this;

        if (kiloBitrate < 0)
            throw new("Wrong bitrate value");

        Bitrate = kiloBitrate.Value;

        return this;
    }

    private bool VideoIsHdr()
    {
        return VideoStream?.PixelFormat.Contains("hdr") ?? false;
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

        if(height is 0)
            ScaleValue = $"{width}:-2";
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
        if (!AvailablePresets.Contains(value))
            throw new($"Wrong preset value for {value}, available formats are {string.Join(", ", AvailablePresets)}");
        Preset = value;
        return this;
    }

    public BaseVideo SetProfile(string value)
    {
        if (!AvailableProfiles.Contains(value))
            throw new($"Wrong profile value for {value}, available formats are {string.Join(", ", AvailableProfiles)}");
        Profile = value;
        return this;
    }

    public BaseVideo SetTune(string value)
    {
        if (!AvailableTune.Contains(value))
            throw new($"Wrong tune value for {value}, available formats are {string.Join(", ", AvailableTune)}");
        Tune = value;
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
        if (MaxRate > 0)
            AddCustomArgument("-maxrate", MaxRate);
        if (BufferSize > 0)
            AddCustomArgument("-bufsize", BufferSize);

        if (Bitrate > 0)
        {
            AddCustomArgument("-b:v", Bitrate);
            AddCustomArgument("-keyint_min", KeyIntMin);
        }

        if (FrameRate > 0)
        {
            AddCustomArgument("-g", FrameRate);
            AddCustomArgument("-fps", FrameRate);
        }

        if (ConstantRateFactor > 0)
        {
            AddCustomArgument("-crf", ConstantRateFactor);
            AddCustomArgument("-cq:v", Convert.ToInt32(ConstantRateFactor * 1.12));
        }

        if (!string.IsNullOrEmpty(Preset))
            AddCustomArgument("-preset", Preset);
        if (!string.IsNullOrEmpty(Profile))
            AddCustomArgument("-profile:v", Profile);
        if (!string.IsNullOrEmpty(Tune))
            AddCustomArgument("-tune:v", Tune);

        return this;
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

        commandDictionary["-map_metadata"] = -1;
        commandDictionary["-fflags"] = "+bitexact";
        commandDictionary["-flags:v"] = "+bitexact";
        commandDictionary["-flags:a"] = "+bitexact";
        commandDictionary["-flags:s"] = "+bitexact";

        commandDictionary["-movflags"] = "faststart";
        commandDictionary["-metadata"] = $"title=\"{Title.EscapeQuotes()}\"";

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
            "libx264" or "h264_nvenc" => new X264(profileCodec),
            "libx265" or "hevc_nvenc" => new X265(profileCodec),
            "vp9" or "libvpx-vp9" => new Vp9(profileCodec),
            _ => throw new($"Video codec {profileCodec} is not supported")
        };
    }
}