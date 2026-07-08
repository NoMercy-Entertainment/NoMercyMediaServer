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

namespace NoMercy.Tests.Encoder.Profiles;

/// <summary>
/// Tests for the 7 AutoLadderConfig validation rules added in Phase C.
/// </summary>
public class AutoLadderValidatorTests
{
    // ── Shared builder ────────────────────────────────────────────────────────

    private static EncodingProfile ProfileWithAutoLadder(AutoLadderConfig config) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "auto-ladder-test",
            Container: Container.HlsFmp4,
            Video: null,
            Audio: [],
            Subtitles: [],
            Ladder: new() { Mode = LadderMode.Auto, AutoConfig = config }
        );

    // ── Rule 1 — MinRungs > MaxRungs ─────────────────────────────────────────

    [Fact]
    public void Rule1_MinRungs_exceeds_MaxRungs_rejects()
    {
        EncodingProfile profile = ProfileWithAutoLadder(
            new() { MinRungs = 5, MaxRungs = 3 }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("MinRungs") && e.Contains("MaxRungs"));
    }

    [Fact]
    public void Rule1_MinRungs_equal_to_MaxRungs_passes()
    {
        EncodingProfile profile = ProfileWithAutoLadder(
            new()
            {
                MinRungs = 3,
                MaxRungs = 3,
                Tiers = LadderTiers.AppleHlsRecommended,
            }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Contains("MinRungs") && e.Contains("MaxRungs"));
    }

    // ── Rule 2 — AppleHlsRecommended + tier with all-null bitrates ───────────

    [Fact]
    public void Rule2_AppleHlsStrategy_AllNullBitrates_rejects()
    {
        LadderTier[] tiers = [new(1920, 1080, "1080p", null, null, null)];
        EncodingProfile profile = ProfileWithAutoLadder(
            new()
            {
                Tiers = tiers,
                BitrateStrategy = BitrateStrategy.AppleHlsRecommended,
            }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e => e.Contains("1080p") && e.Contains("missing recommended bitrate"));
    }

    [Fact]
    public void Rule2_PercentOfSource_AllNullBitrates_passes()
    {
        LadderTier[] tiers = [new(1920, 1080, "1080p", null, null, null)];
        EncodingProfile profile = ProfileWithAutoLadder(
            new()
            {
                Tiers = tiers,
                BitrateStrategy = BitrateStrategy.PercentOfSource,
            }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Contains("missing recommended bitrate"));
    }

    // ── Rule 3 — CrfBased + Crf out of range ─────────────────────────────────

    [Theory]
    [InlineData(-1)]
    [InlineData(52)]
    public void Rule3_CrfBased_OutOfRange_rejects(int crf)
    {
        EncodingProfile profile = ProfileWithAutoLadder(
            new()
            {
                Tiers = LadderTiers.AppleHlsRecommended,
                BitrateStrategy = BitrateStrategy.CrfBased,
                Crf = crf,
            }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Crf") && e.Contains("[0, 51]"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(22)]
    [InlineData(51)]
    public void Rule3_CrfBased_InRange_passes(int crf)
    {
        EncodingProfile profile = ProfileWithAutoLadder(
            new()
            {
                Tiers = LadderTiers.AppleHlsRecommended,
                BitrateStrategy = BitrateStrategy.CrfBased,
                Crf = crf,
            }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Contains("Crf") && e.Contains("[0, 51]"));
    }

    // ── Rule 4 — PercentOfSource + SourcePercentage out of range ─────────────

    [Theory]
    [InlineData(0.0)]
    [InlineData(-10.0)]
    [InlineData(201.0)]
    public void Rule4_PercentOfSource_OutOfRange_rejects(double pct)
    {
        EncodingProfile profile = ProfileWithAutoLadder(
            new()
            {
                Tiers = LadderTiers.AppleHlsRecommended,
                BitrateStrategy = BitrateStrategy.PercentOfSource,
                SourcePercentage = pct,
            }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e => e.Contains("SourcePercentage") && e.Contains("(0, 200]"));
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(50.0)]
    [InlineData(200.0)]
    public void Rule4_PercentOfSource_InRange_passes(double pct)
    {
        EncodingProfile profile = ProfileWithAutoLadder(
            new()
            {
                Tiers = LadderTiers.AppleHlsRecommended,
                BitrateStrategy = BitrateStrategy.PercentOfSource,
                SourcePercentage = pct,
            }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result
            .Errors.Should()
            .NotContain(e => e.Contains("SourcePercentage") && e.Contains("(0, 200]"));
    }

    // ── Rule 5 — Mixed codec policy without both codec types ─────────────────

    [Fact]
    public void Rule5_MixedPolicy_NullLowTierCodec_rejects()
    {
        EncodingProfile profile = ProfileWithAutoLadder(
            new()
            {
                Tiers = LadderTiers.AppleHlsRecommended,
                CodecPolicy = LadderCodecPolicy.Mixed,
                LowTierCodec = null,
                HighTierCodec = VideoCodecType.H265,
            }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e =>
                e.Contains("Mixed") && e.Contains("LowTierCodec") && e.Contains("HighTierCodec")
            );
    }

    [Fact]
    public void Rule5_MixedPolicy_NullHighTierCodec_rejects()
    {
        EncodingProfile profile = ProfileWithAutoLadder(
            new()
            {
                Tiers = LadderTiers.AppleHlsRecommended,
                CodecPolicy = LadderCodecPolicy.Mixed,
                LowTierCodec = VideoCodecType.H264,
                HighTierCodec = null,
            }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e =>
                e.Contains("Mixed") && e.Contains("LowTierCodec") && e.Contains("HighTierCodec")
            );
    }

    [Fact]
    public void Rule5_MixedPolicy_BothCodecsSet_passes()
    {
        EncodingProfile profile = ProfileWithAutoLadder(
            new()
            {
                Tiers = LadderTiers.AppleHlsRecommended,
                CodecPolicy = LadderCodecPolicy.Mixed,
                LowTierCodec = VideoCodecType.H264,
                HighTierCodec = VideoCodecType.H265,
            }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result
            .Errors.Should()
            .NotContain(e =>
                e.Contains("Mixed") && e.Contains("LowTierCodec") && e.Contains("HighTierCodec")
            );
    }

    // ── Rule 6 — Empty Tiers array ───────────────────────────────────────────

    [Fact]
    public void Rule6_EmptyTiers_rejects()
    {
        EncodingProfile profile = ProfileWithAutoLadder(new() { Tiers = [] });

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Tiers") && e.Contains("cannot be empty"));
    }

    [Fact]
    public void Rule6_NonEmptyTiers_passes()
    {
        EncodingProfile profile = ProfileWithAutoLadder(
            new() { Tiers = LadderTiers.Standard }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result
            .Errors.Should()
            .NotContain(e => e.Contains("Tiers") && e.Contains("cannot be empty"));
    }

    // ── Rule 7 — LowTierFramerateMultiplier out of range ─────────────────────

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Rule7_LowTierFramerateMultiplier_OutOfRange_rejects(double multiplier)
    {
        EncodingProfile profile = ProfileWithAutoLadder(
            new()
            {
                Tiers = LadderTiers.AppleHlsRecommended,
                LowTierFramerateMultiplier = multiplier,
            }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e => e.Contains("LowTierFramerateMultiplier") && e.Contains("(0, 1.0]"));
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Rule7_LowTierFramerateMultiplier_InRange_passes(double multiplier)
    {
        EncodingProfile profile = ProfileWithAutoLadder(
            new()
            {
                Tiers = LadderTiers.AppleHlsRecommended,
                LowTierFramerateMultiplier = multiplier,
            }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result
            .Errors.Should()
            .NotContain(e => e.Contains("LowTierFramerateMultiplier") && e.Contains("(0, 1.0]"));
    }
}
