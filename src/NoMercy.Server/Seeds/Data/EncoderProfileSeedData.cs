using NoMercy.Database.Models;
using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Server.Seeds.Data;

public static class EncoderProfileSeedData
{
    public static List<EncoderProfile> GetEncoderProfiles()
    {
        return
        [
            new EncoderProfile
            {
                Id = Ulid.Parse("01HQ6298ZSZYKJT83WDWTPG4G8"),
                Name = "Marvel 4k",
                Container = VideoContainers.Hls,
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    new()
                    {
                        Codec = VideoCodecs.H264Nvenc.Value,
                        Width = FrameSizes._1080p.Width,
                        Crf = 20,
                        SegmentName = ":type:_:framesize:/:type:_:framesize:",
                        PlaylistName = ":type:_:framesize:/:type:_:framesize:",
                        ColorSpace = ColorSpaces.Yuv444P,
                        Preset = VideoPresets.Fast,
                        Tune = VideoTunes.Hq,
                        Keyint = 48,
                        // // Opts = ["no-scenecut"],
                        CustomArguments =
                        [
                            new ValueTuple<string, string>
                            {
                                Item1 = "-x264opts",
                                Item2 = "no-scenecut"
                            }
                        ]
                    },
                    new()
                    {
                        Codec = VideoCodecs.H264Nvenc.Value,
                        Width = FrameSizes._1080p.Width,
                        Crf = 20,
                        SegmentName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        PlaylistName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        ColorSpace = ColorSpaces.Yuv420P,
                        Preset = VideoPresets.Fast,
                        Tune = VideoTunes.Hq,
                        Keyint = 48,
                        CustomArguments =
                        [
                            new ValueTuple<string, string>
                            {
                                Item1 = "-x264opts",
                                Item2 = "no-scenecut"
                            }
                        ]
                    },
                    new()
                    {
                        Codec = VideoCodecs.H264Nvenc.Value,
                        Width = FrameSizes._4k.Width,
                        Crf = 20,
                        SegmentName = ":type:_:framesize:/:type:_:framesize:",
                        PlaylistName = ":type:_:framesize:/:type:_:framesize:",
                        ColorSpace = ColorSpaces.Yuv444P,
                        Preset = VideoPresets.Fast,
                        Tune = VideoTunes.Hq,
                        Keyint = 48,
                        CustomArguments =
                        [
                            new ValueTuple<string, string>
                            {
                                Item1 = "-x264opts",
                                Item2 = "no-scenecut"
                            }
                        ]
                    },
                    new()
                    {
                        Codec = VideoCodecs.H264Nvenc.Value,
                        Width = FrameSizes._4k.Width,
                        Crf = 20,
                        SegmentName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        PlaylistName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        ColorSpace = ColorSpaces.Yuv420P,
                        Preset = VideoPresets.Fast,
                        Tune = VideoTunes.Hq,
                        Keyint = 48,
                        CustomArguments =
                        [
                            new ValueTuple<string, string>
                            {
                                Item1 = "-x264opts",
                                Item2 = "no-scenecut"
                            }
                        ]
                    }
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = AudioCodecs.Aac.Value,
                        Channels = 2,
                        SegmentName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        PlaylistName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        AllowedLanguages = Languages.AllLanguages()
                    },
                    new()
                    {
                        Codec = AudioCodecs.Eac3.Value,
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
            new EncoderProfile
            {
                Id = Ulid.Parse("01HQ629JAYQDEQAH0GW3ZHGW8Z"),
                Name = "1080p high",
                Container = VideoContainers.Hls,
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    new()
                    {
                        Codec = VideoCodecs.H264Nvenc.Value,
                        Width = FrameSizes._1080p.Width,
                        Crf = 20,
                        SegmentName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        PlaylistName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        ColorSpace = ColorSpaces.Yuv420P,
                        Preset = VideoPresets.Fast,
                        Tune = VideoTunes.Hq,
                        Keyint = 48,
                        CustomArguments =
                        [
                            new ValueTuple<string, string>
                            {
                                Item1 = "-x264opts",
                                Item2 = "no-scenecut"
                            }
                        ]
                    }
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = AudioCodecs.Aac.Value,
                        Channels = 2,
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
            new EncoderProfile
            {
                Id = Ulid.Parse("01HQ629SJ32FTV2Q46NX3H1CK9"),
                Name = "1080p regular",
                Container = VideoContainers.Hls,
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    new()
                    {
                        Codec = VideoCodecs.H264Nvenc.Value,
                        Width = FrameSizes._1080p.Width,
                        Crf = 23,
                        SegmentName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        PlaylistName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        ColorSpace = ColorSpaces.Yuv420P,
                        Preset = VideoPresets.Fast,
                        Tune = VideoTunes.Hq,
                        Keyint = 48,
                        CustomArguments =
                        [
                            new ValueTuple<string, string>
                            {
                                Item1 = "-x264opts",
                                Item2 = "no-scenecut"
                            }
                        ]
                    }
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = AudioCodecs.Aac.Value,
                        Channels = 2,
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
            new EncoderProfile
            {
                Id = Ulid.Parse("01HR360AKTW47XC6ZQ2V9DF024"),
                Name = "1080p low",
                Container = VideoContainers.Hls,
                EncoderProfileFolder = [],
                VideoProfiles =
                [
                    new()
                    {
                        Codec = VideoCodecs.H264Nvenc.Value,
                        Width = FrameSizes._1080p.Width,
                        Crf = 25,
                        SegmentName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        PlaylistName = ":type:_:framesize:_SDR/:type:_:framesize:_SDR",
                        ColorSpace = ColorSpaces.Yuv420P,
                        Preset = VideoPresets.Fast,
                        Tune = VideoTunes.Hq,
                        Keyint = 48,
                        CustomArguments =
                        [
                            new ValueTuple<string, string>
                            {
                                Item1 = "-x264opts",
                                Item2 = "no-scenecut"
                            }
                        ]
                    }
                ],
                AudioProfiles =
                [
                    new()
                    {
                        Codec = AudioCodecs.Aac.Value,
                        Channels = 2,
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
            new EncoderProfile
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
            }
        ];
    }

}