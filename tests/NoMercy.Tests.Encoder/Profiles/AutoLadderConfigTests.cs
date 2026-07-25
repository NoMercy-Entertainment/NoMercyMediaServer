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

using Newtonsoft.Json;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

public class AutoLadderConfigTests
{
    [Fact]
    public void Default_Tiers_are_AppleHlsRecommended()
    {
        AutoLadderConfig config = new();
        config.Tiers.Should().BeSameAs(LadderTiers.AppleHlsRecommended);
    }

    [Fact]
    public void Default_BitrateStrategy_is_AppleHlsRecommended()
    {
        AutoLadderConfig config = new();
        config.BitrateStrategy.Should().Be(BitrateStrategy.AppleHlsRecommended);
    }

    [Fact]
    public void Default_Crf_is_22()
    {
        AutoLadderConfig config = new();
        config.Crf.Should().Be(22);
    }

    [Fact]
    public void Default_SourcePercentage_is_50()
    {
        AutoLadderConfig config = new();
        config.SourcePercentage.Should().Be(50.0);
    }

    [Fact]
    public void Default_MaxRungs_is_10()
    {
        AutoLadderConfig config = new();
        config.MaxRungs.Should().Be(10);
    }

    [Fact]
    public void Default_MinRungs_is_1()
    {
        AutoLadderConfig config = new();
        config.MinRungs.Should().Be(1);
    }

    [Fact]
    public void Default_NeverUpscale_is_true()
    {
        AutoLadderConfig config = new();
        config.NeverUpscale.Should().BeTrue();
    }

    [Fact]
    public void Default_NeverUpsource_is_true()
    {
        AutoLadderConfig config = new();
        config.NeverUpsource.Should().BeTrue();
    }

    [Fact]
    public void Default_MinTierGapPercent_is_50()
    {
        AutoLadderConfig config = new();
        config.MinTierGapPercent.Should().Be(50.0);
    }

    [Fact]
    public void Default_CodecPolicy_is_Uniform()
    {
        AutoLadderConfig config = new();
        config.CodecPolicy.Should().Be(LadderCodecPolicy.Uniform);
    }

    [Fact]
    public void Default_LowTierCodec_is_null()
    {
        AutoLadderConfig config = new();
        config.LowTierCodec.Should().BeNull();
    }

    [Fact]
    public void Default_HighTierCodec_is_null()
    {
        AutoLadderConfig config = new();
        config.HighTierCodec.Should().BeNull();
    }

    [Fact]
    public void Default_MixedPolicySplitHeight_is_720()
    {
        AutoLadderConfig config = new();
        config.MixedPolicySplitHeight.Should().Be(720);
    }

    [Fact]
    public void Default_VbrCeilingMultiplier_is_1point5()
    {
        AutoLadderConfig config = new();
        config.VbrCeilingMultiplier.Should().Be(1.5);
    }

    [Fact]
    public void Default_BufferSizeMultiplier_is_2()
    {
        AutoLadderConfig config = new();
        config.BufferSizeMultiplier.Should().Be(2.0);
    }

    [Fact]
    public void Default_ReduceFramerateForLowTiers_is_false()
    {
        AutoLadderConfig config = new();
        config.ReduceFramerateForLowTiers.Should().BeFalse();
    }

    [Fact]
    public void Default_LowTierFramerateMultiplier_is_0point5()
    {
        AutoLadderConfig config = new();
        config.LowTierFramerateMultiplier.Should().Be(0.5);
    }

    [Fact]
    public void Default_LowTierFramerateThresholdHeight_is_480()
    {
        AutoLadderConfig config = new();
        config.LowTierFramerateThresholdHeight.Should().Be(480);
    }

    [Fact]
    public void Records_are_equal_when_fields_match()
    {
        AutoLadderConfig a = new() { Crf = 18, MaxRungs = 3 };
        AutoLadderConfig b = new() { Crf = 18, MaxRungs = 3 };
        a.Should().Be(b);
    }

    [Fact]
    public void Records_are_not_equal_when_fields_differ()
    {
        AutoLadderConfig a = new() { Crf = 18 };
        AutoLadderConfig b = new() { Crf = 22 };
        a.Should().NotBe(b);
    }

    [Fact]
    public void Json_roundtrip_preserves_all_scalar_fields()
    {
        AutoLadderConfig original = new()
        {
            BitrateStrategy = BitrateStrategy.PercentOfSource,
            Crf = 18,
            SourcePercentage = 75.0,
            MaxRungs = 4,
            MinRungs = 2,
            NeverUpscale = false,
            NeverUpsource = false,
            MinTierGapPercent = 30.0,
            CodecPolicy = LadderCodecPolicy.Mixed,
            LowTierCodec = VideoCodecType.H264,
            HighTierCodec = VideoCodecType.H265,
            MixedPolicySplitHeight = 720,
            VbrCeilingMultiplier = 1.2,
            BufferSizeMultiplier = 1.8,
            ReduceFramerateForLowTiers = true,
            LowTierFramerateMultiplier = 0.5,
            LowTierFramerateThresholdHeight = 480,
        };

        string json = JsonConvert.SerializeObject(original);
        AutoLadderConfig? restored = JsonConvert.DeserializeObject<AutoLadderConfig>(json);

        restored.Should().NotBeNull();
        restored!.BitrateStrategy.Should().Be(BitrateStrategy.PercentOfSource);
        restored.Crf.Should().Be(18);
        restored.SourcePercentage.Should().Be(75.0);
        restored.MaxRungs.Should().Be(4);
        restored.MinRungs.Should().Be(2);
        restored.NeverUpscale.Should().BeFalse();
        restored.NeverUpsource.Should().BeFalse();
        restored.MinTierGapPercent.Should().Be(30.0);
        restored.CodecPolicy.Should().Be(LadderCodecPolicy.Mixed);
        restored.LowTierCodec.Should().Be(VideoCodecType.H264);
        restored.HighTierCodec.Should().Be(VideoCodecType.H265);
        restored.MixedPolicySplitHeight.Should().Be(720);
        restored.VbrCeilingMultiplier.Should().Be(1.2);
        restored.BufferSizeMultiplier.Should().Be(1.8);
        restored.ReduceFramerateForLowTiers.Should().BeTrue();
        restored.LowTierFramerateMultiplier.Should().Be(0.5);
        restored.LowTierFramerateThresholdHeight.Should().Be(480);
    }
}
