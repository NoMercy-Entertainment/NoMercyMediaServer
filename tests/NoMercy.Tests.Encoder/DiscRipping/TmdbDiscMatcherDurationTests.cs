using NoMercy.Encoder.Analysis;
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
        // discDurationSec = 0 → blend is pure string similarity (no runtime component).
        double score = TmdbDiscMatcher.BlendConfidence(
            query: "Avatar",
            candidate: "Avatar",
            rank: 0,
            discDurationSec: 0,
            runtimeMin: 162
        );

        // perfect token match at rank 0 → similarity = 1.0, rankPenalty = 1.0
        score.Should().BeApproximately(1.0, precision: 0.0001);
    }

    [Fact]
    public void BlendConfidence_NullRuntime_FallsBackToStringSimilarity()
    {
        double score = TmdbDiscMatcher.BlendConfidence(
            query: "Avatar",
            candidate: "Avatar",
            rank: 0,
            discDurationSec: 9720,
            runtimeMin: null
        );

        score.Should().BeApproximately(1.0, precision: 0.0001);
    }

    [Fact]
    public void BlendConfidence_ExactDurationMatch_BoostsConfidence()
    {
        // Disc = 9720 s (162 min), candidate runtime = 162 min → durScore = 1.0.
        double exactMatch = TmdbDiscMatcher.BlendConfidence(
            query: "Avatar",
            candidate: "Avatar",
            rank: 0,
            discDurationSec: 9720,
            runtimeMin: 162
        );

        // strScore = 1.0 * 1.0 = 1.0, durScore = 1.0 → blended = 0.6 + 0.4 = 1.0
        exactMatch.Should().BeApproximately(1.0, precision: 0.0001);
    }

    [Fact]
    public void BlendConfidence_CloserRuntimeWins()
    {
        // Disc duration = 1380 s (23 min).
        // Candidate A: runtime 23 min (exact match)
        // Candidate B: runtime 45 min (off by 22 min)
        int discDurationSec = 1380;

        double scoreA = TmdbDiscMatcher.BlendConfidence(
            query: "Avatar Book 1",
            candidate: "Avatar Book 1",
            rank: 0,
            discDurationSec: discDurationSec,
            runtimeMin: 23
        );

        double scoreB = TmdbDiscMatcher.BlendConfidence(
            query: "Avatar Book 1",
            candidate: "Avatar Book 1",
            rank: 0,
            discDurationSec: discDurationSec,
            runtimeMin: 45
        );

        scoreA.Should().BeGreaterThan(scoreB);
    }

    [Fact]
    public void BlendConfidence_PoorLabelMatchHighRankReducesScore()
    {
        // Even with a good runtime match, rank=3 cuts the string component.
        double highRankScore = TmdbDiscMatcher.BlendConfidence(
            query: "Avatar",
            candidate: "Avatar",
            rank: 3,
            discDurationSec: 9720,
            runtimeMin: 162
        );

        double rank0Score = TmdbDiscMatcher.BlendConfidence(
            query: "Avatar",
            candidate: "Avatar",
            rank: 0,
            discDurationSec: 9720,
            runtimeMin: 162
        );

        highRankScore.Should().BeLessThan(rank0Score);
    }

    [Fact]
    public void BlendConfidence_VeryDifferentRuntime_ReducesScore()
    {
        // Disc = 7200 s (120 min), candidate runtime = 30 min → big delta.
        double score = TmdbDiscMatcher.BlendConfidence(
            query: "Movie",
            candidate: "Movie",
            rank: 0,
            discDurationSec: 7200,
            runtimeMin: 30
        );

        // durDelta = |7200 - 1800| = 5400, runtimeSec = 1800
        // durScore = 1 - clamp(5400/1800, 0, 1) = 1 - 1 = 0
        // blended = 0.6 * 1.0 + 0.4 * 0 = 0.6
        score.Should().BeApproximately(0.6, precision: 0.0001);
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
            TotalDuration: TimeSpan.FromSeconds(16200)
        );

        info.MainTitleDurationSec.Should().Be(7200);
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
            TotalDuration: TimeSpan.FromSeconds(7260)
        );

        info.MainTitleDurationSec.Should().Be(7200);
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

        info.MainTitleDurationSec.Should().Be(0);
    }

    private static DiscTitle MakeTitle(int index, double durationSec, bool isMainFeature) =>
        new(
            Index: index,
            Name: $"Title {index}",
            Duration: TimeSpan.FromSeconds(durationSec),
            VideoStreams: [],
            AudioStreams: [],
            Subtitles: [],
            Chapters: [],
            EstimatedSizeBytes: 0,
            IsMainFeature: isMainFeature
        );
}
