using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;
using V2RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Naming;

internal static class TestProfiles
{
    private static Ulid IdFromName(string name) => Ulid.NewUlid();

    public static EncodingProfile ArchiveRemuxMkv() =>
        new(
            Id: IdFromName("Archive Remux MKV"),
            Name: "Archive Remux MKV",
            Container: Container.Mkv,
            Video: new(
                StreamPolicy.Copy,
                VideoCodecType.Copy,
                0,
                null,
                V2RateControlMode.Crf,
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
            IsBuiltin = true,
        };

    public static EncodingProfile WebHls1080p() =>
        new(
            Id: IdFromName("Web 1080p"),
            Name: "Web 1080p",
            Container: Container.HlsFmp4,
            Video: new(
                StreamPolicy.Transcode,
                VideoCodecType.H264,
                1920,
                1080,
                V2RateControlMode.Crf,
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
            IsBuiltin = true,
        };

    public static EncodingProfile WithName(string name) =>
        new(
            Id: IdFromName(name),
            Name: name,
            Container: Container.HlsFmp4,
            Video: new(
                StreamPolicy.Transcode,
                VideoCodecType.H264,
                1920,
                1080,
                V2RateControlMode.Crf,
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
            Audio: [],
            Subtitles: []
        );
}
