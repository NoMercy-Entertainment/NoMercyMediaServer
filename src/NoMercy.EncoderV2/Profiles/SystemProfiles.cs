using NoMercy.EncoderV2.Abstractions;
using NoMercy.EncoderV2.Codecs.Audio;
using NoMercy.EncoderV2.Codecs.Subtitle;
using NoMercy.EncoderV2.Codecs.Video;
using NoMercy.EncoderV2.Containers;

namespace NoMercy.EncoderV2.Profiles;

/// <summary>
/// Provides built-in system encoding profiles
/// </summary>
public static class SystemProfiles
{
    /// <summary>
    /// HLS streaming profile with multiple quality levels (H.264 + AAC)
    /// </summary>
    public static IEncodingProfile HlsAdaptive => new EncodingProfile
    {
        Id = "hls-adaptive",
        Name = "HLS Adaptive Streaming",
        Description = "Multi-bitrate HLS streaming with H.264 video and AAC audio",
        IsSystem = true,
        Container = new HlsContainer
        {
            SegmentDuration = 4,
            PlaylistType = HlsPlaylistType.Vod
        },
        VideoOutputs =
        [
            new VideoOutputConfig
            {
                Id = "1080p",
                Codec = new H264Codec { Preset = "medium", Crf = 22, Profile = "high", PixelFormat = "yuv420p" },
                Width = 1920,
                Height = 1080,
                ScaleMode = ScaleMode.DownscaleOnly
            },
            new VideoOutputConfig
            {
                Id = "720p",
                Codec = new H264Codec { Preset = "medium", Crf = 23, Profile = "main", PixelFormat = "yuv420p" },
                Width = 1280,
                Height = 720,
                ScaleMode = ScaleMode.DownscaleOnly
            },
            new VideoOutputConfig
            {
                Id = "480p",
                Codec = new H264Codec { Preset = "medium", Crf = 24, Profile = "main", PixelFormat = "yuv420p" },
                Width = 854,
                Height = 480,
                ScaleMode = ScaleMode.DownscaleOnly
            }
        ],
        AudioOutputs =
        [
            new AudioOutputConfig
            {
                Id = "stereo",
                Codec = new AacCodec { Bitrate = 128, Channels = 2, SampleRate = 48000 }
            }
        ],
        SubtitleOutputs =
        [
            new SubtitleOutputConfig
            {
                Id = "webvtt",
                Codec = new WebvttCodec()
            }
        ],
        ThumbnailConfig = new ThumbnailConfig
        {
            IntervalSeconds = 10,
            Width = 320,
            Format = "jpeg",
            Quality = 75,
            GenerateSprite = true,
            SpriteColumns = 10
        },
        Options = new EncodingOptions
        {
            UseHardwareAcceleration = true,
            OverwriteOutput = true
        }
    };

    /// <summary>
    /// HLS streaming profile with H.265 for better compression
    /// </summary>
    public static IEncodingProfile HlsHevc => new EncodingProfile
    {
        Id = "hls-hevc",
        Name = "HLS HEVC Streaming",
        Description = "Multi-bitrate HLS streaming with H.265/HEVC video for better compression",
        IsSystem = true,
        Container = new HlsContainer
        {
            SegmentDuration = 4,
            PlaylistType = HlsPlaylistType.Vod
        },
        VideoOutputs =
        [
            new VideoOutputConfig
            {
                Id = "1080p",
                Codec = new H265Codec { Preset = "medium", Crf = 24, Profile = "main", PixelFormat = "yuv420p" },
                Width = 1920,
                Height = 1080,
                ScaleMode = ScaleMode.DownscaleOnly
            },
            new VideoOutputConfig
            {
                Id = "720p",
                Codec = new H265Codec { Preset = "medium", Crf = 26, Profile = "main", PixelFormat = "yuv420p" },
                Width = 1280,
                Height = 720,
                ScaleMode = ScaleMode.DownscaleOnly
            },
            new VideoOutputConfig
            {
                Id = "480p",
                Codec = new H265Codec { Preset = "medium", Crf = 28, Profile = "main", PixelFormat = "yuv420p" },
                Width = 854,
                Height = 480,
                ScaleMode = ScaleMode.DownscaleOnly
            }
        ],
        AudioOutputs =
        [
            new AudioOutputConfig
            {
                Id = "stereo",
                Codec = new AacCodec { Bitrate = 128, Channels = 2, SampleRate = 48000 }
            }
        ],
        SubtitleOutputs =
        [
            new SubtitleOutputConfig
            {
                Id = "webvtt",
                Codec = new WebvttCodec()
            }
        ],
        Options = new EncodingOptions
        {
            UseHardwareAcceleration = true,
            OverwriteOutput = true
        }
    };

    /// <summary>
    /// High quality 4K HLS profile
    /// </summary>
    public static IEncodingProfile Hls4K => new EncodingProfile
    {
        Id = "hls-4k",
        Name = "HLS 4K Streaming",
        Description = "4K HLS streaming with H.265 and surround audio",
        IsSystem = true,
        Container = new HlsContainer
        {
            SegmentDuration = 6,
            PlaylistType = HlsPlaylistType.Vod
        },
        VideoOutputs =
        [
            new VideoOutputConfig
            {
                Id = "2160p",
                Codec = new H265Codec { Preset = "slow", Crf = 22, Profile = "main10", PixelFormat = "yuv420p10le" },
                Width = 3840,
                Height = 2160,
                ScaleMode = ScaleMode.DownscaleOnly
            },
            new VideoOutputConfig
            {
                Id = "1080p",
                Codec = new H265Codec { Preset = "medium", Crf = 24, Profile = "main", PixelFormat = "yuv420p" },
                Width = 1920,
                Height = 1080,
                ScaleMode = ScaleMode.DownscaleOnly
            },
            new VideoOutputConfig
            {
                Id = "720p",
                Codec = new H265Codec { Preset = "medium", Crf = 26, Profile = "main", PixelFormat = "yuv420p" },
                Width = 1280,
                Height = 720,
                ScaleMode = ScaleMode.DownscaleOnly
            }
        ],
        AudioOutputs =
        [
            new AudioOutputConfig
            {
                Id = "surround",
                Codec = new Eac3Codec { Bitrate = 640, Channels = 6, SampleRate = 48000 }
            },
            new AudioOutputConfig
            {
                Id = "stereo",
                Codec = new AacCodec { Bitrate = 192, Channels = 2, SampleRate = 48000 }
            }
        ],
        SubtitleOutputs =
        [
            new SubtitleOutputConfig
            {
                Id = "webvtt",
                Codec = new WebvttCodec()
            }
        ],
        Options = new EncodingOptions
        {
            UseHardwareAcceleration = true,
            OverwriteOutput = true
        }
    };

    /// <summary>
    /// Web-optimized MP4 profile
    /// </summary>
    public static IEncodingProfile WebMp4 => new EncodingProfile
    {
        Id = "web-mp4",
        Name = "Web MP4",
        Description = "MP4 optimized for web streaming",
        IsSystem = true,
        Container = new Mp4Container { FastStart = true },
        VideoOutputs =
        [
            new VideoOutputConfig
            {
                Id = "main",
                Codec = new H264Codec { Preset = "medium", Crf = 22, Profile = "high", PixelFormat = "yuv420p" },
                Width = 1920,
                Height = 1080,
                ScaleMode = ScaleMode.DownscaleOnly
            }
        ],
        AudioOutputs =
        [
            new AudioOutputConfig
            {
                Id = "stereo",
                Codec = new AacCodec { Bitrate = 192, Channels = 2, SampleRate = 48000 }
            }
        ],
        Options = new EncodingOptions
        {
            UseHardwareAcceleration = true,
            OverwriteOutput = true
        }
    };

    /// <summary>
    /// Archive quality MKV profile
    /// </summary>
    public static IEncodingProfile ArchiveMkv => new EncodingProfile
    {
        Id = "archive-mkv",
        Name = "Archive MKV",
        Description = "High quality MKV for archival purposes",
        IsSystem = true,
        Container = new MkvContainer(),
        VideoOutputs =
        [
            new VideoOutputConfig
            {
                Id = "main",
                Codec = new H265Codec { Preset = "slow", Crf = 18, Profile = "main10", PixelFormat = "yuv420p10le" }
            }
        ],
        AudioOutputs =
        [
            new AudioOutputConfig
            {
                Id = "lossless",
                Codec = new FlacCodec { CompressionLevel = 8 }
            }
        ],
        SubtitleOutputs =
        [
            new SubtitleOutputConfig
            {
                Id = "copy",
                Codec = new SubtitleCopyCodec()
            }
        ],
        Options = new EncodingOptions
        {
            UseHardwareAcceleration = true,
            OverwriteOutput = true
        }
    };

    /// <summary>
    /// Audio-only profile for music
    /// </summary>
    public static IEncodingProfile AudioOpus => new EncodingProfile
    {
        Id = "audio-opus",
        Name = "Audio Opus",
        Description = "High quality Opus audio encoding",
        IsSystem = true,
        Container = new MkvContainer(),
        VideoOutputs = [],
        AudioOutputs =
        [
            new AudioOutputConfig
            {
                Id = "opus",
                Codec = new OpusCodec { Bitrate = 192, Channels = 2, SampleRate = 48000, CompressionLevel = 10 }
            }
        ],
        Options = new EncodingOptions
        {
            OverwriteOutput = true
        }
    };

    /// <summary>
    /// Gets all system profiles
    /// </summary>
    public static IReadOnlyList<IEncodingProfile> All =>
    [
        HlsAdaptive,
        HlsHevc,
        Hls4K,
        WebMp4,
        ArchiveMkv,
        AudioOpus
    ];
}
