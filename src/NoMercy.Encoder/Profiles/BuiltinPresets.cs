using System.Security.Cryptography;
using System.Text;
using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.Profiles;

public static class BuiltinPresets
{
    public static EncodingProfile[] All() =>
        [
            Web1080pBalanced(),
            Web720pFast(),
            Mobile480pLowBandwidth(),
            AbrStandard480_720_1080(),
            AbrPremiumHevc720_1080_2160(),
            AnimeHevc1080p10bit(),
            FullRemuxHls(),
            SmartRemuxHls(),
            DirectStreamAudioHls(),
            CompressHevc1080pMkv(),
            CompressHevc4kMkv(),
            CompressH264_1080pMkv(),
            ArchiveRemuxMkv(),
            MusicFlacLossless(),
            MusicAac256k(),
            MusicMp3_320k(),
            Chromecast1080p(),
            Chromecast4kHevc(),
            AppleTvHd1080pHevc(),
            AppleTv4kHevc(),
            LegacyDeviceH264Baseline(),
            Dash1080pBalanced(),
        ];

    private static Ulid IdFromName(string name)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        return new Ulid(hash.AsSpan(0, 16).ToArray());
    }

    private static EncodingProfile Web1080pBalanced()
    {
        const string Name = "Web 1080p Balanced";
        return new(
            Id: IdFromName(Name),
            Name: Name,
            Container: Container.HlsFmp4,
            Video: new(
                StreamPolicy.Transcode,
                VideoCodecType.H264,
                1920,
                1080,
                RateControlMode.Crf,
                22,
                0,
                null,
                null,
                "medium",
                CodecProfile.High,
                "4.0",
                null,
                8,
                "yuv420p",
                4,
                false,
                "video/{label}",
                "video/{label}/playlist"
            ),
            Audio:
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    192,
                    2,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-{codec}",
                    "audio/{lang}-{codec}/playlist"
                ),
            ],
            Subtitles:
            [
                new(
                    SubtitlePolicy.Extract,
                    SubtitleCodecType.WebVtt,
                    AllowedLanguages.All,
                    true,
                    null,
                    "subs/{lang}"
                ),
            ],
            Hls: new(),
            HlsDerivatives: new()
        )
        {
            Description = "H.264 1080p balanced default for browser playback.",
            IsBuiltin = true,
            ClientCompatibility = ClientCompatibility.Universal,
        };
    }

    private static EncodingProfile Web720pFast()
    {
        const string Name = "Web 720p Fast";
        return new(
            Id: IdFromName(Name),
            Name: Name,
            Container: Container.HlsFmp4,
            Video: new(
                StreamPolicy.Transcode,
                VideoCodecType.H264,
                1280,
                720,
                RateControlMode.Crf,
                23,
                0,
                null,
                null,
                "fast",
                CodecProfile.High,
                "4.0",
                null,
                8,
                "yuv420p",
                4,
                false,
                "video/{label}",
                "video/{label}/playlist"
            ),
            Audio:
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    128,
                    2,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-{codec}",
                    "audio/{lang}-{codec}/playlist"
                ),
            ],
            Subtitles:
            [
                new(
                    SubtitlePolicy.Extract,
                    SubtitleCodecType.WebVtt,
                    AllowedLanguages.All,
                    true,
                    null,
                    "subs/{lang}"
                ),
            ],
            Hls: new(),
            HlsDerivatives: new()
        )
        {
            Description = "H.264 720p fast encode for lower-bandwidth viewers.",
            IsBuiltin = true,
            ClientCompatibility = ClientCompatibility.Universal,
        };
    }

    private static EncodingProfile Mobile480pLowBandwidth()
    {
        const string Name = "Mobile 480p Low Bandwidth";
        return new(
            Id: IdFromName(Name),
            Name: Name,
            Container: Container.HlsFmp4,
            Video: new(
                StreamPolicy.Transcode,
                VideoCodecType.H264,
                854,
                480,
                RateControlMode.Crf,
                26,
                0,
                null,
                null,
                "fast",
                CodecProfile.Main,
                "3.1",
                null,
                8,
                "yuv420p",
                4,
                false,
                "video/{label}",
                "video/{label}/playlist"
            ),
            Audio:
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    96,
                    2,
                    44100,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-{codec}",
                    "audio/{lang}-{codec}/playlist"
                ),
            ],
            Subtitles:
            [
                new(
                    SubtitlePolicy.Extract,
                    SubtitleCodecType.WebVtt,
                    AllowedLanguages.All,
                    true,
                    null,
                    "subs/{lang}"
                ),
            ],
            Hls: new(),
            HlsDerivatives: new() { SpriteVttIntervalSeconds = 20, SpriteVttThumbnailWidth = 80 }
        )
        {
            Description = "H.264 480p for mobile and low-bandwidth connections.",
            IsBuiltin = true,
            ClientCompatibility = ClientCompatibility.Universal,
        };
    }

    private static EncodingProfile AbrStandard480_720_1080()
    {
        const string Name = "ABR Standard 480/720/1080";
        return new(
            Id: IdFromName(Name),
            Name: Name,
            Container: Container.HlsFmp4,
            Video: null,
            Audio:
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    192,
                    2,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-{codec}",
                    "audio/{lang}-{codec}/playlist"
                ),
            ],
            Subtitles:
            [
                new(
                    SubtitlePolicy.Extract,
                    SubtitleCodecType.WebVtt,
                    AllowedLanguages.All,
                    true,
                    null,
                    "subs/{lang}"
                ),
            ],
            Hls: new(),
            HlsDerivatives: new(),
            Ladder: new()
            {
                Mode = LadderMode.Manual,
                Rungs =
                [
                    new(
                        854,
                        480,
                        VideoCodecType.H264,
                        1500,
                        2250,
                        3000,
                        24.0,
                        "medium",
                        CodecProfile.High,
                        8,
                        "yuv420p"
                    ),
                    new(
                        1280,
                        720,
                        VideoCodecType.H264,
                        3000,
                        4500,
                        6000,
                        24.0,
                        "medium",
                        CodecProfile.High,
                        8,
                        "yuv420p"
                    ),
                    new(
                        1920,
                        1080,
                        VideoCodecType.H264,
                        6000,
                        9000,
                        12000,
                        24.0,
                        "medium",
                        CodecProfile.High,
                        8,
                        "yuv420p"
                    ),
                ],
            }
        )
        {
            Description = "Adaptive bitrate H.264 ladder: 480p / 720p / 1080p.",
            IsBuiltin = true,
            ClientCompatibility = ClientCompatibility.Universal,
        };
    }

    private static EncodingProfile AbrPremiumHevc720_1080_2160()
    {
        const string Name = "ABR Premium HEVC 720/1080/2160";
        return new(
            Id: IdFromName(Name),
            Name: Name,
            Container: Container.HlsFmp4,
            Video: null,
            Audio:
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Eac3,
                    448,
                    6,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-eac3",
                    "audio/{lang}-eac3/playlist"
                ),
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    192,
                    2,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-aac",
                    "audio/{lang}-aac/playlist"
                ),
            ],
            Subtitles:
            [
                new(
                    SubtitlePolicy.Extract,
                    SubtitleCodecType.WebVtt,
                    AllowedLanguages.All,
                    true,
                    null,
                    "subs/{lang}"
                ),
            ],
            Hls: new(),
            HlsDerivatives: new() { GenerateIFramePlaylists = true },
            Ladder: new()
            {
                Mode = LadderMode.Manual,
                Rungs =
                [
                    new(
                        1280,
                        720,
                        VideoCodecType.H265,
                        1600,
                        2400,
                        3200,
                        24.0,
                        "slow",
                        CodecProfile.Main10,
                        10,
                        "yuv420p10le"
                    ),
                    new(
                        1920,
                        1080,
                        VideoCodecType.H265,
                        3400,
                        5100,
                        6800,
                        24.0,
                        "slow",
                        CodecProfile.Main10,
                        10,
                        "yuv420p10le"
                    ),
                    new(
                        3840,
                        2160,
                        VideoCodecType.H265,
                        11600,
                        17400,
                        23200,
                        24.0,
                        "slow",
                        CodecProfile.Main10,
                        10,
                        "yuv420p10le"
                    ),
                ],
            }
        )
        {
            Description = "HEVC Main10 4K ABR ladder with EAC3 5.1 surround for premium devices.",
            IsBuiltin = true,
            ClientCompatibility =
                ClientCompatibility.NativeAndroid
                | ClientCompatibility.NativeIos
                | ClientCompatibility.Cast,
        };
    }

    private static EncodingProfile AnimeHevc1080p10bit()
    {
        const string Name = "Anime HEVC 1080p 10-bit";
        return new(
            Id: IdFromName(Name),
            Name: Name,
            Container: Container.HlsFmp4,
            Video: new(
                StreamPolicy.Transcode,
                VideoCodecType.H265,
                1920,
                1080,
                RateControlMode.Crf,
                20,
                0,
                null,
                null,
                "slow",
                CodecProfile.Main10,
                null,
                "animation",
                10,
                "yuv420p10le",
                4,
                false,
                "video/{label}",
                "video/{label}/playlist"
            ),
            Audio:
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    192,
                    2,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-{codec}",
                    "audio/{lang}-{codec}/playlist"
                ),
            ],
            Subtitles:
            [
                new(
                    SubtitlePolicy.Extract,
                    SubtitleCodecType.WebVtt,
                    AllowedLanguages.All,
                    true,
                    null,
                    "subs/{lang}"
                ),
            ],
            Hls: new(),
            HlsDerivatives: new()
        )
        {
            Description = "HEVC Main10 1080p 10-bit tuned for anime with slow preset.",
            IsBuiltin = true,
            ClientCompatibility =
                ClientCompatibility.NativeAndroid
                | ClientCompatibility.NativeIos
                | ClientCompatibility.Cast,
        };
    }

    private static EncodingProfile FullRemuxHls()
    {
        const string Name = "Full Remux HLS";
        return new(
            Id: IdFromName(Name),
            Name: Name,
            Container: Container.HlsFmp4,
            Video: new(
                StreamPolicy.Copy,
                VideoCodecType.Copy,
                0,
                null,
                RateControlMode.Crf,
                0,
                0,
                null,
                null,
                null,
                CodecProfile.Auto,
                null,
                null,
                8,
                null,
                4,
                false,
                "video/copy",
                "video/copy/playlist"
            ),
            Audio:
            [
                new(
                    StreamPolicy.Copy,
                    AudioCodecType.Copy,
                    0,
                    2,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-copy",
                    "audio/{lang}-copy/playlist"
                ),
            ],
            Subtitles:
            [
                new(
                    SubtitlePolicy.Copy,
                    SubtitleCodecType.Copy,
                    AllowedLanguages.All,
                    true,
                    null,
                    "subs/{lang}"
                ),
            ],
            Hls: new(),
            HlsDerivatives: new() { GenerateFontsJson = false }
        )
        {
            Description =
                "Stream-copy remux to HLS fMP4 — no re-encode. Source codec compatibility varies.",
            IsBuiltin = true,
            ClientCompatibility = ClientCompatibility.Universal,
        };
    }

    private static EncodingProfile SmartRemuxHls()
    {
        const string Name = "Smart Remux HLS";
        return new(
            Id: IdFromName(Name),
            Name: Name,
            Container: Container.HlsFmp4,
            Video: new(
                StreamPolicy.Copy,
                VideoCodecType.Copy,
                0,
                null,
                RateControlMode.Crf,
                0,
                0,
                null,
                null,
                null,
                CodecProfile.Auto,
                null,
                null,
                8,
                null,
                4,
                false,
                "video/copy",
                "video/copy/playlist"
            ),
            Audio:
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    192,
                    2,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-{codec}",
                    "audio/{lang}-{codec}/playlist"
                ),
            ],
            Subtitles:
            [
                new(
                    SubtitlePolicy.Extract,
                    SubtitleCodecType.WebVtt,
                    AllowedLanguages.All,
                    true,
                    null,
                    "subs/{lang}"
                ),
            ],
            Hls: new(),
            HlsDerivatives: new()
        )
        {
            Description =
                "Copy video, transcode audio to AAC — smart remux for broad compatibility.",
            IsBuiltin = true,
            ClientCompatibility = ClientCompatibility.Universal,
        };
    }

    private static EncodingProfile DirectStreamAudioHls()
    {
        const string Name = "Direct Stream Audio HLS";
        return new(
            Id: IdFromName(Name),
            Name: Name,
            Container: Container.AudioHlsFmp4,
            Video: null,
            Audio:
            [
                new(
                    StreamPolicy.Copy,
                    AudioCodecType.Aac,
                    192,
                    2,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-copy",
                    "audio/{lang}-copy/playlist"
                ),
            ],
            Subtitles: [],
            Hls: new(),
            HlsDerivatives: new()
            {
                GenerateSpriteVtt = false,
                GenerateThumbnailTrack = false,
                GenerateFontsJson = false,
            }
        )
        {
            Description = "Audio-only HLS stream copy — no video, no re-encode.",
            IsBuiltin = true,
            ClientCompatibility = ClientCompatibility.Universal,
        };
    }

    private static EncodingProfile CompressHevc1080pMkv()
    {
        const string Name = "Compress HEVC 1080p MKV";
        return new(
            Id: IdFromName(Name),
            Name: Name,
            Container: Container.Mkv,
            Video: new(
                StreamPolicy.Transcode,
                VideoCodecType.H265,
                1920,
                1080,
                RateControlMode.Crf,
                18,
                0,
                null,
                null,
                "slow",
                CodecProfile.Main10,
                null,
                null,
                10,
                "yuv420p10le",
                4,
                false,
                "video/{label}",
                "video/{label}/playlist"
            ),
            Audio:
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Flac,
                    0,
                    2,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-{codec}",
                    "audio/{lang}-{codec}/playlist"
                ),
            ],
            Subtitles:
            [
                new(
                    SubtitlePolicy.Copy,
                    SubtitleCodecType.Copy,
                    AllowedLanguages.All,
                    true,
                    null,
                    "subs/{lang}"
                ),
            ]
        )
        {
            Description =
                "HEVC Main10 CRF 18 slow re-encode to MKV — high quality, smaller storage.",
            IsBuiltin = true,
            ClientCompatibility = ClientCompatibility.NativeAndroid | ClientCompatibility.NativeIos,
        };
    }

    private static EncodingProfile CompressHevc4kMkv()
    {
        const string Name = "Compress HEVC 4K MKV";
        return new(
            Id: IdFromName(Name),
            Name: Name,
            Container: Container.Mkv,
            Video: new(
                StreamPolicy.Transcode,
                VideoCodecType.H265,
                3840,
                2160,
                RateControlMode.Crf,
                18,
                0,
                null,
                null,
                "slower",
                CodecProfile.Main10,
                null,
                null,
                10,
                "yuv420p10le",
                4,
                false,
                "video/{label}",
                "video/{label}/playlist"
            ),
            Audio:
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Flac,
                    0,
                    6,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-{codec}",
                    "audio/{lang}-{codec}/playlist"
                ),
            ],
            Subtitles:
            [
                new(
                    SubtitlePolicy.Copy,
                    SubtitleCodecType.Copy,
                    AllowedLanguages.All,
                    true,
                    null,
                    "subs/{lang}"
                ),
            ]
        )
        {
            Description =
                "HEVC Main10 CRF 18 slower 4K re-encode to MKV — maximum quality compression.",
            IsBuiltin = true,
            ClientCompatibility = ClientCompatibility.NativeAndroid | ClientCompatibility.NativeIos,
        };
    }

    private static EncodingProfile CompressH264_1080pMkv()
    {
        const string Name = "Compress H.264 1080p MKV";
        return new(
            Id: IdFromName(Name),
            Name: Name,
            Container: Container.Mkv,
            Video: new(
                StreamPolicy.Transcode,
                VideoCodecType.H264,
                1920,
                1080,
                RateControlMode.Crf,
                20,
                0,
                null,
                null,
                "slow",
                CodecProfile.High,
                "4.0",
                null,
                8,
                "yuv420p",
                4,
                false,
                "video/{label}",
                "video/{label}/playlist"
            ),
            Audio:
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    320,
                    2,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-{codec}",
                    "audio/{lang}-{codec}/playlist"
                ),
            ],
            Subtitles:
            [
                new(
                    SubtitlePolicy.Copy,
                    SubtitleCodecType.Copy,
                    AllowedLanguages.All,
                    true,
                    null,
                    "subs/{lang}"
                ),
            ]
        )
        {
            Description = "H.264 CRF 20 slow 1080p re-encode to MKV with AAC 320k audio.",
            IsBuiltin = true,
            ClientCompatibility = ClientCompatibility.Universal,
        };
    }

    private static EncodingProfile ArchiveRemuxMkv()
    {
        const string Name = "Archive Remux MKV";
        return new(
            Id: IdFromName(Name),
            Name: Name,
            Container: Container.Mkv,
            Video: new(
                StreamPolicy.Copy,
                VideoCodecType.Copy,
                0,
                null,
                RateControlMode.Crf,
                0,
                0,
                null,
                null,
                null,
                CodecProfile.Auto,
                null,
                null,
                8,
                null,
                4,
                false,
                "video/copy",
                "video/copy/playlist"
            ),
            Audio:
            [
                new(
                    StreamPolicy.Copy,
                    AudioCodecType.Copy,
                    0,
                    2,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-copy",
                    "audio/{lang}-copy/playlist"
                ),
            ],
            Subtitles:
            [
                new(
                    SubtitlePolicy.Copy,
                    SubtitleCodecType.Copy,
                    AllowedLanguages.All,
                    true,
                    null,
                    "subs/{lang}"
                ),
            ]
        )
        {
            Description = "Lossless remux to MKV — no re-encode, preserves all source tracks.",
            IsBuiltin = true,
            ClientCompatibility = ClientCompatibility.NativeAndroid | ClientCompatibility.NativeIos,
        };
    }

    private static EncodingProfile MusicFlacLossless()
    {
        const string Name = "Music FLAC Lossless";
        return new(
            Id: IdFromName(Name),
            Name: Name,
            Container: Container.Flac,
            Video: null,
            Audio:
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Flac,
                    0,
                    2,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-{codec}",
                    "audio/{lang}-{codec}/playlist"
                ),
            ],
            Subtitles: []
        )
        {
            Description = "Lossless FLAC stereo export at 48 kHz.",
            IsBuiltin = true,
            ClientCompatibility = ClientCompatibility.Universal,
        };
    }

    private static EncodingProfile MusicAac256k()
    {
        const string Name = "Music AAC 256k";
        return new(
            Id: IdFromName(Name),
            Name: Name,
            Container: Container.Aac,
            Video: null,
            Audio:
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    256,
                    2,
                    44100,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-{codec}",
                    "audio/{lang}-{codec}/playlist"
                ),
            ],
            Subtitles: []
        )
        {
            Description = "AAC 256 kbps stereo 44.1 kHz — good quality streaming audio.",
            IsBuiltin = true,
            ClientCompatibility = ClientCompatibility.Universal,
        };
    }

    private static EncodingProfile MusicMp3_320k()
    {
        const string Name = "Music MP3 320k";
        return new(
            Id: IdFromName(Name),
            Name: Name,
            Container: Container.Mp3,
            Video: null,
            Audio:
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Mp3,
                    320,
                    2,
                    44100,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-{codec}",
                    "audio/{lang}-{codec}/playlist"
                ),
            ],
            Subtitles: []
        )
        {
            Description = "MP3 320 kbps stereo — maximum compatibility for legacy devices.",
            IsBuiltin = true,
            ClientCompatibility = ClientCompatibility.Universal,
        };
    }

    private static EncodingProfile Chromecast1080p()
    {
        const string Name = "Chromecast 1080p";
        return new(
            Id: IdFromName(Name),
            Name: Name,
            Container: Container.HlsFmp4,
            Video: new(
                StreamPolicy.Transcode,
                VideoCodecType.H264,
                1920,
                1080,
                RateControlMode.Crf,
                23,
                0,
                null,
                null,
                "fast",
                CodecProfile.High,
                "4.0",
                null,
                8,
                "yuv420p",
                4,
                false,
                "video/{label}",
                "video/{label}/playlist"
            ),
            Audio:
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    192,
                    2,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-aac",
                    "audio/{lang}-aac/playlist"
                ),
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Ac3,
                    384,
                    6,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-ac3",
                    "audio/{lang}-ac3/playlist"
                ),
            ],
            Subtitles:
            [
                new(
                    SubtitlePolicy.Extract,
                    SubtitleCodecType.WebVtt,
                    AllowedLanguages.All,
                    true,
                    null,
                    "subs/{lang}"
                ),
            ],
            Hls: new(CmafCompatible: false),
            HlsDerivatives: new() { GenerateIFramePlaylists = true }
        )
        {
            Description = "H.264 1080p fast with AAC stereo and AC3 5.1 for Chromecast playback.",
            IsBuiltin = true,
            ClientCompatibility = ClientCompatibility.Cast,
        };
    }

    private static EncodingProfile Chromecast4kHevc()
    {
        const string Name = "Chromecast 4K HEVC";
        return new(
            Id: IdFromName(Name),
            Name: Name,
            Container: Container.HlsFmp4,
            Video: new(
                StreamPolicy.Transcode,
                VideoCodecType.H265,
                3840,
                2160,
                RateControlMode.Crf,
                20,
                0,
                null,
                null,
                "slow",
                CodecProfile.Main10,
                null,
                null,
                10,
                "yuv420p10le",
                4,
                false,
                "video/{label}",
                "video/{label}/playlist"
            ),
            Audio:
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    256,
                    2,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-aac",
                    "audio/{lang}-aac/playlist"
                ),
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Eac3,
                    448,
                    6,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-eac3",
                    "audio/{lang}-eac3/playlist"
                ),
            ],
            Subtitles:
            [
                new(
                    SubtitlePolicy.Extract,
                    SubtitleCodecType.WebVtt,
                    AllowedLanguages.All,
                    true,
                    null,
                    "subs/{lang}"
                ),
            ],
            Hls: new(),
            HlsDerivatives: new() { GenerateIFramePlaylists = true }
        )
        {
            Description =
                "HEVC Main10 4K with AAC stereo and EAC3 5.1 for Chromecast with Google TV.",
            IsBuiltin = true,
            ClientCompatibility = ClientCompatibility.Cast,
        };
    }

    private static EncodingProfile AppleTvHd1080pHevc()
    {
        const string Name = "Apple TV HD 1080p HEVC";
        return new(
            Id: IdFromName(Name),
            Name: Name,
            Container: Container.HlsFmp4,
            Video: new(
                StreamPolicy.Transcode,
                VideoCodecType.H265,
                1920,
                1080,
                RateControlMode.Crf,
                22,
                0,
                null,
                null,
                "medium",
                CodecProfile.Main,
                null,
                null,
                8,
                "yuv420p",
                4,
                false,
                "video/{label}",
                "video/{label}/playlist"
            ),
            Audio:
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    256,
                    2,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-aac",
                    "audio/{lang}-aac/playlist"
                ),
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Eac3,
                    384,
                    6,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-eac3",
                    "audio/{lang}-eac3/playlist"
                ),
            ],
            Subtitles:
            [
                new(
                    SubtitlePolicy.Extract,
                    SubtitleCodecType.WebVtt,
                    AllowedLanguages.All,
                    true,
                    null,
                    "subs/{lang}"
                ),
            ],
            Hls: new(),
            HlsDerivatives: new()
        )
        {
            Description = "HEVC Main 1080p 8-bit with AAC stereo and EAC3 5.1 for Apple TV HD.",
            IsBuiltin = true,
            ClientCompatibility = ClientCompatibility.NativeIos | ClientCompatibility.Cast,
        };
    }

    private static EncodingProfile AppleTv4kHevc()
    {
        const string Name = "Apple TV 4K HEVC";
        return new(
            Id: IdFromName(Name),
            Name: Name,
            Container: Container.HlsFmp4,
            Video: new(
                StreamPolicy.Transcode,
                VideoCodecType.H265,
                3840,
                2160,
                RateControlMode.Crf,
                20,
                0,
                null,
                null,
                "slow",
                CodecProfile.Main10,
                null,
                null,
                10,
                "yuv420p10le",
                4,
                false,
                "video/{label}",
                "video/{label}/playlist"
            ),
            Audio:
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    256,
                    2,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-aac",
                    "audio/{lang}-aac/playlist"
                ),
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Eac3,
                    448,
                    6,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-eac3",
                    "audio/{lang}-eac3/playlist"
                ),
            ],
            Subtitles:
            [
                new(
                    SubtitlePolicy.Extract,
                    SubtitleCodecType.WebVtt,
                    AllowedLanguages.All,
                    true,
                    null,
                    "subs/{lang}"
                ),
            ],
            Hls: new(),
            HlsDerivatives: new()
        )
        {
            Description =
                "HEVC Main10 4K with AAC stereo and EAC3 5.1 Dolby Digital Plus for Apple TV 4K.",
            IsBuiltin = true,
            ClientCompatibility = ClientCompatibility.NativeIos | ClientCompatibility.Cast,
        };
    }

    private static EncodingProfile LegacyDeviceH264Baseline()
    {
        const string Name = "Legacy Device H.264 Baseline";
        return new(
            Id: IdFromName(Name),
            Name: Name,
            Container: Container.Mp4,
            Video: new(
                StreamPolicy.Transcode,
                VideoCodecType.H264,
                1920,
                1080,
                RateControlMode.Crf,
                24,
                0,
                null,
                null,
                "fast",
                CodecProfile.Baseline,
                "4.0",
                null,
                8,
                "yuv420p",
                4,
                false,
                "video/{label}",
                "video/{label}/playlist"
            ),
            Audio:
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    128,
                    2,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-{codec}",
                    "audio/{lang}-{codec}/playlist"
                ),
            ],
            Subtitles:
            [
                new(
                    SubtitlePolicy.Extract,
                    SubtitleCodecType.Srt,
                    AllowedLanguages.All,
                    true,
                    null,
                    "subs/{lang}"
                ),
            ]
        )
        {
            Description =
                "H.264 Baseline 4.0 CRF 24 for legacy devices — maximum compatibility MP4.",
            IsBuiltin = true,
            ClientCompatibility = ClientCompatibility.LegacyDevices,
        };
    }

    private static EncodingProfile Dash1080pBalanced()
    {
        const string Name = "DASH 1080p Balanced";
        return new(
            Id: IdFromName(Name),
            Name: Name,
            Container: Container.Dash,
            Video: new(
                StreamPolicy.Transcode,
                VideoCodecType.H264,
                1920,
                1080,
                RateControlMode.Crf,
                22,
                0,
                null,
                null,
                "medium",
                CodecProfile.High,
                "4.0",
                null,
                8,
                "yuv420p",
                4,
                false,
                "video/{label}",
                "video/{label}/playlist"
            ),
            Audio:
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    192,
                    2,
                    48000,
                    AllowedLanguages.All,
                    null,
                    null,
                    null,
                    "audio/{lang}-{codec}",
                    "audio/{lang}-{codec}/playlist"
                ),
            ],
            Subtitles:
            [
                new(
                    SubtitlePolicy.Extract,
                    SubtitleCodecType.WebVtt,
                    AllowedLanguages.All,
                    true,
                    null,
                    "subs/{lang}"
                ),
            ],
            Dash: new()
        )
        {
            Description = "H.264 1080p balanced for DASH adaptive streaming.",
            IsBuiltin = true,
            ClientCompatibility = ClientCompatibility.Universal,
        };
    }
}
