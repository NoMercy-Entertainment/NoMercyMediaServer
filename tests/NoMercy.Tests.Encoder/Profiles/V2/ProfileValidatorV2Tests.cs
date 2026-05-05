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

    [Fact]
    public void Audio_bitrate_zero_rejects()
    {
        EncodingProfile profile = MinimalHls() with
        {
            Audio =
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    0,
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
        ProfileValidator.Validate(profile).Errors.Should().Contain(e => e.Contains("BitrateKbps"));
    }

    [Fact]
    public void Manual_ladder_with_no_rungs_rejects()
    {
        EncodingProfile profile = MinimalHls() with
        {
            Ladder = new() { Mode = LadderMode.Manual, Rungs = [] },
        };
        ProfileValidator
            .Validate(profile)
            .Errors.Should()
            .Contain(e => e.Contains("Manual ladder"));
    }

    [Fact]
    public void Manual_ladder_unsorted_bitrates_rejects()
    {
        EncodingProfile profile = MinimalHls() with
        {
            Ladder = new()
            {
                Mode = LadderMode.Manual,
                Rungs =
                [
                    new(1280, 720, VideoCodecType.H264, 5000, 7500, 10000, 24.0),
                    new(1920, 1080, VideoCodecType.H264, 3000, 4500, 6000, 24.0),
                ],
            },
        };
        ProfileValidator.Validate(profile).Errors.Should().Contain(e => e.Contains("ascending"));
    }

    [Fact]
    public void Hls_fmp4_with_cmaf_compatible_and_mp3_audio_rejects()
    {
        EncodingProfile profile = MinimalHls() with
        {
            Hls = new() { CmafCompatible = true },
            Audio =
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Mp3,
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
        };
        ProfileValidator
            .Validate(profile)
            .Errors.Should()
            .Contain(e => e.Contains("CMAF") && e.Contains("Mp3"));
    }

    [Fact]
    public void Every_invalid_container_video_codec_pair_emits_actionable_error()
    {
        foreach (Container container in Enum.GetValues<Container>())
        {
            foreach (VideoCodecType codec in Enum.GetValues<VideoCodecType>())
            {
                if (ContainerCompatibility.SupportsVideo(container, codec))
                    continue;
                EncodingProfile profile = MinimalHls() with
                {
                    Container = container,
                    Video = MinimalHls().Video! with { Codec = codec },
                };
                ProfileValidationResult result = ProfileValidator.Validate(profile);
                result.IsValid.Should().BeFalse($"{container}+{codec} must reject");
                result
                    .Errors.Should()
                    .Contain(e =>
                        e.Contains(container.ToString())
                        && e.Contains(codec.ToString())
                        && e.Contains("Compatible containers")
                    );
            }
        }
    }

    [Fact]
    public void Every_invalid_container_audio_codec_pair_emits_actionable_error()
    {
        foreach (Container container in Enum.GetValues<Container>())
        {
            foreach (AudioCodecType codec in Enum.GetValues<AudioCodecType>())
            {
                if (ContainerCompatibility.SupportsAudio(container, codec))
                    continue;
                EncodingProfile profile = MinimalHls() with
                {
                    Container = container,
                    Audio =
                    [
                        new(
                            StreamPolicy.Transcode,
                            codec,
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
                };
                ProfileValidationResult result = ProfileValidator.Validate(profile);
                result.IsValid.Should().BeFalse($"{container}+{codec} audio must reject");
                result
                    .Errors.Should()
                    .Contain(e =>
                        e.Contains(container.ToString())
                        && e.Contains(codec.ToString())
                        && e.Contains("Compatible containers")
                    );
            }
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Audio_bitrate_non_positive_for_lossy_codec_rejects(int bitrate)
    {
        EncodingProfile profile = MinimalHls() with
        {
            Audio =
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    bitrate,
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
        ProfileValidator.Validate(profile).Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Audio_bitrate_zero_for_flac_is_accepted()
    {
        EncodingProfile profile = MinimalHls() with
        {
            Container = Container.Mkv,
            Audio =
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Flac,
                    0,
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
        ProfileValidator.Validate(profile).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Audio_bitrate_zero_for_truehd_is_accepted()
    {
        EncodingProfile profile = MinimalHls() with
        {
            Container = Container.Mkv,
            Audio =
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.TrueHd,
                    0,
                    6,
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
        ProfileValidator.Validate(profile).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Manual_ladder_with_one_rung_passes()
    {
        EncodingProfile profile = MinimalHls() with
        {
            Ladder = new()
            {
                Mode = LadderMode.Manual,
                Rungs = [new(1920, 1080, VideoCodecType.H264, 6000, 9000, 12000, 24.0)],
            },
        };
        ProfileValidator.Validate(profile).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Manual_ladder_with_descending_resolutions_warns_but_allows()
    {
        EncodingProfile profile = MinimalHls() with
        {
            Ladder = new()
            {
                Mode = LadderMode.Manual,
                Rungs =
                [
                    new(854, 480, VideoCodecType.H264, 1500, 2250, 3000, 24.0),
                    new(1920, 1080, VideoCodecType.H264, 6000, 9000, 12000, 24.0),
                    new(1280, 720, VideoCodecType.H264, 8000, 12000, 16000, 24.0),
                ],
            },
        };
        ProfileValidationResult result = ProfileValidator.Validate(profile);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Container_codec_error_names_field_problem_and_suggestion()
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
        string error = ProfileValidator.Validate(profile).Errors.Single();
        error.Should().Contain("Mp4");
        error.Should().Contain("Opus");
        error.Should().Contain("does not support");
        error.Should().Contain("Compatible containers");
    }
}
