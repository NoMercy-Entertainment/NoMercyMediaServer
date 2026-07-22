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
            query: "Avatar",
            candidate: "Avatar",
            rank: 0,
            discDurationSec: 0,
            runtimeMin: 162
        );

        score.Should().BeApproximately(expectedValue: 1.0, precision: 0.0001);
    }

    [Fact]
    public void BlendConfidence_NullRuntime_FallsBackToStringSimilarity()
    {
        double score = VideoDiscIdentifier.BlendConfidence(
            query: "Avatar",
            candidate: "Avatar",
            rank: 0,
            discDurationSec: 9720,
            runtimeMin: null
        );

        score.Should().BeApproximately(expectedValue: 1.0, precision: 0.0001);
    }

    [Fact]
    public void BlendConfidence_ExactDurationMatch_BoostsConfidence()
    {
        double exactMatch = VideoDiscIdentifier.BlendConfidence(
            query: "Avatar",
            candidate: "Avatar",
            rank: 0,
            discDurationSec: 9720,
            runtimeMin: 162
        );

        exactMatch.Should().BeApproximately(expectedValue: 1.0, precision: 0.0001);
    }

    [Fact]
    public void BlendConfidence_CloserRuntimeWins()
    {
        int discDurationSec = 1380;

        double scoreA = VideoDiscIdentifier.BlendConfidence(
            query: "Avatar Book 1",
            candidate: "Avatar Book 1",
            rank: 0,
            discDurationSec: discDurationSec,
            runtimeMin: 23
        );

        double scoreB = VideoDiscIdentifier.BlendConfidence(
            query: "Avatar Book 1",
            candidate: "Avatar Book 1",
            rank: 0,
            discDurationSec: discDurationSec,
            runtimeMin: 45
        );

        scoreA.Should().BeGreaterThan(expected: scoreB);
    }

    [Fact]
    public void BlendConfidence_PoorLabelMatchHighRankReducesScore()
    {
        double highRankScore = VideoDiscIdentifier.BlendConfidence(
            query: "Avatar",
            candidate: "Avatar",
            rank: 3,
            discDurationSec: 9720,
            runtimeMin: 162
        );

        double rank0Score = VideoDiscIdentifier.BlendConfidence(
            query: "Avatar",
            candidate: "Avatar",
            rank: 0,
            discDurationSec: 9720,
            runtimeMin: 162
        );

        highRankScore.Should().BeLessThan(expected: rank0Score);
    }

    [Fact]
    public void BlendConfidence_VeryDifferentRuntime_ReducesScore()
    {
        double score = VideoDiscIdentifier.BlendConfidence(
            query: "Movie",
            candidate: "Movie",
            rank: 0,
            discDurationSec: 7200,
            runtimeMin: 30
        );

        score.Should().BeApproximately(expectedValue: 0.6, precision: 0.0001);
    }

    // ── DiscInfo.MainTitleDurationSec ─────────────────────────────────────────

    [Fact]
    public void DiscInfo_MainTitleDurationSec_PrefersIsMainFeatureFlag()
    {
        DiscTitle mainTitle = MakeTitle(index: 1, durationSec: 7200, isMainFeature: true);
        DiscTitle longTitle = MakeTitle(index: 2, durationSec: 9000, isMainFeature: false);

        DiscInfo info = new(
            Type: OpticalDiscType.BluRay,
            DiscLabel: "TEST",
            Titles: [mainTitle, longTitle],
            AudioTracks: null,
            TotalDuration: TimeSpan.FromSeconds(seconds: 16200)
        );

        info.MainTitleDurationSec.Should().Be(expected: 7200);
    }

    [Fact]
    public void DiscInfo_MainTitleDurationSec_FallsBackToLongestWhenNoFlagSet()
    {
        DiscTitle shortTitle = MakeTitle(index: 1, durationSec: 60, isMainFeature: false);
        DiscTitle longTitle = MakeTitle(index: 2, durationSec: 7200, isMainFeature: false);

        DiscInfo info = new(
            Type: OpticalDiscType.BluRay,
            DiscLabel: "TEST",
            Titles: [shortTitle, longTitle],
            AudioTracks: null,
            TotalDuration: TimeSpan.FromSeconds(seconds: 7260)
        );

        info.MainTitleDurationSec.Should().Be(expected: 7200);
    }

    [Fact]
    public void DiscInfo_MainTitleDurationSec_ZeroWhenNoTitles()
    {
        DiscInfo info = new(
            Type: OpticalDiscType.BluRay,
            DiscLabel: "EMPTY",
            Titles: [],
            AudioTracks: null,
            TotalDuration: TimeSpan.Zero
        );

        info.MainTitleDurationSec.Should().Be(expected: 0);
    }

    private static DiscTitle MakeTitle(int index, double durationSec, bool isMainFeature) =>
        new(
            Index: index,
            Name: $"Title {index}",
            Duration: TimeSpan.FromSeconds(value: durationSec),
            VideoStreams: [],
            AudioStreams: [],
            Subtitles: [],
            Chapters: [],
            EstimatedSizeBytes: 0,
            IsMainFeature: isMainFeature
        );
}
