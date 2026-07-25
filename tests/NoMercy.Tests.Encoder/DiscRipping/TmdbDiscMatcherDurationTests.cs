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

using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Sources;

namespace NoMercy.Tests.Encoder.DiscRipping;

public class TmdbDiscMatcherDurationTests
{
    // ── BlendConfidence ──────────────────────────────────────────────────────

    [Fact]
    public void BlendConfidence_NoDuration_FallsBackToStringSimilarity()
    {
        double score = VideoDiscIdentifier.BlendConfidence(
            "Avatar",
            "Avatar",
            0,
            0,
            162
        );

        score.Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public void BlendConfidence_NullRuntime_FallsBackToStringSimilarity()
    {
        double score = VideoDiscIdentifier.BlendConfidence(
            "Avatar",
            "Avatar",
            0,
            9720,
            null
        );

        score.Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public void BlendConfidence_ExactDurationMatch_BoostsConfidence()
    {
        double exactMatch = VideoDiscIdentifier.BlendConfidence(
            "Avatar",
            "Avatar",
            0,
            9720,
            162
        );

        exactMatch.Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public void BlendConfidence_CloserRuntimeWins()
    {
        int discDurationSec = 1380;

        double scoreA = VideoDiscIdentifier.BlendConfidence(
            "Avatar Book 1",
            "Avatar Book 1",
            0,
            discDurationSec,
            23
        );

        double scoreB = VideoDiscIdentifier.BlendConfidence(
            "Avatar Book 1",
            "Avatar Book 1",
            0,
            discDurationSec,
            45
        );

        scoreA.Should().BeGreaterThan(scoreB);
    }

    [Fact]
    public void BlendConfidence_PoorLabelMatchHighRankReducesScore()
    {
        double highRankScore = VideoDiscIdentifier.BlendConfidence(
            "Avatar",
            "Avatar",
            3,
            9720,
            162
        );

        double rank0Score = VideoDiscIdentifier.BlendConfidence(
            "Avatar",
            "Avatar",
            0,
            9720,
            162
        );

        highRankScore.Should().BeLessThan(rank0Score);
    }

    [Fact]
    public void BlendConfidence_VeryDifferentRuntime_ReducesScore()
    {
        double score = VideoDiscIdentifier.BlendConfidence(
            "Movie",
            "Movie",
            0,
            7200,
            30
        );

        score.Should().BeApproximately(0.6, 0.0001);
    }

    // ── DiscInfo.MainTitleDurationSec ─────────────────────────────────────────

    [Fact]
    public void DiscInfo_MainTitleDurationSec_PrefersIsMainFeatureFlag()
    {
        DiscTitle mainTitle = MakeTitle(1, 7200, true);
        DiscTitle longTitle = MakeTitle(2, 9000, false);

        DiscInfo info = new(
            OpticalDiscType.BluRay,
            "TEST",
            [mainTitle, longTitle],
            null,
            TimeSpan.FromSeconds(16200)
        );

        info.MainTitleDurationSec.Should().Be(7200);
    }

    [Fact]
    public void DiscInfo_MainTitleDurationSec_FallsBackToLongestWhenNoFlagSet()
    {
        DiscTitle shortTitle = MakeTitle(1, 60, false);
        DiscTitle longTitle = MakeTitle(2, 7200, false);

        DiscInfo info = new(
            OpticalDiscType.BluRay,
            "TEST",
            [shortTitle, longTitle],
            null,
            TimeSpan.FromSeconds(7260)
        );

        info.MainTitleDurationSec.Should().Be(7200);
    }

    [Fact]
    public void DiscInfo_MainTitleDurationSec_ZeroWhenNoTitles()
    {
        DiscInfo info = new(
            OpticalDiscType.BluRay,
            "EMPTY",
            [],
            null,
            TimeSpan.Zero
        );

        info.MainTitleDurationSec.Should().Be(0);
    }

    private static DiscTitle MakeTitle(int index, double durationSec, bool isMainFeature) =>
        new(
            index,
            $"Title {index}",
            TimeSpan.FromSeconds(durationSec),
            [],
            [],
            [],
            [],
            0,
            isMainFeature
        );
}
