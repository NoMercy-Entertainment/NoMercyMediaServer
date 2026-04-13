using NoMercy.Database.Models.Media;

namespace NoMercy.Service.Seeds.Data;

// TODO(encoder-v3): VideoCodecs/FrameSizes/ColorSpaces/VideoPresets/Languages static helpers
// were V1 encoder types. Replaced with string literals below.

public static class EncoderProfileSeedData
{
    public static List<EncoderProfile> GetEncoderProfiles()
    {
        return
        [
            new()
            {
                Id = Ulid.Parse("01HQ6298ZSZYKJT83WDWTPG4G8"),
                Name = "Marvel 4k",
                Container = "m3u8",
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    // HDR 4K profile
                    new()
                    {
                        Codec = "libx264",
                        Bitrate = 24000,
                        Crf = 20,
                        Width = 3840,
                        SegmentName = ":type:_:framesize:/:type:_:framesize:",
                        PlaylistName = ":type:_:framesize:/:type:_:framesize:",
                        ColorSpace = "yuv420p",
                        Preset = "fast",
                        Tune = "hq",
                        Profile = "main10",
                        Level = "5.1",
                        KeyInt = -1,
                        ConvertHdrToSdr = false,
                    },
                    // HDR 1080p profile
                    new()
                    {
                        Codec = "libx264",
                        Bitrate = 10656,
                        Crf = 20,
                        Width = 1920,
                        SegmentName = ":type:_:framesize:/:type:_:framesize:",
                        PlaylistName = ":type:_:framesize:/:type:_:framesize:",
                        ColorSpace = "yuv420p",
                        Preset = "fast",
                        Tune = "hq",
                        Profile = "main10",
                        Level = "4.0",
                        KeyInt = -1,
                        ConvertHdrToSdr = false,
                    },
                    // SDR 4K profile
                    new()
                    {
                        Codec = "libx264",
                        Bitrate = 20000,
                        Crf = 20,
                        Width = 3840,
                        SegmentName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        PlaylistName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        ColorSpace = "yuv420p",
                        Preset = "fast",
                        Tune = "hq",
                        Profile = "high",
                        Level = "5.1",
                        KeyInt = -1,
                        ConvertHdrToSdr = true,
                    },
                    // SDR 1080p profile
                    new()
                    {
                        Codec = "libx264",
                        Bitrate = 8695,
                        Crf = 20,
                        Width = 1920,
                        SegmentName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        PlaylistName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        ColorSpace = "yuv420p",
                        Preset = "fast",
                        Tune = "hq",
                        Profile = "high",
                        Level = "4.0",
                        KeyInt = -1,
                        ConvertHdrToSdr = true,
                    },
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = "aac",
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        PlaylistName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        AllowedLanguages = [],
                    },
                    new()
                    {
                        Codec = "eac3",
                        SampleRate = 48000,
                        SegmentName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        PlaylistName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        AllowedLanguages = [],
                    },
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = "webvtt",
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = [],
                    },
                    new()
                    {
                        Codec = "ass",
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = [],
                    },
                ],
            },
            new()
            {
                Id = Ulid.Parse("01HQ629JAYQDEQAH0GW3ZHGW8Z"),
                Name = "1080p high",
                Container = "m3u8",
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    new()
                    {
                        Codec = "libx264",
                        Bitrate = 10656,
                        Crf = 20,
                        Width = 1920,
                        SegmentName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        PlaylistName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        ColorSpace = "yuv420p",
                        Preset = "fast",
                        Tune = "hq",
                        Profile = "high",
                        Level = "4.0",
                        KeyInt = -1,
                        ConvertHdrToSdr = true,
                    },
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = "aac",
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        PlaylistName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        AllowedLanguages = [],
                    },
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = "webvtt",
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = [],
                    },
                ],
            },
            new()
            {
                Id = Ulid.Parse("01HQ629SJ32FTV2Q46NX3H1CK9"),
                Name = "1080p regular",
                Container = "m3u8",
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    new()
                    {
                        Codec = "libx264",
                        Bitrate = 8695,
                        Crf = 22,
                        Width = 1920,
                        SegmentName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        PlaylistName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        ColorSpace = "yuv420p",
                        Preset = "fast",
                        Tune = "hq",
                        Profile = "high",
                        Level = "4.0",
                        KeyInt = -1,
                        ConvertHdrToSdr = true,
                    },
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = "aac",
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        PlaylistName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        AllowedLanguages = [],
                    },
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = "webvtt",
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = [],
                    },
                    new()
                    {
                        Codec = "ass",
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = [],
                    },
                ],
            },
            new()
            {
                Id = Ulid.Parse("01HR360AKTW47XC6ZQ2V9DF024"),
                Name = "1080p low",
                Container = "m3u8",
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    new()
                    {
                        Codec = "libx264",
                        Bitrate = 6956,
                        Crf = 24,
                        Width = 1920,
                        SegmentName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        PlaylistName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        ColorSpace = "yuv420p",
                        Preset = "fast",
                        Tune = "hq",
                        Profile = "high",
                        Level = "4.0",
                        KeyInt = -1,
                        ConvertHdrToSdr = true,
                    },
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = "aac",
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        PlaylistName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        AllowedLanguages = [],
                    },
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = "webvtt",
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = [],
                    },
                    new()
                    {
                        Codec = "ass",
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = [],
                    },
                ],
            },
            new()
            {
                Id = Ulid.Parse("01JRH6Q85QT0D08F9J9577J04K"),
                Name = "Music",
                Container = "mp3",
                EncoderProfileFolder = [],
                AudioProfiles = [new() { Codec = "libmp3lame" }],
            },
            // Standard Quality Presets (HLS Video Streaming)
            new()
            {
                Id = Ulid.Parse("01JRH6Q8A5T0D08F9J9577J04K"),
                Name = "HD Streaming (720p)",
                Container = "m3u8",
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    new()
                    {
                        Codec = "libx264",
                        Bitrate = 4000,
                        Crf = 23,
                        Width = 1280,
                        SegmentName = ":type:_:framesize:/:type:_:framesize:",
                        PlaylistName = ":type:_:framesize:/:type:_:framesize:",
                        ColorSpace = "yuv420p",
                        Preset = "fast",
                        Tune = "hq",
                        Profile = "high",
                        Level = "4.0",
                        KeyInt = -1,
                        ConvertHdrToSdr = true,
                    },
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = "aac",
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        PlaylistName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        AllowedLanguages = [],
                    },
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = "webvtt",
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = [],
                    },
                ],
            },
            new()
            {
                Id = Ulid.Parse("01JRH6Q8B5T0D08F9J9577J04K"),
                Name = "Full HD Streaming (1080p)",
                Container = "m3u8",
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    new()
                    {
                        Codec = "libx264",
                        Bitrate = 8000,
                        Crf = 22,
                        Width = 1920,
                        SegmentName = ":type:_:framesize:/:type:_:framesize:",
                        PlaylistName = ":type:_:framesize:/:type:_:framesize:",
                        ColorSpace = "yuv420p",
                        Preset = "fast",
                        Tune = "hq",
                        Profile = "high",
                        Level = "4.0",
                        KeyInt = -1,
                        ConvertHdrToSdr = true,
                    },
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = "aac",
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        PlaylistName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        AllowedLanguages = [],
                    },
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = "webvtt",
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = [],
                    },
                    new()
                    {
                        Codec = "ass",
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = [],
                    },
                ],
            },
            new()
            {
                Id = Ulid.Parse("01JRH6Q8C5T0D08F9J9577J04K"),
                Name = "4K Streaming",
                Container = "m3u8",
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    new()
                    {
                        Codec = "libx264",
                        Bitrate = 18000,
                        Crf = 19,
                        Width = 3840,
                        SegmentName = ":type:_:framesize:/:type:_:framesize:",
                        PlaylistName = ":type:_:framesize:/:type:_:framesize:",
                        ColorSpace = "yuv420p",
                        Preset = "fast",
                        Tune = "hq",
                        Profile = "high",
                        Level = "5.1",
                        KeyInt = -1,
                        ConvertHdrToSdr = true,
                    },
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = "aac",
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        PlaylistName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        AllowedLanguages = [],
                    },
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = "webvtt",
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = [],
                    },
                    new()
                    {
                        Codec = "ass",
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = [],
                    },
                ],
            },
            // MP4 Progressive Download Profiles
            new()
            {
                Id = Ulid.Parse("01JRH6Q8D5T0D08F9J9577J04K"),
                Name = "MP4 Standard (1080p)",
                Container = "mp4",
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    new()
                    {
                        Codec = "libx264",
                        Bitrate = 8000,
                        Crf = 21,
                        Width = 1920,
                        SegmentName = ":filename:",
                        PlaylistName = ":filename:",
                        ColorSpace = "yuv420p",
                        Preset = "fast",
                        Tune = "hq",
                        Profile = "high",
                        Level = "4.0",
                        KeyInt = -1,
                        ConvertHdrToSdr = true,
                    },
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = "aac",
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":filename:",
                        PlaylistName = ":filename:",
                        AllowedLanguages = [],
                    },
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = "webvtt",
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = [],
                    },
                ],
            },
            new()
            {
                Id = Ulid.Parse("01JRH6Q8E5T0D08F9J9577J04K"),
                Name = "MP4 High Quality (4K)",
                Container = "mp4",
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    new()
                    {
                        Codec = "libx264",
                        Bitrate = 18000,
                        Crf = 18,
                        Width = 3840,
                        SegmentName = ":filename:",
                        PlaylistName = ":filename:",
                        ColorSpace = "yuv420p",
                        Preset = "fast",
                        Tune = "hq",
                        Profile = "high",
                        Level = "5.1",
                        KeyInt = -1,
                        ConvertHdrToSdr = true,
                    },
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = "aac",
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":filename:",
                        PlaylistName = ":filename:",
                        AllowedLanguages = [],
                    },
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = "webvtt",
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = [],
                    },
                ],
            },
            // Audio Profiles
            new()
            {
                Id = Ulid.Parse("01JRH6Q8F5T0D08F9J9577J04K"),
                Name = "MP3 High Quality (320kbps)",
                Container = "mp3",
                EncoderProfileFolder = [],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = "libmp3lame",
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":filename:",
                        PlaylistName = ":filename:",
                        AllowedLanguages = [],
                    },
                ],
            },
            new()
            {
                Id = Ulid.Parse("01JRH6Q8G5T0D08F9J9577J04K"),
                Name = "MP3 Standard (192kbps)",
                Container = "mp3",
                EncoderProfileFolder = [],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = "libmp3lame",
                        Channels = 2,
                        SampleRate = 44100,
                        SegmentName = ":filename:",
                        PlaylistName = ":filename:",
                        AllowedLanguages = [],
                    },
                ],
            },
            new()
            {
                Id = Ulid.Parse("01JRH6Q8H5T0D08F9J9577J04K"),
                Name = "FLAC Lossless",
                Container = "flac",
                EncoderProfileFolder = [],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = "flac",
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":filename:",
                        PlaylistName = ":filename:",
                        AllowedLanguages = [],
                    },
                ],
            },
            new()
            {
                Id = Ulid.Parse("01JRH6Q8I5T0D08F9J9577J04K"),
                Name = "AAC Standard",
                Container = "m4a",
                EncoderProfileFolder = [],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = "aac",
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":filename:",
                        PlaylistName = ":filename:",
                        AllowedLanguages = [],
                    },
                ],
            },
            new()
            {
                Id = Ulid.Parse("01JRH6Q8J5T0D08F9J9577J04K"),
                Name = "Opus (Streaming Audio)",
                Container = "ogg",
                EncoderProfileFolder = [],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = "libopus",
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":filename:",
                        PlaylistName = ":filename:",
                        AllowedLanguages = [],
                    },
                ],
            },
            // Archival Profiles (CRF, slow preset, not for streaming ladder)
            new()
            {
                Id = Ulid.Parse("01JRH6Q8K5T0D08F9J9577J04K"),
                Name = "Archival HEVC (H.265)",
                Container = "mkv",
                Param = "archival",
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    new()
                    {
                        Codec = "libx265",
                        Bitrate = 0,
                        Crf = 20,
                        Width = 3840,
                        SegmentName = ":filename:-archival-hevc",
                        PlaylistName = ":filename:-archival-hevc",
                        ColorSpace = "yuv420p",
                        Preset = "slow",
                        Tune = "hq",
                        Profile = "main10",
                        Level = "5.1",
                        KeyInt = -1,
                        ConvertHdrToSdr = false,
                    },
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = "flac",
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":filename:",
                        PlaylistName = ":filename:",
                        AllowedLanguages = [],
                    },
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = "ass",
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = [],
                    },
                ],
            },
            new()
            {
                Id = Ulid.Parse("01JRH6Q8L5T0D08F9J9577J04K"),
                Name = "Archival AV1",
                Container = "mkv",
                Param = "archival",
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    new()
                    {
                        Codec = "libaom-av1",
                        Bitrate = 0,
                        Crf = 26,
                        Width = 3840,
                        SegmentName = ":filename:-archival-av1",
                        PlaylistName = ":filename:-archival-av1",
                        ColorSpace = "yuv420p",
                        Preset = "slow",
                        Tune = "hq",
                        Profile = "main10",
                        Level = "5.1",
                        KeyInt = -1,
                        ConvertHdrToSdr = false,
                    },
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = "flac",
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":filename:",
                        PlaylistName = ":filename:",
                        AllowedLanguages = [],
                    },
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = "ass",
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = [],
                    },
                ],
            },
        ];
    }
}
