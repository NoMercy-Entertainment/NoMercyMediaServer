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

    public new virtual ContainerDto ContainerDto { get; protected set; } =
        AvailableContainers.First(c => c.IsDefault);

    public FfProbeData FfProbeData = null!;
    public readonly List<BaseVideo> VideoStreams = [];
    public readonly List<BaseAudio> AudioStreams = [];
    public readonly List<BaseSubtitle> SubtitleStreams = [];
    public readonly List<BaseImage> ImageStreams = [];

    public static ContainerDto[] AvailableContainers =>
        [
            new()
            {
                Name = VideoContainers.Hls,
                Type = "video",
                IsDefault = false,
            },
            new()
            {
                Name = VideoContainers.Mkv,
                Type = "video",
                IsDefault = false,
            },
            new()
            {
                Name = VideoContainers.Mp4,
                Type = "video",
                IsDefault = true,
            },
            new()
            {
                Name = VideoContainers.Webm,
                Type = "video",
                IsDefault = false,
            },
            new()
            {
                Name = AudioContainers.Flac,
                Type = "audio",
                IsDefault = false,
            },
            new()
            {
                Name = AudioContainers.Mp3,
                Type = "audio",
                IsDefault = true,
            },
            new()
            {
                Name = AudioContainers.M4A,
                Type = "audio",
                IsDefault = false,
            },
            new()
            {
                Name = AudioContainers.Ogg,
                Type = "audio",
                IsDefault = false,
            },
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
            "m4a" => "M4a",
            "ogg" => "Ogg",
            _ => throw new ArgumentOutOfRangeException(nameof(container), container, null),
        };
    }

    public virtual CodecDto[] AvailableVideoCodecs =>
        [
            VideoCodecs.H264,
            VideoCodecs.H264Nvenc,
            VideoCodecs.H265,
            VideoCodecs.H265Nvenc,
            VideoCodecs.Vp9,
            VideoCodecs.Vp9Nvenc,
        ];

    public virtual CodecDto[] AvailableAudioCodecs =>
        [
            AudioCodecs.Aac,
            AudioCodecs.Opus,
            AudioCodecs.Vorbis,
            AudioCodecs.Mp3,
            AudioCodecs.Flac,
            AudioCodecs.Ac3,
            AudioCodecs.Eac3,
            AudioCodecs.LibOpus,
            AudioCodecs.TrueHd,
        ];

    public virtual CodecDto[] AvailableSubtitleCodecs =>
        [SubtitleCodecs.Webvtt, SubtitleCodecs.Srt, SubtitleCodecs.Ass, SubtitleCodecs.Copy];

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
                $"Wrong video container value for {videoContainer}, available formats are {string.Join(", ", AvailableContainers.Select(container => container.Name))}"
            );

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
            _ => throw new ArgumentOutOfRangeException(
                nameof(videoContainer),
                videoContainer,
                null
            ),
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
        if (!SupportsMasterPlaylist)
            return Task.CompletedTask;
        return HlsPlaylistGenerator.Build(BasePath, FileName);
    }

    /// <summary>
    ///     True for adaptive-streaming containers that require a master playlist on top of the
    ///     per-variant playlists (HLS, future DASH). Single-file containers (Mp4, Mkv, Mp3, Flac,
    ///     M4a, Ogg, WebM) leave this false so the encoder doesn't pollute their output directory
    ///     with an empty <c>master.m3u8</c>.
    /// </summary>
    public virtual bool SupportsMasterPlaylist => false;

    /// <summary>
    ///     True for containers that can carry attached fonts (MKV) or stream subtitles that
    ///     reference embedded font glyphs (HLS's ASS sidecars). Audio-only containers never need
    ///     font extraction.
    /// </summary>
    public virtual bool SupportsFontsExtraction => true;

    public static BaseContainer Create(string? profileContainer)
    {
        return (profileContainer ?? string.Empty).ToLowerInvariant() switch
        {
            "mkv" => new Mkv(),
            "mp4" => new Mp4(),
            "mp3" => new Mp3(),
            "flac" => new Flac(),
            "m4a" => new M4a(),
            "ogg" => new Ogg(),
            "webm" => new WebM(),
            "m3u8" or "hls" => new Hls().SetHlsFlags("independent_segments"),
            _ => throw new($"Container {profileContainer} not supported"),
        };
    }

    /// <summary>
    ///     Video-bearing containers can carry the thumbnail sprite stream. Audio-only containers
    ///     (Mp3, Flac, M4a, Ogg) have no frames to sample — adding a sprite stream to them produces
    ///     either a failed ffmpeg invocation or an empty / garbage sprite.
    /// </summary>
    public virtual bool SupportsSpriteStream => true;

    public Task ExtractChapters()
    {
        return Chapters.Extract(InputFile, BasePath);
    }

    public Task ExtractFonts()
    {
        if (!SupportsFontsExtraction)
            return Task.CompletedTask;
        return Fonts.Extract(InputFile, BasePath);
    }

    /// <summary>
    /// Returns the set of output subdirectory names (relative to BasePath) that this
    /// container's streams would write to.  Used by the encode job to limit cleanup to
    /// only the directories owned by a failed profile, so that output from previously
    /// completed profiles is not destroyed.
    /// </summary>
    public HashSet<string> GetOutputSubdirectories()
    {
        HashSet<string> result = [];

        foreach (BaseVideo video in VideoStreams)
        {
            string raw = video._hlsPlaylistFilename;
            if (!string.IsNullOrEmpty(raw))
            {
                string firstSegment = raw.Split('/')[0];
                if (!string.IsNullOrEmpty(firstSegment))
                    result.Add(firstSegment);
            }
        }

        foreach (BaseAudio audio in AudioStreams)
        {
            string raw = audio.HlsSegmentFilename;
            if (!string.IsNullOrEmpty(raw))
            {
                string firstSegment = raw.Split('/')[0];
                if (!string.IsNullOrEmpty(firstSegment))
                    result.Add(firstSegment);
            }
        }

        return result;
    }
}
