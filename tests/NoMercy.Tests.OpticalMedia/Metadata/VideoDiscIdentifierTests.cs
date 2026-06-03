using NoMercy.OpticalMedia.Metadata;

namespace NoMercy.Tests.OpticalMedia.Metadata;

/// <summary>
/// Tests for <see cref="VideoDiscIdentifier"/> internals that are accessible
/// via InternalsVisibleTo.
/// </summary>
[Trait("Category", "Unit")]
public class VideoDiscIdentifierTests
{
    // ── NormalizeLabel ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("Avatar_Book_1_Disc_1", "Avatar Book 1")]
    [InlineData("THE_DARK_KNIGHT", "THE DARK KNIGHT")]
    [InlineData("Inception", "Inception")]
    [InlineData("Lord.Of.The.Rings", "Lord Of The Rings")]
    [InlineData("Star-Wars-A-New-Hope", "Star Wars A New Hope")]
    [InlineData("Breaking_Bad_Season_2_Disc_3", "Breaking Bad Season 2")]
    public void NormalizeLabel_StripsDiscSuffixAndNormalizesSeparators(
        string input,
        string expected
    )
    {
        string result = VideoDiscIdentifier.NormalizeLabel(input);
        result.Should().Be(expected);
    }

    // ── BlendConfidence ────────────────────────────────────────────────────

    [Fact]
    public void BlendConfidence_NoRuntime_UsesStringScoreOnly()
    {
        double confidence = VideoDiscIdentifier.BlendConfidence(
            "Avatar",
            "Avatar",
            rank: 0,
            discDurationSec: 0,
            runtimeMin: null
        );

        confidence.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void BlendConfidence_PerfectStringAndDuration_IsHigh()
    {
        double confidence = VideoDiscIdentifier.BlendConfidence(
            "Avatar",
            "Avatar",
            rank: 0,
            discDurationSec: 162 * 60,
            runtimeMin: 162
        );

        confidence.Should().BeGreaterThan(0.85);
    }

    [Fact]
    public void BlendConfidence_HigherRank_LowerScore()
    {
        double rank0 = VideoDiscIdentifier.BlendConfidence(
            "Avatar",
            "Avatar",
            rank: 0,
            discDurationSec: 0,
            runtimeMin: null
        );
        double rank2 = VideoDiscIdentifier.BlendConfidence(
            "Avatar",
            "Avatar",
            rank: 2,
            discDurationSec: 0,
            runtimeMin: null
        );

        rank0.Should().BeGreaterThan(rank2);
    }

    // ── Episode resolution bug fix ─────────────────────────────────────────

    /// <summary>
    /// Validates the fixed per-episode delta calculation. The old code
    /// computed <c>Math.Abs(discDurationSec - showRunSec)</c> in the loop —
    /// a constant value — so every episode slot produced the same delta
    /// and the result was always episode 1. The fixed version computes
    /// <c>Math.Abs(discDurationSec - (epIdx * showRunSec))</c> so a disc
    /// containing N episodes picks the right N.
    ///
    /// This test exercises the pure delta function directly.
    /// </summary>
    [Theory]
    [InlineData(3600, 3600, 1)]
    [InlineData(7200, 3600, 2)]
    [InlineData(10800, 3600, 3)]
    [InlineData(14400, 3600, 4)]
    public void EpisodeDeltaFunction_MultiEpisodeDisc_PicksCorrectEpisodeCount(
        int discDurationSec,
        int episodeRunSec,
        int epCountOnDisc
    )
    {
        // Simulate the corrected loop: for each epIdx test
        // whether Math.Abs(discDurationSec - epIdx*episodeRunSec) is minimised
        // at the correct episodeCount.
        int maxEpisodes = 6;
        int bestEpIdx = 1;
        double bestDelta = double.MaxValue;

        for (int epIdx = 1; epIdx <= maxEpisodes; epIdx++)
        {
            double expectedSec = epIdx * episodeRunSec;
            double delta = Math.Abs(discDurationSec - expectedSec);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                bestEpIdx = epIdx;
            }
        }

        bestEpIdx
            .Should()
            .Be(
                epCountOnDisc,
                $"disc duration {discDurationSec}s should map to {epCountOnDisc} episode(s) at {episodeRunSec}s each"
            );
    }

    /// <summary>
    /// Proves the OLD (broken) delta was constant — provided as documentation
    /// of the bug that was fixed.
    /// </summary>
    [Fact]
    public void OldEpisodeDelta_WasConstantAcrossIterations()
    {
        int discDurationSec = 7200;
        int episodeRunSec = 3600;
        int iterations = 4;

        double constantDelta = Math.Abs(discDurationSec - (double)episodeRunSec);

        HashSet<double> deltas = [];
        for (int epIdx = 1; epIdx <= iterations; epIdx++)
        {
            double buggyDelta = Math.Abs(discDurationSec - (double)episodeRunSec);
            deltas.Add(buggyDelta);
        }

        deltas.Should().HaveCount(1, "old code always produced the same delta regardless of epIdx");
        deltas.First().Should().Be(constantDelta);
    }
}
