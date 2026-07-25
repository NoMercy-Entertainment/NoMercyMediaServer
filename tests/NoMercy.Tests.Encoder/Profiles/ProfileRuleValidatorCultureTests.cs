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

using System.Globalization;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;
using RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Profiles;

/// <summary>
/// <see cref="ProfileRuleValidator"/> rule messages ride straight through
/// EncoderProfilesController as JSON. On a comma-decimal server locale a bare
/// ":N0" (luma samples/sec) reformats with period-grouping instead of
/// comma-grouping, and a bare ":F2" (source fps) reformats with a comma
/// decimal separator. This pins InvariantCulture on both formatters.
/// </summary>
public class ProfileRuleValidatorCultureTests
{
    private static VideoOutput Video(
        VideoCodecType codec = VideoCodecType.H264,
        int? width = 1920,
        int? height = 1080,
        string? level = null
    ) =>
        new(
            Policy: StreamPolicy.Transcode,
            Codec: codec,
            Width: width,
            Height: height,
            RateControl: RateControlMode.Crf,
            Crf: 23,
            BitrateKbps: 0,
            MaxBitrateKbps: null,
            BufferSizeKbps: null,
            Preset: null,
            CodecProfile: CodecProfile.Auto,
            Level: level,
            Tune: null,
            BitDepth: 8,
            PixelFormat: null,
            KeyframeIntervalSeconds: 2,
            ConvertHdrToSdr: false,
            SegmentNameTemplate: "video_:framesize:/:framesize:_%05d",
            PlaylistNameTemplate: "video_:framesize:/playlist"
        );

    private static EncodingProfile ProfileFor(VideoOutput video) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "rule-validator-culture-test",
            Container: Container.HlsFmp4,
            Video: video,
            Audio: [],
            Subtitles: [],
            SegmentDurationSeconds: 6
        );

    private static MediaInfo Source(int width, int height, double frameRate) =>
        new(
            FilePath: "/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(90),
            OverallBitRateKbps: 5000,
            FileSizeBytes: 0,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "h264",
                    Width: width,
                    Height: height,
                    FrameRate: frameRate,
                    BitDepth: 8,
                    PixelFormat: "yuv420p",
                    ColorPrimaries: null,
                    ColorTransfer: null,
                    ColorSpace: null,
                    IsDefault: true,
                    BitRateKbps: 5000,
                    AverageFrameRate: frameRate,
                    RealFrameRate: frameRate
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static bool HasRule(ValidationEnvelope env, string id) =>
        env.Errors.Any(r => r.Id == id) || env.Warnings.Any(r => r.Id == id);

    [Theory]
    [InlineData("de-DE")]
    [InlineData("nl-NL")]
    [InlineData("fr-FR")]
    public void LevelResolutionMismatch_LumaSamplesUseInvariantGrouping(string culture)
    {
        CultureInfo previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new(culture);

            // 3840 x 2160 x 30 (assumed) = 248,832,000 luma samples/sec —
            // H.264 Level 4.0 caps at 62,914,560, far below.
            EncodingProfile profile = ProfileFor(Video(width: 3840, height: 2160, level: "4.0"));

            ValidationEnvelope env = ProfileRuleValidator.Validate(profile);

            Assert.True(HasRule(env, EncoderRuleId.LevelResolutionMismatch));
            EncoderRule rule = env.Errors.Single(r =>
                r.Id == EncoderRuleId.LevelResolutionMismatch
            );
            Assert.Contains("248,832,000", rule.Message);
            Assert.DoesNotContain("248.832.000", rule.Message);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("nl-NL")]
    [InlineData("fr-FR")]
    public void LevelFrameRateCapExceeded_Message_IsInvariantAcrossCulture(string culture)
    {
        CultureInfo previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new(culture);

            // 3840 x 2160 x 60 = 497,664,000 luma samples/sec — H.264 Level 5.0
            // caps at 150,994,944, far below.
            EncodingProfile profile = ProfileFor(
                Video(codec: VideoCodecType.H264, width: 3840, height: 2160, level: "5.0")
            );
            MediaInfo source = Source(width: 3840, height: 2160, frameRate: 60);

            ValidationEnvelope env = ProfileRuleValidator.ValidateWithSource(profile, source);

            Assert.True(HasRule(env, EncoderRuleId.LevelFrameRateCapExceeded));
            EncoderRule rule = env.Errors.Single(r =>
                r.Id == EncoderRuleId.LevelFrameRateCapExceeded
            );
            Assert.Contains("60.00", rule.Message);
            Assert.Contains("497,664,000", rule.Message);
            Assert.DoesNotContain("60,00", rule.Message);
            Assert.DoesNotContain("497.664.000", rule.Message);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }
}
