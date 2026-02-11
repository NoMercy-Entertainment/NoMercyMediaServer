using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Server.Seeds.Data;

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
                Container = VideoContainers.Hls,
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    // HDR 4K profile
                    new()
                    {
                        Codec = VideoCodecs.H264.Value,
                        Bitrate = 24000,
                        Width = FrameSizes._4k.Width,
                        SegmentName = ":type:_:framesize:/:type:_:framesize:",
                        PlaylistName = ":type:_:framesize:/:type:_:framesize:",
                        ColorSpace = ColorSpaces.Yuv420P,
                        Preset = VideoPresets.Fast,
                        Tune = VideoTunes.Hq,
                        Profile = VideoProfiles.Main10,
                        Level = "5.1",
                        KeyInt = -1,
                        ConvertHdrToSdr = false
                    },
                    // HDR 1080p profile
                    new()
                    {
                        Codec = VideoCodecs.H264.Value,
                        Bitrate = 10656,
                        Width = FrameSizes._1080p.Width,
                        SegmentName = ":type:_:framesize:/:type:_:framesize:",
                        PlaylistName = ":type:_:framesize:/:type:_:framesize:",
                        ColorSpace = ColorSpaces.Yuv420P,
                        Preset = VideoPresets.Fast,
                        Tune = VideoTunes.Hq,
                        Profile = VideoProfiles.Main10,
                        Level = "4.0",
                        KeyInt = -1,
                        ConvertHdrToSdr = false
                    },
                    // SDR 4K profile
                    new()
                    {
                        Codec = VideoCodecs.H264.Value,
                        Bitrate = 20000,
                        Width = FrameSizes._4k.Width,
                        SegmentName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        PlaylistName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        ColorSpace = ColorSpaces.Yuv420P,
                        Preset = VideoPresets.Fast,
                        Tune = VideoTunes.Hq,
                        Profile = VideoProfiles.High,
                        Level = "5.1",
                        KeyInt = -1,
                        ConvertHdrToSdr = true
                    },
                    // SDR 1080p profile
                    new()
                    {
                        Codec = VideoCodecs.H264.Value,
                        Bitrate = 8695,
                        Width = FrameSizes._1080p.Width,
                        SegmentName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        PlaylistName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        ColorSpace = ColorSpaces.Yuv420P,
                        Preset = VideoPresets.Fast,
                        Tune = VideoTunes.Hq,
                        Profile = VideoProfiles.High,
                        Level = "4.0",
                        KeyInt = -1,
                        ConvertHdrToSdr = true
                    }
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = AudioCodecs.Aac.Value,
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        PlaylistName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        AllowedLanguages = Languages.AllLanguages()
                    },
                    new()
                    {
                        Codec = AudioCodecs.Eac3.Value,
                        SampleRate = 48000,
                        SegmentName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        PlaylistName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = SubtitleCodecs.Webvtt.Value,
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = Languages.AllLanguages()
                    },
                    new()
                    {
                        Codec = SubtitleCodecs.Ass.Value,
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ]
            },
            new()
            {
                Id = Ulid.Parse("01HQ629JAYQDEQAH0GW3ZHGW8Z"),
                Name = "1080p high",
                Container = VideoContainers.Hls,
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    new()
                    {
                        Codec = VideoCodecs.H264.Value,
                        Bitrate = 10656,
                        Width = FrameSizes._1080p.Width,

                        SegmentName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        PlaylistName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        ColorSpace = ColorSpaces.Yuv420P,
                        Preset = VideoPresets.Fast,
                        Tune = VideoTunes.Hq,
                        Profile = VideoProfiles.High,
                        Level = "4.0",
                        KeyInt = -1,
                        ConvertHdrToSdr = true
                    }
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = AudioCodecs.Aac.Value,
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        PlaylistName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = SubtitleCodecs.Webvtt.Value,
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ]
            },
            new()
            {
                Id = Ulid.Parse("01HQ629SJ32FTV2Q46NX3H1CK9"),
                Name = "1080p regular",
                Container = VideoContainers.Hls,
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    new()
                    {
                        Codec = VideoCodecs.H264.Value,
                        Bitrate = 8695,
                        Width = FrameSizes._1080p.Width,

                        SegmentName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        PlaylistName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        ColorSpace = ColorSpaces.Yuv420P,
                        Preset = VideoPresets.Fast,
                        Tune = VideoTunes.Hq,
                        Profile = VideoProfiles.High,
                        Level = "4.0",
                        KeyInt = -1,
                        ConvertHdrToSdr = true
                    }
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = AudioCodecs.Aac.Value,
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        PlaylistName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = SubtitleCodecs.Webvtt.Value,
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = Languages.AllLanguages()
                    },
                    new()
                    {
                        Codec = SubtitleCodecs.Ass.Value,
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ]
            },
            new()
            {
                Id = Ulid.Parse("01HR360AKTW47XC6ZQ2V9DF024"),
                Name = "1080p low",
                Container = VideoContainers.Hls,
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    new()
                    {
                        Codec = VideoCodecs.H264.Value,
                        Bitrate = 6956,
                        Width = FrameSizes._1080p.Width,

                        SegmentName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        PlaylistName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        ColorSpace = ColorSpaces.Yuv420P,
                        Preset = VideoPresets.Fast,
                        Tune = VideoTunes.Hq,
                        Profile = VideoProfiles.High,
                        Level = "4.0",
                        KeyInt = -1,
                        ConvertHdrToSdr = true
                    }
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = AudioCodecs.Aac.Value,
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        PlaylistName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = SubtitleCodecs.Webvtt.Value,
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = Languages.AllLanguages()
                    },
                    new()
                    {
                        Codec = SubtitleCodecs.Ass.Value,
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ]
            },
            new()
            {
                Id = Ulid.Parse("01JRH6Q85QT0D08F9J9577J04K"),
                Name = "Music",
                Container = AudioContainers.Mp3,
                EncoderProfileFolder = [],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = AudioCodecs.Mp3.Value
                    }
                ]
            },
            // Standard Quality Presets (HLS Video Streaming)
            new()
            {
                Id = Ulid.Parse("01JRH6Q8A5T0D08F9J9577J04K"),
                Name = "HD Streaming (720p)",
                Container = VideoContainers.Hls,
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    new()
                    {
                        Codec = VideoCodecs.H264.Value,
                        Bitrate = 4000,
                        Width = 1280,

                        SegmentName = ":type:_:framesize:/:type:_:framesize:",
                        PlaylistName = ":type:_:framesize:/:type:_:framesize:",
                        ColorSpace = ColorSpaces.Yuv420P,
                        Preset = VideoPresets.Fast,
                        Tune = VideoTunes.Hq,
                        Profile = VideoProfiles.High,
                        Level = "4.0",
                        KeyInt = -1,
                        ConvertHdrToSdr = true
                    }
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = AudioCodecs.Aac.Value,
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        PlaylistName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = SubtitleCodecs.Webvtt.Value,
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ]
            },
            new()
            {
                Id = Ulid.Parse("01JRH6Q8B5T0D08F9J9577J04K"),
                Name = "Full HD Streaming (1080p)",
                Container = VideoContainers.Hls,
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    new()
                    {
                        Codec = VideoCodecs.H264.Value,
                        Bitrate = 8000,
                        Width = FrameSizes._1080p.Width,

                        SegmentName = ":type:_:framesize:/:type:_:framesize:",
                        PlaylistName = ":type:_:framesize:/:type:_:framesize:",
                        ColorSpace = ColorSpaces.Yuv420P,
                        Preset = VideoPresets.Fast,
                        Tune = VideoTunes.Hq,
                        Profile = VideoProfiles.High,
                        Level = "4.0",
                        KeyInt = -1,
                        ConvertHdrToSdr = true
                    }
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = AudioCodecs.Aac.Value,
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        PlaylistName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = SubtitleCodecs.Webvtt.Value,
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = Languages.AllLanguages()
                    },
                    new()
                    {
                        Codec = SubtitleCodecs.Ass.Value,
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ]
            },
            new()
            {
                Id = Ulid.Parse("01JRH6Q8C5T0D08F9J9577J04K"),
                Name = "4K Streaming",
                Container = VideoContainers.Hls,
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    new()
                    {
                        Codec = VideoCodecs.H264.Value,
                        Bitrate = 18000,
                        Width = FrameSizes._4k.Width,

                        SegmentName = ":type:_:framesize:/:type:_:framesize:",
                        PlaylistName = ":type:_:framesize:/:type:_:framesize:",
                        ColorSpace = ColorSpaces.Yuv420P,
                        Preset = VideoPresets.Fast,
                        Tune = VideoTunes.Hq,
                        Profile = VideoProfiles.High,
                        Level = "5.1",
                        KeyInt = -1,
                        ConvertHdrToSdr = true
                    }
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = AudioCodecs.Aac.Value,
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        PlaylistName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = SubtitleCodecs.Webvtt.Value,
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = Languages.AllLanguages()
                    },
                    new()
                    {
                        Codec = SubtitleCodecs.Ass.Value,
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ]
            },
            // MP4 Progressive Download Profiles
            new()
            {
                Id = Ulid.Parse("01JRH6Q8D5T0D08F9J9577J04K"),
                Name = "MP4 Standard (1080p)",
                Container = VideoContainers.Mp4,
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    new()
                    {
                        Codec = VideoCodecs.H264.Value,
                        Bitrate = 8000,
                        Width = FrameSizes._1080p.Width,

                        SegmentName = ":filename:",
                        PlaylistName = ":filename:",
                        ColorSpace = ColorSpaces.Yuv420P,
                        Preset = VideoPresets.Fast,
                        Tune = VideoTunes.Hq,
                        Profile = VideoProfiles.High,
                        Level = "4.0",
                        KeyInt = -1,
                        ConvertHdrToSdr = true
                    }
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = AudioCodecs.Aac.Value,
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":filename:",
                        PlaylistName = ":filename:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = SubtitleCodecs.Webvtt.Value,
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ]
            },
            new()
            {
                Id = Ulid.Parse("01JRH6Q8E5T0D08F9J9577J04K"),
                Name = "MP4 High Quality (4K)",
                Container = VideoContainers.Mp4,
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    new()
                    {
                        Codec = VideoCodecs.H264.Value,
                        Bitrate = 18000,
                        Width = FrameSizes._4k.Width,

                        SegmentName = ":filename:",
                        PlaylistName = ":filename:",
                        ColorSpace = ColorSpaces.Yuv420P,
                        Preset = VideoPresets.Fast,
                        Tune = VideoTunes.Hq,
                        Profile = VideoProfiles.High,
                        Level = "5.1",
                        KeyInt = -1,
                        ConvertHdrToSdr = true
                    }
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = AudioCodecs.Aac.Value,
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":filename:",
                        PlaylistName = ":filename:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = SubtitleCodecs.Webvtt.Value,
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ]
            },
            // Audio Profiles
            new()
            {
                Id = Ulid.Parse("01JRH6Q8F5T0D08F9J9577J04K"),
                Name = "MP3 High Quality (320kbps)",
                Container = AudioContainers.Mp3,
                EncoderProfileFolder = [],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = AudioCodecs.Mp3.Value,
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":filename:",
                        PlaylistName = ":filename:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ]
            },
            new()
            {
                Id = Ulid.Parse("01JRH6Q8G5T0D08F9J9577J04K"),
                Name = "MP3 Standard (192kbps)",
                Container = AudioContainers.Mp3,
                EncoderProfileFolder = [],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = AudioCodecs.Mp3.Value,
                        Channels = 2,
                        SampleRate = 44100,
                        SegmentName = ":filename:",
                        PlaylistName = ":filename:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ]
            },
            new()
            {
                Id = Ulid.Parse("01JRH6Q8H5T0D08F9J9577J04K"),
                Name = "FLAC Lossless",
                Container = AudioContainers.Flac,
                EncoderProfileFolder = [],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = AudioCodecs.Flac.Value,
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":filename:",
                        PlaylistName = ":filename:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ]
            },
            new()
            {
                Id = Ulid.Parse("01JRH6Q8I5T0D08F9J9577J04K"),
                Name = "AAC Standard",
                Container = AudioContainers.M4A,
                EncoderProfileFolder = [],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = AudioCodecs.Aac.Value,
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":filename:",
                        PlaylistName = ":filename:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ]
            },
            new()
            {
                Id = Ulid.Parse("01JRH6Q8J5T0D08F9J9577J04K"),
                Name = "Opus (Streaming Audio)",
                Container = AudioContainers.Ogg,
                EncoderProfileFolder = [],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = AudioCodecs.LibOpus.Value,
                        Channels = 2,
                        SampleRate = 48000,
                        SegmentName = ":filename:",
                        PlaylistName = ":filename:",
                        AllowedLanguages = Languages.AllLanguages()
                    }
                ]
            }
        ];
    }

}

