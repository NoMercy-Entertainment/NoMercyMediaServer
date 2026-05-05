using FluentAssertions;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles.V2;
using RateControlMode = NoMercy.Encoder.Profiles.V2.RateControlMode;

namespace NoMercy.Tests.Encoder.Profiles.V2;

public class ProfileValidatorV2Tests
{
    private static EncodingProfile MinimalHls() =>
        new(
            Id: Ulid.NewUlid(),
            Name: "test",
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
                "",
                ""
            ),
            Audio:
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    192,
                    2,
                    48000,
                    [],
                    null,
                    null,
                    null,
                    "",
                    ""
                ),
            ],
            Subtitles: []
        );

    [Fact]
    public void Valid_minimal_hls_profile_passes()
    {
        ProfileValidationResult result = ProfileValidator.Validate(MinimalHls());
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Mp4_with_opus_audio_rejects()
    {
        EncodingProfile profile = MinimalHls() with
        {
            Container = Container.Mp4,
            Audio =
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Opus,
                    128,
                    2,
                    48000,
                    [],
                    null,
                    null,
                    null,
                    "",
                    ""
                ),
            ],
        };
        ProfileValidationResult result = ProfileValidator.Validate(profile);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Mp4") && e.Contains("Opus"));
    }

    [Fact]
    public void Hls_ts_with_hevc_rejects()
    {
        EncodingProfile profile = MinimalHls() with
        {
            Container = Container.HlsTs,
            Video = MinimalHls().Video! with { Codec = VideoCodecType.H265 },
        };
        ProfileValidationResult result = ProfileValidator.Validate(profile);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("HlsTs") && e.Contains("H265"));
    }
}
