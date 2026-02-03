using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.EncoderV2.Profiles;

/// <summary>
/// Default encoding profile definitions for common use cases
/// </summary>
public static class DefaultProfiles
{
    public static EncoderProfile[] All =>
    [
        Profile1080pHigh,
        Profile720pMedium,
        Profile480pLow,
        ProfileHevc1080p,
        ProfileHevc4k,
        ProfileAv1,
        ProfileAudioOnly
    ];

    /// <summary>
    /// High quality 1080p H.264 profile
    /// </summary>
    public static EncoderProfile Profile1080pHigh => new()
    {
        Id = Ulid.NewUlid(),
        Name = "1080p High",
        Container = "mp4",
        Param = "hls",
        VideoProfiles =
        [
            new IVideoProfile
            {
                Codec = "libx264",
                Width = 1920,
                Height = 1080,
                Bitrate = 5000,
                Crf = 23,
                Preset = "medium",
                Profile = "high",
                Framerate = 0,
                KeyInt = 48,
                ConvertHdrToSdr = false,
                Opts = ["-pix_fmt yuv420p"]
            }
        ],
        AudioProfiles =
        [
            new IAudioProfile
            {
                Codec = "aac",
                Channels = 2,
                SampleRate = 48000,
                Opts = ["-b:a 192k"]
            }
        ],
        SubtitleProfiles =
        [
            new ISubtitleProfile
            {
                Codec = "webvtt",
                Opts = []
            }
        ]
    };

    /// <summary>
    /// Medium quality 720p H.264 profile
    /// </summary>
    public static EncoderProfile Profile720pMedium => new()
    {
        Id = Ulid.NewUlid(),
        Name = "720p Medium",
        Container = "mp4",
        Param = "hls",
        VideoProfiles =
        [
            new IVideoProfile
            {
                Codec = "libx264",
                Width = 1280,
                Height = 720,
                Bitrate = 2500,
                Crf = 23,
                Preset = "medium",
                Profile = "high",
                Framerate = 0,
                KeyInt = 48,
                ConvertHdrToSdr = false,
                Opts = ["-pix_fmt yuv420p"]
            }
        ],
        AudioProfiles =
        [
            new IAudioProfile
            {
                Codec = "aac",
                Channels = 2,
                SampleRate = 48000,
                Opts = ["-b:a 128k"]
            }
        ],
        SubtitleProfiles =
        [
            new ISubtitleProfile
            {
                Codec = "webvtt",
                Opts = []
            }
        ]
    };

    /// <summary>
    /// Low quality 480p H.264 profile for bandwidth-constrained scenarios
    /// </summary>
    public static EncoderProfile Profile480pLow => new()
    {
        Id = Ulid.NewUlid(),
        Name = "480p Low",
        Container = "mp4",
        Param = "hls",
        VideoProfiles =
        [
            new IVideoProfile
            {
                Codec = "libx264",
                Width = 854,
                Height = 480,
                Bitrate = 1000,
                Crf = 23,
                Preset = "medium",
                Profile = "main",
                Framerate = 0,
                KeyInt = 48,
                ConvertHdrToSdr = false,
                Opts = ["-pix_fmt yuv420p"]
            }
        ],
        AudioProfiles =
        [
            new IAudioProfile
            {
                Codec = "aac",
                Channels = 2,
                SampleRate = 44100,
                Opts = ["-b:a 96k"]
            }
        ],
        SubtitleProfiles =
        [
            new ISubtitleProfile
            {
                Codec = "webvtt",
                Opts = []
            }
        ]
    };

    /// <summary>
    /// High efficiency 1080p HEVC profile
    /// </summary>
    public static EncoderProfile ProfileHevc1080p => new()
    {
        Id = Ulid.NewUlid(),
        Name = "1080p HEVC",
        Container = "mp4",
        Param = "hls",
        VideoProfiles =
        [
            new IVideoProfile
            {
                Codec = "libx265",
                Width = 1920,
                Height = 1080,
                Bitrate = 3000,
                Crf = 28,
                Preset = "medium",
                Profile = "main",
                Framerate = 0,
                KeyInt = 48,
                ConvertHdrToSdr = false,
                Opts = ["-pix_fmt yuv420p", "-x265-params log-level=error"]
            }
        ],
        AudioProfiles =
        [
            new IAudioProfile
            {
                Codec = "aac",
                Channels = 2,
                SampleRate = 48000,
                Opts = ["-b:a 192k"]
            }
        ],
        SubtitleProfiles =
        [
            new ISubtitleProfile
            {
                Codec = "webvtt",
                Opts = []
            }
        ]
    };

    /// <summary>
    /// 4K HEVC profile for high resolution content
    /// </summary>
    public static EncoderProfile ProfileHevc4k => new()
    {
        Id = Ulid.NewUlid(),
        Name = "4K HEVC",
        Container = "mp4",
        Param = "hls",
        VideoProfiles =
        [
            new IVideoProfile
            {
                Codec = "libx265",
                Width = 3840,
                Height = 2160,
                Bitrate = 15000,
                Crf = 28,
                Preset = "slow",
                Profile = "main10",
                Framerate = 0,
                KeyInt = 48,
                ConvertHdrToSdr = false,
                Opts = ["-pix_fmt yuv420p10le", "-x265-params log-level=error"]
            }
        ],
        AudioProfiles =
        [
            new IAudioProfile
            {
                Codec = "aac",
                Channels = 2,
                SampleRate = 48000,
                Opts = ["-b:a 256k"]
            }
        ],
        SubtitleProfiles =
        [
            new ISubtitleProfile
            {
                Codec = "webvtt",
                Opts = []
            }
        ]
    };

    /// <summary>
    /// AV1 profile for next-generation encoding
    /// </summary>
    public static EncoderProfile ProfileAv1 => new()
    {
        Id = Ulid.NewUlid(),
        Name = "1080p AV1",
        Container = "mp4",
        Param = "hls",
        VideoProfiles =
        [
            new IVideoProfile
            {
                Codec = "libaom-av1",
                Width = 1920,
                Height = 1080,
                Bitrate = 2000,
                Crf = 32,
                Preset = "medium",
                Profile = "main",
                Framerate = 0,
                KeyInt = 48,
                ConvertHdrToSdr = false,
                Opts = ["-cpu-used 4", "-row-mt 1"]
            }
        ],
        AudioProfiles =
        [
            new IAudioProfile
            {
                Codec = "aac",
                Channels = 2,
                SampleRate = 48000,
                Opts = ["-b:a 192k"]
            }
        ],
        SubtitleProfiles =
        [
            new ISubtitleProfile
            {
                Codec = "webvtt",
                Opts = []
            }
        ]
    };

    /// <summary>
    /// Audio-only profile for music/podcast content
    /// </summary>
    public static EncoderProfile ProfileAudioOnly => new()
    {
        Id = Ulid.NewUlid(),
        Name = "Audio Only",
        Container = "mp4",
        Param = "audio",
        VideoProfiles = [],
        AudioProfiles =
        [
            new IAudioProfile
            {
                Codec = "aac",
                Channels = 2,
                SampleRate = 48000,
                Opts = ["-b:a 192k", "-movflags +faststart"]
            }
        ],
        SubtitleProfiles = []
    };
}

