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
            Ulid.NewUlid(),
            Name: "culture-test",
            Container: Container.HlsFmp4,
            Video: null,
            Audio: [],
            Subtitles: [],
            Ladder: new() { Mode = LadderMode.Auto, AutoConfig = config }
        );

    [Theory]
    [InlineData("de-DE")]
    [InlineData("nl-NL")]
    [InlineData("fr-FR")]
    public void SourcePercentageError_StaysPeriodDecimalUnderCommaCulture(string culture)
    {
        CultureInfo previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new(culture);

            EncodingProfile profile = ProfileWithAutoLadder(
                new()
                {
                    Tiers = LadderTiers.AppleHlsRecommended,
                    BitrateStrategy = BitrateStrategy.PercentOfSource,
                    SourcePercentage = 250.5,
                }
            );

            ProfileValidationResult result = ProfileValidator.Validate(profile);

            result.Errors.Should().Contain(e => e.Contains("250.5"));
            result.Errors.Should().NotContain(e => e.Contains("250,5"));
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
    public void LowTierFramerateMultiplierError_StaysPeriodDecimalUnderCommaCulture(string culture)
    {
        CultureInfo previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new(culture);

            EncodingProfile profile = ProfileWithAutoLadder(
                new() { Tiers = LadderTiers.AppleHlsRecommended, LowTierFramerateMultiplier = 1.5 }
            );

            ProfileValidationResult result = ProfileValidator.Validate(profile);

            result.Errors.Should().Contain(e => e.Contains("1.5"));
            result.Errors.Should().NotContain(e => e.Contains("1,5"));
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
    public void LevelCapExceededError_LumaSamplesUseInvariantGrouping(string culture)
    {
        CultureInfo previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new(culture);

            // 1920 x 1080 x 240 = 497,664,000 luma samples/sec — H.264 Level 4.2
            // caps at 133,693,440, far below.
            MediaInfo source = Source(1920, 1080, 240.0);
            EncodingProfile profile = TranscodeProfile(VideoCodecType.H264, "4.2");

            ProfileValidationResult result = ProfileValidator.ValidateWithSource(profile, source);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("497,664,000"));
            result.Errors.Should().NotContain(e => e.Contains("497.664.000"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    private static MediaInfo Source(int width, int height, double fps) =>
        new(
            "/media/test.mkv",
            "matroska",
            TimeSpan.FromHours(2),
            8000,
            7_200_000_000,
            [
                new(
                    0,
                    "h264",
                    width,
                    height,
                    fps,
                    8,
                    "yuv420p",
                    null,
                    null,
                    null,
                    true,
                    8000,
                    fps
                ),
            ],
            [],
            [],
            []
        );

    private static EncodingProfile TranscodeProfile(VideoCodecType codec, string level) =>
        new(
            Ulid.NewUlid(),
            "culture-test",
            Container.Mkv,
            new(
                StreamPolicy.Transcode,
                codec,
                1920,
                1080,
                RateControlMode.Crf,
                23,
                0,
                null,
                null,
                "medium",
                CodecProfile.High,
                level,
                null,
                8,
                null,
                2,
                false,
                "video/video",
                "video/video"
            ),
            [],
            []
        );
}
