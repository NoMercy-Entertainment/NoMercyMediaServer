using NoMercy.Database.Models;
using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Data.Logic.Seeds;

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
                EncoderProfileFolder =
                [
                    new()
                    {
                        FolderId = Ulid.Parse("01J8T6PB9JDE801599F7YGPGE8"),
                        EncoderProfileId = Ulid.Parse("01HQ6298ZSZYKJT83WDWTPG4G8")
                    },
                    new()
                    {
                        FolderId = Ulid.Parse("01J8T6PDZYCR8JQ8EVQDGCFK8W"),
                        EncoderProfileId = Ulid.Parse("01HQ6298ZSZYKJT83WDWTPG4G8")
                    }
                ],
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
                        AllowedLanguages = AllLanguages()
                    },
                    new()
                    {
                        Codec = AudioCodecs.Eac3.Value,
                        SegmentName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        PlaylistName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        AllowedLanguages = AllLanguages()
                    }
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = SubtitleCodecs.Webvtt.Value,
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = AllLanguages(),
                    },
                    new()
                    {
                        Codec = SubtitleCodecs.Ass.Value,
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = AllLanguages(),
                    }
                ]
            },
            new EncoderProfile
            {
                Id = Ulid.Parse("01HQ629JAYQDEQAH0GW3ZHGW8Z"),
                Name = "1080p high",
                Container = VideoContainers.Hls,
                EncoderProfileFolder = [
                    new()
                    {
                        FolderId = Ulid.Parse("01J8T6PB9JDE801599F7YGPGE8"),
                    },
                    new()
                    {
                        FolderId = Ulid.Parse("01J8T6PB9JDE801599F7YGPGE8"),
                    }
                ],
                VideoProfiles = [
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
                        CustomArguments = [
                            new ValueTuple<string, string>()
                            {
                                Item1 = "-x264opts",
                                Item2 = "no-scenecut"
                            }
                        ]
                    }
                ],
                AudioProfiles = [
                    new()
                    {
                        Codec = AudioCodecs.Aac.Value,
                        Channels = 2,
                        SegmentName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        PlaylistName = ":type:_:language:_:codec:/:type:_:language:_:codec:",
                        AllowedLanguages = AllLanguages(),
                    },
                ],
                SubtitleProfiles = [
                    new()
                    {
                        Codec = SubtitleCodecs.Webvtt.Value,
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = AllLanguages(),
                    }
                ]
            },
            new EncoderProfile
            {
                Id = Ulid.Parse("01HQ629SJ32FTV2Q46NX3H1CK9"),
                Name = "1080p regular",
                Container = VideoContainers.Hls,
                EncoderProfileFolder =
                [
                    new()
                    {
                        FolderId = Ulid.Parse("01HQ5W78J5ADPV6K0SBZRBGWE3"),
                        EncoderProfileId = Ulid.Parse("01HQ629SJ32FTV2Q46NX3H1CK9")
                    },
                    new()
                    {
                        FolderId = Ulid.Parse("01HQ5W67GRBPHJKNAZMDYKMVXA"),
                        EncoderProfileId = Ulid.Parse("01HQ629SJ32FTV2Q46NX3H1CK9")
                    }
                ],
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
                        AllowedLanguages = AllLanguages()
                    }
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = SubtitleCodecs.Webvtt.Value,
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = AllLanguages(),
                    },
                    new()
                    {
                        Codec = SubtitleCodecs.Ass.Value,
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = AllLanguages(),
                    }
                ]
            },

            new EncoderProfile
            {
                Id = Ulid.Parse("01HR360AKTW47XC6ZQ2V9DF024"),
                Name = "1080p low",
                Container = VideoContainers.Hls,
                EncoderProfileFolder =
                [
                    new()
                    {
                        FolderId = Ulid.Parse("01HQ5W4Y1ZHYZKS87P0AG24ERE"),
                        EncoderProfileId = Ulid.Parse("01HR360AKTW47XC6ZQ2V9DF024")
                    }
                ],
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
                        AllowedLanguages = AllLanguages()
                    }
                ],
                SubtitleProfiles =
                [
                    new()
                    {
                        Codec = SubtitleCodecs.Webvtt.Value,
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = AllLanguages()
                    },
                    new()
                    {
                        Codec = SubtitleCodecs.Ass.Value,
                        PlaylistName = "subtitles/:filename:.:language:.:variant:",
                        AllowedLanguages = AllLanguages()
                    }
                ]
            }
        ];
    }

    public static string[] AllLanguages()
    {
        return
        [
            Languages.Aar, Languages.Abk, Languages.Afr, Languages.Aka, Languages.Alb, Languages.Amh, Languages.Ara,
            Languages.Arg, Languages.Arm, Languages.Asm, Languages.Ava, Languages.Ave, Languages.Aym, Languages.Aze,
            Languages.Bak, Languages.Bam, Languages.Baq, Languages.Bel, Languages.Ben, Languages.Bih, Languages.Bis,
            Languages.Bos, Languages.Bre, Languages.Bul, Languages.Bur, Languages.Cat, Languages.Cha, Languages.Che,
            Languages.Chi, Languages.Chu, Languages.Chv, Languages.Cor, Languages.Cos, Languages.Cre, Languages.Cze,
            Languages.Dan, Languages.Div, Languages.Dut, Languages.Dzo, Languages.Eng, Languages.Epo, Languages.Est,
            Languages.Ewe, Languages.Fao, Languages.Fij, Languages.Fil, Languages.Fin, Languages.Fre, Languages.Fry,
            Languages.Ful, Languages.Geo, Languages.Ger, Languages.Gla, Languages.Gle, Languages.Glg, Languages.Glv,
            Languages.Gre, Languages.Grn, Languages.Gsw, Languages.Guj, Languages.Hat, Languages.Hau, Languages.Haw,
            Languages.Heb, Languages.Her, Languages.Hin, Languages.Hmo, Languages.Hrv, Languages.Hun, Languages.Ibo,
            Languages.Ice, Languages.Ido, Languages.Iii, Languages.Iku, Languages.Ile, Languages.Ina, Languages.Ind, Languages.Ipk,
            Languages.Ita, Languages.Jav, Languages.Jpn, Languages.Kan, Languages.Kas, Languages.Kau, Languages.Kaz,
            Languages.Khm, Languages.Kik, Languages.Kin, Languages.Kir, Languages.Kom, Languages.Kon, Languages.Kor,
            Languages.Kua, Languages.Kur, Languages.Lao, Languages.Lat, Languages.Lav, Languages.Lim, Languages.Lin,
            Languages.Lit, Languages.Ltz, Languages.Lub, Languages.Lug, Languages.Mac, Languages.Mah, Languages.Mal,
            Languages.Mao, Languages.Mar, Languages.May, Languages.Mlg, Languages.Mlt, Languages.Mon, Languages.Nau,
            Languages.Nav, Languages.Nbl, Languages.Nde, Languages.Ndo, Languages.Nep, Languages.Nno, Languages.Nob,
            Languages.Nor, Languages.Nya, Languages.Oci, Languages.Oji, Languages.Ori, Languages.Orm, Languages.Oss,
            Languages.Pan, Languages.Per, Languages.Pli, Languages.Pol, Languages.Por, Languages.Pus, Languages.Que,
            Languages.Roh, Languages.Rum, Languages.Run, Languages.Rus, Languages.Sag, Languages.San, Languages.Sin,
            Languages.Slo, Languages.Slv, Languages.Sme, Languages.Smo, Languages.Sna, Languages.Snd, Languages.Som,
            Languages.Sot, Languages.Spa, Languages.Srd, Languages.Srp, Languages.Ssw, Languages.Sun, Languages.Swa,
            Languages.Swe, Languages.Tah, Languages.Tam, Languages.Tat, Languages.Tel, Languages.Tgk, Languages.Tgl,
            Languages.Tha, Languages.Tib, Languages.Tir, Languages.Ton, Languages.Tsn, Languages.Tso, Languages.Tuk,
            Languages.Tur, Languages.Twi, Languages.Uig, Languages.Ukr, Languages.Urd, Languages.Uzb, Languages.Ven,
            Languages.Vie, Languages.Vol, Languages.Wel, Languages.Wln, Languages.Wol, Languages.Xho, Languages.Yid,
            Languages.Yor, Languages.Zha, Languages.Zul
        ];
    }
}