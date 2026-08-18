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

using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

public class LadderTiersTests
{
    [Fact]
    public void AppleHlsRecommended_has_6_tiers()
    {
        LadderTiers.AppleHlsRecommended.Should().HaveCount(6);
    }

    [Fact]
    public void AppleHlsRecommended_labels_are_correct()
    {
        string[] labels = [.. LadderTiers.AppleHlsRecommended.Select(t => t.Label)];
        labels.Should().Equal(["360p", "540p", "720p", "1080p", "1440p", "2160p"]);
    }

    [Fact]
    public void AppleHlsRecommended_heights_are_correct()
    {
        int[] heights = [.. LadderTiers.AppleHlsRecommended.Select(t => t.Height)];
        heights.Should().Equal([360, 540, 720, 1080, 1440, 2160]);
    }

    [Fact]
    public void AppleHlsRecommended_widths_are_correct()
    {
        int[] widths = [.. LadderTiers.AppleHlsRecommended.Select(t => t.Width)];
        widths.Should().Equal([640, 960, 1280, 1920, 2560, 3840]);
    }

    [Fact]
    public void AppleHlsRecommended_H264_bitrates_match_Apple_spec()
    {
        int?[] bitrates =
        [
            .. LadderTiers.AppleHlsRecommended.Select(t => t.RecommendedBitrateH264Kbps),
        ];
        bitrates.Should().Equal([365, 2000, 3000, 6000, 12000, 24000]);
    }

    [Fact]
    public void AppleHlsRecommended_HEVC_bitrates_match_Apple_spec()
    {
        int?[] bitrates =
        [
            .. LadderTiers.AppleHlsRecommended.Select(t => t.RecommendedBitrateHevcKbps),
        ];
        bitrates.Should().Equal([200, 800, 1600, 3400, 6000, 11600]);
    }

    [Fact]
    public void AppleHlsRecommended_AV1_bitrates_match_spec()
    {
        int?[] bitrates =
        [
            .. LadderTiers.AppleHlsRecommended.Select(t => t.RecommendedBitrateAv1Kbps),
        ];
        bitrates.Should().Equal([150, 600, 1200, 2500, 4500, 8000]);
    }

    [Fact]
    public void AppleHlsRecommended_VP9_bitrates_are_65_percent_of_H264()
    {
        // VP9 ≈ 0.65× H.264 per rung (rounded).
        LadderTier[] tiers = LadderTiers.AppleHlsRecommended;
        foreach (LadderTier tier in tiers)
        {
            int expected = (int)Math.Round(tier.RecommendedBitrateH264Kbps!.Value * 0.65);
            tier.RecommendedBitrateVp9Kbps.Should().Be(expected, $"rung {tier.Label}");
        }
    }

    [Fact]
    public void AppleHlsRecommended_VP9_bitrates_are_set()
    {
        LadderTiers
            .AppleHlsRecommended.Select(t => t.RecommendedBitrateVp9Kbps)
            .Should()
            .AllSatisfy(b => b.Should().NotBeNull());
    }

    [Fact]
    public void AppleHlsRecommended_AV1_below_VP9_below_H264_every_rung()
    {
        foreach (LadderTier tier in LadderTiers.AppleHlsRecommended)
        {
            tier.RecommendedBitrateAv1Kbps.Should()
                .BeLessThan(tier.RecommendedBitrateVp9Kbps!.Value);
            tier.RecommendedBitrateVp9Kbps.Should()
                .BeLessThan(tier.RecommendedBitrateH264Kbps!.Value);
        }
    }

    [Fact]
    public void Standard_has_3_tiers()
    {
        LadderTiers.Standard.Should().HaveCount(3);
    }

    [Fact]
    public void Standard_labels_are_correct()
    {
        string[] labels = [.. LadderTiers.Standard.Select(t => t.Label)];
        labels.Should().Equal(["480p", "720p", "1080p"]);
    }

    [Fact]
    public void Standard_H264_bitrates_are_correct()
    {
        int?[] bitrates = [.. LadderTiers.Standard.Select(t => t.RecommendedBitrateH264Kbps)];
        bitrates.Should().Equal([1500, 3000, 6000]);
    }

    [Fact]
    public void Standard_HEVC_bitrates_are_all_null()
    {
        LadderTiers
            .Standard.Select(t => t.RecommendedBitrateHevcKbps)
            .Should()
            .AllSatisfy(b => b.Should().BeNull());
    }

    [Fact]
    public void Premium_has_6_tiers()
    {
        LadderTiers.Premium.Should().HaveCount(6);
    }

    [Fact]
    public void Premium_labels_include_2160p()
    {
        LadderTiers.Premium.Select(t => t.Label).Should().Contain("2160p");
    }

    [Fact]
    public void Premium_HEVC_bitrates_are_set_for_all_tiers()
    {
        LadderTiers
            .Premium.Select(t => t.RecommendedBitrateHevcKbps)
            .Should()
            .AllSatisfy(b => b.Should().NotBeNull());
    }
}
