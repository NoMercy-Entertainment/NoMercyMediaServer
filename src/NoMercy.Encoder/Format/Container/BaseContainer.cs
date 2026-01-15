using NoMercy.Encoder.Core;
using NoMercy.Encoder.Format.Audio;
using NoMercy.Encoder.Format.Image;
using NoMercy.Encoder.Format.Rules;
using NoMercy.Encoder.Format.Subtitle;
using NoMercy.Encoder.Format.Video;
using NoMercy.NmSystem;

namespace NoMercy.Encoder.Format.Container;

public class BaseContainer : Classes
{
    #region Properties

    public new virtual ContainerDto ContainerDto { get; protected set; } = AvailableContainers.First(c => c.IsDefault);

    public FfProbeData FfProbeData;
    public readonly List<BaseVideo> VideoStreams = [];
    public readonly List<BaseAudio> AudioStreams = [];
    public readonly List<BaseSubtitle> SubtitleStreams = [];
    public readonly List<BaseImage> ImageStreams = [];

    public static ContainerDto[] AvailableContainers =>
    [
        new() { Name = VideoContainers.Hls, Type = "video", IsDefault = false },
        new() { Name = VideoContainers.Mkv, Type = "video", IsDefault = false },
        new() { Name = VideoContainers.Mp4, Type = "video", IsDefault = true },
        new() { Name = VideoContainers.Webm, Type = "video", IsDefault = false },
        new() { Name = AudioContainers.Flac, Type = "audio", IsDefault = false },
        new() { Name = AudioContainers.Mp3, Type = "audio", IsDefault = true }
    ];

    public static string GetName(string container)
    {
        return container switch
        {
            "mkv" => "Mkv",
            "mp4" => "Mp4",
            "m3u8" => "Hls",
            "webm" => "WebM",
            "flv" => "Flv",
            "flac" => "Flac",
            "mp3" => "Mp3",
            _ => throw new ArgumentOutOfRangeException(nameof(container), container, null)
        };
    }

    public virtual CodecDto[] AvailableVideoCodecs =>
    [
        VideoCodecs.H264, VideoCodecs.H264Nvenc,
        VideoCodecs.H265, VideoCodecs.H265Nvenc,
        VideoCodecs.Vp9, VideoCodecs.Vp9Nvenc
    ];

    public virtual CodecDto[] AvailableAudioCodecs =>
    [
        AudioCodecs.Aac, AudioCodecs.Opus, AudioCodecs.Vorbis,
        AudioCodecs.Mp3, AudioCodecs.Flac, AudioCodecs.Ac3,
        AudioCodecs.Eac3, AudioCodecs.LibOpus, AudioCodecs.TrueHd
    ];

    public virtual CodecDto[] AvailableSubtitleCodecs =>
    [
        SubtitleCodecs.Webvtt, SubtitleCodecs.Srt, SubtitleCodecs.Ass,
        SubtitleCodecs.Copy
    ];

    internal readonly Dictionary<string, dynamic> _extraParameters = new();
    private readonly Dictionary<string, dynamic> _ops = new();
    protected internal readonly Dictionary<int, dynamic> Streams = [];

    public virtual CodecDto[] AvailableCodecs => [];
    protected virtual string[] AvailablePresets => [];
    protected virtual string[] AvailableProfiles => [];
    protected virtual string[] AvailableTunes => [];
    protected virtual string[] AvailableLevels => [];

    #endregion

    #region Setters

    protected BaseContainer SetContainer(string videoContainer)
    {
        ContainerDto[] availableCodecs = AvailableContainers;
        if (availableCodecs.All(container => container.Name != videoContainer))
            throw new(
                $"Wrong video container value for {videoContainer}, available formats are {string.Join(", ", AvailableContainers.Select(container => container.Name))}");

        ContainerDto = availableCodecs.First(container => container.Name == videoContainer);
        Extension = ContainerDto.Name switch
        {
            "mkv" => "mkv",
            "mp4" => "mp4",
            "webm" => "webm",
            "flv" => "flv",
            "m4a" => "m4a",
            "aac" => "aac",
            "opus" => "opus",
            "ogg" => "ogg",
            "mp3" => "mp3",
            "flac" => "flac",
            "m3u8" => "m3u8",
            _ => throw new ArgumentOutOfRangeException(nameof(videoContainer), videoContainer, null)
        };

        return this;
    }

    public BaseContainer AddCustomArgument(string key, dynamic value)
    {
        _extraParameters[key] = value;
        return this;
    }

    public BaseContainer AddCustomArgument(string value)
    {
        _extraParameters.Add(value, "");
        return this;
    }

    public BaseContainer AddOpts(string key, dynamic value)
    {
        _ops[key] = value;
        return this;
    }

    public BaseContainer AddStream(BaseVideo stream)
    {
        stream.IsVideo = true;
        Streams.Add(Streams.Count, stream);
        return this;
    }

    public BaseContainer AddStream(BaseAudio stream)
    {
        stream.IsAudio = true;
        Streams.Add(Streams.Count, stream);
        return this;
    }

    public BaseContainer AddStream(BaseSubtitle stream)
    {
        stream.IsSubtitle = true;
        Streams.Add(Streams.Count, stream);
        return this;
    }

    public BaseContainer AddStream(BaseImage stream)
    {
        stream.IsImage = true;
        Streams.Add(Streams.Count, stream);
        return this;
    }

    public override BaseContainer ApplyFlags()
    {
        // AddCustomArgument("-map_metadata", -1);
        return this;
    }

    #endregion

    public Task BuildMasterPlaylist()
    {
        return HlsPlaylistGenerator.Build(BasePath, FileName);
    }

    public static BaseContainer Create(string? profileContainer)
    {
        return profileContainer switch
        {
            "mkv" => new Mkv(),
            "Mp4" => new Mp4(),
            "mp3" => new Mp3(),
            "flac" => new Flac(),
            "m3u8" => new Hls().SetHlsFlags("independent_segments"),
            _ => throw new($"Container {profileContainer} not supported")
        };
    }

    public Task ExtractChapters()
    {
        return Chapters.Extract(InputFile, BasePath);
    }

    public Task ExtractFonts()
    {
        return Fonts.Extract(InputFile, BasePath);
    }
}