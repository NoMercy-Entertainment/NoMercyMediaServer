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
using NoMercy.Encoder.Profiles;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;

namespace NoMercy.Tests.Encoder.Profiles;

/// <summary>
/// <see cref="ProfileValidator"/> errors/warnings ride straight through
/// EncoderProfilesController as JSON — an admin editing a profile reads the
/// out-of-range value back out of the error text. On a comma-decimal server
/// locale, a bare interpolation of a double (SourcePercentage,
/// LowTierFramerateMultiplier) or a bare ":N0" (luma samples/sec) reformats
/// those numbers with a comma decimal / period grouping instead of the
/// period-decimal / comma-grouping the rest of the API assumes. This pins
/// InvariantCulture on every formatter in the class.
/// </summary>
public class ProfileValidatorCultureTests
{
    private static EncodingProfile ProfileWithAutoLadder(AutoLadderConfig config) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "culture-test",
            Container: Container.HlsFmp4,
            Video: null,
            Audio: [],
            Subtitles: [],
            Ladder: new() { Mode = LadderMode.Auto, AutoConfig = config }
        );

    [Theory]
    [InlineData(data: "de-DE")]
    [InlineData(data: "nl-NL")]
    [InlineData(data: "fr-FR")]
    public void SourcePercentageError_StaysPeriodDecimalUnderCommaCulture(string culture)
    {
        CultureInfo previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new(name: culture);

            EncodingProfile profile = ProfileWithAutoLadder(
                config: new()
                {
                    Tiers = LadderTiers.AppleHlsRecommended,
                    BitrateStrategy = BitrateStrategy.PercentOfSource,
                    SourcePercentage = 250.5,
                }
            );

            ProfileValidationResult result = ProfileValidator.Validate(profile: profile);

            result.Errors.Should().Contain(predicate: e => e.Contains("250.5"));
            result.Errors.Should().NotContain(predicate: e => e.Contains("250,5"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(data: "de-DE")]
    [InlineData(data: "nl-NL")]
    [InlineData(data: "fr-FR")]
    public void LowTierFramerateMultiplierError_StaysPeriodDecimalUnderCommaCulture(string culture)
    {
        CultureInfo previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new(name: culture);

            EncodingProfile profile = ProfileWithAutoLadder(
                config: new() { Tiers = LadderTiers.AppleHlsRecommended, LowTierFramerateMultiplier = 1.5 }
            );

            ProfileValidationResult result = ProfileValidator.Validate(profile: profile);

            result.Errors.Should().Contain(predicate: e => e.Contains("1.5"));
            result.Errors.Should().NotContain(predicate: e => e.Contains("1,5"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(data: "de-DE")]
    [InlineData(data: "nl-NL")]
    [InlineData(data: "fr-FR")]
    public void LevelCapExceededError_LumaSamplesUseInvariantGrouping(string culture)
    {
        CultureInfo previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new(name: culture);

            // 1920 x 1080 x 240 = 497,664,000 luma samples/sec — H.264 Level 4.2
            // caps at 133,693,440, far below.
            MediaInfo source = Source(width: 1920, height: 1080, fps: 240.0);
            EncodingProfile profile = TranscodeProfile(codec: VideoCodecType.H264, level: "4.2");

            ProfileValidationResult result = ProfileValidator.ValidateWithSource(profile: profile, source: source);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(predicate: e => e.Contains("497,664,000"));
            result.Errors.Should().NotContain(predicate: e => e.Contains("497.664.000"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    private static MediaInfo Source(int width, int height, double fps) =>
        new(
            FilePath: "/media/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(hours: 2),
            OverallBitRateKbps: 8000,
            FileSizeBytes: 7_200_000_000,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "h264",
                    Width: width,
                    Height: height,
                    FrameRate: fps,
                    BitDepth: 8,
                    PixelFormat: "yuv420p",
                    ColorPrimaries: null,
                    ColorTransfer: null,
                    ColorSpace: null,
                    IsDefault: true,
                    BitRateKbps: 8000,
                    AverageFrameRate: fps
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static EncodingProfile TranscodeProfile(VideoCodecType codec, string level) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "culture-test",
            Container: Container.Mkv,
            Video: new(
                Policy: StreamPolicy.Transcode,
                Codec: codec,
                Width: 1920,
                Height: 1080,
                RateControl: RateControlMode.Crf,
                Crf: 23,
                BitrateKbps: 0,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: "medium",
                CodecProfile: CodecProfile.High,
                Level: level,
                Tune: null,
                BitDepth: 8,
                PixelFormat: null,
                KeyframeIntervalSeconds: 2,
                ConvertHdrToSdr: false,
                SegmentNameTemplate: "video/video",
                PlaylistNameTemplate: "video/video"
            ),
            Audio: [],
            Subtitles: []
        );
}
