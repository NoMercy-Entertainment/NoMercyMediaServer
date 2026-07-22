// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;
using RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Profiles.V2;

public class ProfileValidatorV2Tests
{
    private static EncodingProfile MinimalHls() =>
        new(
            Id: Ulid.NewUlid(),
            Name: "test",
            Container: Container.HlsFmp4,
            Video: new(
                Policy: StreamPolicy.Transcode,
                Codec: VideoCodecType.H264,
                Width: 1920,
                Height: 1080,
                RateControl: RateControlMode.Crf,
                Crf: 22,
                BitrateKbps: 0,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: "medium",
                CodecProfile: CodecProfile.High,
                Level: "4.0",
                Tune: null,
                BitDepth: 8,
                PixelFormat: "yuv420p",
                KeyframeIntervalSeconds: 4,
                ConvertHdrToSdr: false,
                SegmentNameTemplate: "",
                PlaylistNameTemplate: ""
            ),
            Audio:
            [
                new(
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: [],
                    DefaultLanguage: null,
                    Loudness: null,
                    Downmix: null,
                    SegmentNameTemplate: "",
                    PlaylistNameTemplate: ""
                ),
            ],
            Subtitles: []
        );

    [Fact]
    public void Valid_minimal_hls_profile_passes()
    {
        ProfileValidationResult result = ProfileValidator.Validate(profile: MinimalHls());
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
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Opus,
                    BitrateKbps: 128,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: [],
                    DefaultLanguage: null,
                    Loudness: null,
                    Downmix: null,
                    SegmentNameTemplate: "",
                    PlaylistNameTemplate: ""
                ),
            ],
        };
        ProfileValidationResult result = ProfileValidator.Validate(profile: profile);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(predicate: e => e.Contains("Mp4") && e.Contains("Opus"));
    }

    [Fact]
    public void Hls_ts_with_hevc_rejects()
    {
        EncodingProfile profile = MinimalHls() with
        {
            Container = Container.HlsTs,
            Video = MinimalHls().Video! with { Codec = VideoCodecType.H265 },
        };
        ProfileValidationResult result = ProfileValidator.Validate(profile: profile);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(predicate: e => e.Contains("HlsTs") && e.Contains("H265"));
    }

    [Fact]
    public void Audio_bitrate_zero_rejects()
    {
        EncodingProfile profile = MinimalHls() with
        {
            Audio =
            [
                new(
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 0,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: [],
                    DefaultLanguage: null,
                    Loudness: null,
                    Downmix: null,
                    SegmentNameTemplate: "",
                    PlaylistNameTemplate: ""
                ),
            ],
        };
        ProfileValidator.Validate(profile: profile).Errors.Should().Contain(predicate: e => e.Contains("BitrateKbps"));
    }

    [Fact]
    public void Manual_ladder_with_no_rungs_rejects()
    {
        EncodingProfile profile = MinimalHls() with
        {
            Ladder = new() { Mode = LadderMode.Manual, Rungs = [] },
        };
        ProfileValidator
            .Validate(profile: profile)
            .Errors.Should()
            .Contain(predicate: e => e.Contains("Manual ladder"));
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
                    new(Width: 1280, Height: 720, Codec: VideoCodecType.H264, BitrateKbps: 5000, MaxBitrateKbps: 7500, BufferSizeKbps: 10000, Framerate: 24.0),
                    new(Width: 1920, Height: 1080, Codec: VideoCodecType.H264, BitrateKbps: 3000, MaxBitrateKbps: 4500, BufferSizeKbps: 6000, Framerate: 24.0),
                ],
            },
        };
        ProfileValidator.Validate(profile: profile).Errors.Should().Contain(predicate: e => e.Contains("ascending"));
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
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Mp3,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: [],
                    DefaultLanguage: null,
                    Loudness: null,
                    Downmix: null,
                    SegmentNameTemplate: "",
                    PlaylistNameTemplate: ""
                ),
            ],
        };
        ProfileValidator
            .Validate(profile: profile)
            .Errors.Should()
            .Contain(predicate: e => e.Contains("CMAF") && e.Contains("Mp3"));
    }

    [Fact]
    public void Every_invalid_container_video_codec_pair_emits_actionable_error()
    {
        foreach (Container container in Enum.GetValues<Container>())
        {
            foreach (VideoCodecType codec in Enum.GetValues<VideoCodecType>())
            {
                if (ContainerCompatibility.SupportsVideo(container: container, codec: codec))
                    continue;
                EncodingProfile profile = MinimalHls() with
                {
                    Container = container,
                    Video = MinimalHls().Video! with { Codec = codec },
                };
                ProfileValidationResult result = ProfileValidator.Validate(profile: profile);
                result.IsValid.Should().BeFalse(because: $"{container}+{codec} must reject");
                result
                    .Errors.Should()
                    .Contain(predicate: e =>
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
                if (ContainerCompatibility.SupportsAudio(container: container, codec: codec))
                    continue;
                EncodingProfile profile = MinimalHls() with
                {
                    Container = container,
                    Audio =
                    [
                        new(
                            Policy: StreamPolicy.Transcode,
                            Codec: codec,
                            BitrateKbps: 192,
                            Channels: 2,
                            SampleRateHz: 48000,
                            AllowedLanguages: [],
                            DefaultLanguage: null,
                            Loudness: null,
                            Downmix: null,
                            SegmentNameTemplate: "",
                            PlaylistNameTemplate: ""
                        ),
                    ],
                };
                ProfileValidationResult result = ProfileValidator.Validate(profile: profile);
                result.IsValid.Should().BeFalse(because: $"{container}+{codec} audio must reject");
                result
                    .Errors.Should()
                    .Contain(predicate: e =>
                        e.Contains(container.ToString())
                        && e.Contains(codec.ToString())
                        && e.Contains("Compatible containers")
                    );
            }
        }
    }

    [Theory]
    [InlineData(data: -1)]
    [InlineData(data: 0)]
    public void Audio_bitrate_non_positive_for_lossy_codec_rejects(int bitrate)
    {
        EncodingProfile profile = MinimalHls() with
        {
            Audio =
            [
                new(
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: bitrate,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: [],
                    DefaultLanguage: null,
                    Loudness: null,
                    Downmix: null,
                    SegmentNameTemplate: "",
                    PlaylistNameTemplate: ""
                ),
            ],
        };
        ProfileValidator.Validate(profile: profile).Errors.Should().NotBeEmpty();
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
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Flac,
                    BitrateKbps: 0,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: [],
                    DefaultLanguage: null,
                    Loudness: null,
                    Downmix: null,
                    SegmentNameTemplate: "",
                    PlaylistNameTemplate: ""
                ),
            ],
        };
        ProfileValidator.Validate(profile: profile).IsValid.Should().BeTrue();
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
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.TrueHd,
                    BitrateKbps: 0,
                    Channels: 6,
                    SampleRateHz: 48000,
                    AllowedLanguages: [],
                    DefaultLanguage: null,
                    Loudness: null,
                    Downmix: null,
                    SegmentNameTemplate: "",
                    PlaylistNameTemplate: ""
                ),
            ],
        };
        ProfileValidator.Validate(profile: profile).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Manual_ladder_with_one_rung_passes()
    {
        EncodingProfile profile = MinimalHls() with
        {
            Ladder = new()
            {
                Mode = LadderMode.Manual,
                Rungs = [new(Width: 1920, Height: 1080, Codec: VideoCodecType.H264, BitrateKbps: 6000, MaxBitrateKbps: 9000, BufferSizeKbps: 12000, Framerate: 24.0)],
            },
        };
        ProfileValidator.Validate(profile: profile).IsValid.Should().BeTrue();
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
                    new(Width: 854, Height: 480, Codec: VideoCodecType.H264, BitrateKbps: 1500, MaxBitrateKbps: 2250, BufferSizeKbps: 3000, Framerate: 24.0),
                    new(Width: 1920, Height: 1080, Codec: VideoCodecType.H264, BitrateKbps: 6000, MaxBitrateKbps: 9000, BufferSizeKbps: 12000, Framerate: 24.0),
                    new(Width: 1280, Height: 720, Codec: VideoCodecType.H264, BitrateKbps: 8000, MaxBitrateKbps: 12000, BufferSizeKbps: 16000, Framerate: 24.0),
                ],
            },
        };
        ProfileValidationResult result = ProfileValidator.Validate(profile: profile);
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
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Opus,
                    BitrateKbps: 128,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: [],
                    DefaultLanguage: null,
                    Loudness: null,
                    Downmix: null,
                    SegmentNameTemplate: "",
                    PlaylistNameTemplate: ""
                ),
            ],
        };
        string error = ProfileValidator.Validate(profile: profile).Errors.Single();
        error.Should().Contain(expected: "Mp4");
        error.Should().Contain(expected: "Opus");
        error.Should().Contain(expected: "does not support");
        error.Should().Contain(expected: "Compatible containers");
    }

    [Fact]
    public void Custom_argument_overriding_codec_warns_but_does_not_reject()
    {
        EncodingProfile profile = MinimalHls() with
        {
            CustomArguments = new() { [key: "c:v"] = "libx265" },
        };
        ProfileValidationResult result = ProfileValidator.Validate(profile: profile);
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(predicate: w => w.Contains("c:v"));
    }
}
