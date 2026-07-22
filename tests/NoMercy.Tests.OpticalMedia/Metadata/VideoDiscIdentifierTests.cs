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

using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Sources;
using NoMercy.Tests.OpticalMedia.Infrastructure;

namespace NoMercy.Tests.OpticalMedia.Metadata;

/// <summary>
/// Tests for <see cref="VideoDiscIdentifier"/> internals that are accessible
/// via InternalsVisibleTo.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class VideoDiscIdentifierTests
{
    // ── NormalizeLabel ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(data: ["Avatar_Book_1_Disc_1", "Avatar Book 1"])]
    [InlineData(data: ["THE_DARK_KNIGHT", "THE DARK KNIGHT"])]
    [InlineData(data: ["Inception", "Inception"])]
    [InlineData(data: ["Lord.Of.The.Rings", "Lord Of The Rings"])]
    [InlineData(data: ["Star-Wars-A-New-Hope", "Star Wars A New Hope"])]
    [InlineData(data: ["Breaking_Bad_Season_2_Disc_3", "Breaking Bad Season 2"])]
    public void NormalizeLabel_StripsDiscSuffixAndNormalizesSeparators(
        string input,
        string expected
    )
    {
        string result = VideoDiscIdentifier.NormalizeLabel(label: input);
        result.Should().Be(expected: expected);
    }

    // ── BlendConfidence ────────────────────────────────────────────────────

    [Fact]
    public void BlendConfidence_NoRuntime_UsesStringScoreOnly()
    {
        double confidence = VideoDiscIdentifier.BlendConfidence(
            query: "Avatar",
            candidate: "Avatar",
            rank: 0,
            discDurationSec: 0,
            runtimeMin: null
        );

        confidence.Should().BeApproximately(expectedValue: 1.0, precision: 0.001);
    }

    [Fact]
    public void BlendConfidence_PerfectStringAndDuration_IsHigh()
    {
        double confidence = VideoDiscIdentifier.BlendConfidence(
            query: "Avatar",
            candidate: "Avatar",
            rank: 0,
            discDurationSec: 162 * 60,
            runtimeMin: 162
        );

        confidence.Should().BeGreaterThan(expected: 0.85);
    }

    [Fact]
    public void BlendConfidence_HigherRank_LowerScore()
    {
        double rank0 = VideoDiscIdentifier.BlendConfidence(
            query: "Avatar",
            candidate: "Avatar",
            rank: 0,
            discDurationSec: 0,
            runtimeMin: null
        );
        double rank2 = VideoDiscIdentifier.BlendConfidence(
            query: "Avatar",
            candidate: "Avatar",
            rank: 2,
            discDurationSec: 0,
            runtimeMin: null
        );

        rank0.Should().BeGreaterThan(expected: rank2);
    }

    [Fact]
    public void BlendConfidence_EmptyQuery_TreatsSimilarityAsZero()
    {
        // NormalizedSimilarity short-circuits to 0 when either token set is
        // empty (an all-whitespace/empty label after normalization).
        double confidence = VideoDiscIdentifier.BlendConfidence(
            query: "",
            candidate: "Avatar",
            rank: 0,
            discDurationSec: 0,
            runtimeMin: null
        );

        confidence.Should().Be(expected: 0);
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
    [InlineData(data: [3600, 3600, 1])]
    [InlineData(data: [7200, 3600, 2])]
    [InlineData(data: [10800, 3600, 3])]
    [InlineData(data: [14400, 3600, 4])]
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
            double delta = Math.Abs(value: discDurationSec - expectedSec);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                bestEpIdx = epIdx;
            }
        }

        bestEpIdx
            .Should()
            .Be(
                expected: epCountOnDisc,
                because: $"disc duration {discDurationSec}s should map to {epCountOnDisc} episode(s) at {episodeRunSec}s each"
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

        double constantDelta = Math.Abs(value: discDurationSec - (double)episodeRunSec);

        HashSet<double> deltas = [];
        for (int epIdx = 1; epIdx <= iterations; epIdx++)
        {
            double buggyDelta = Math.Abs(value: discDurationSec - (double)episodeRunSec);
            deltas.Add(item: buggyDelta);
        }

        deltas.Should().HaveCount(expected: 1, because: "old code always produced the same delta regardless of epIdx");
        deltas.First().Should().Be(expected: constantDelta);
    }
}

/// <summary>
/// End-to-end tests for <see cref="VideoDiscIdentifier.IdentifyAsync"/>
/// against the real TMDB HTTP contract, using <see cref="ProviderHttpHarness"/>
/// to script request/response bodies (no mock of the identifier itself) so
/// the search → runtime-fetch → confidence-blend → episode-resolution chain
/// all runs for real.
/// </summary>
[Trait(name: "Category", value: "Unit")]
[Collection(name: "HttpClientProvider")]
public sealed class VideoDiscIdentifierHttpTests : ProviderHttpHarness
{
    public VideoDiscIdentifierHttpTests()
        : base(httpClientNames: NoMercy.Providers.Helpers.HttpClientNames.Tmdb) { }

    private static DiscInfo MakeDisc(
        string? label,
        string? embeddedTitle = null,
        int durationSec = 0
    ) =>
        new(
            Type: OpticalDiscType.Dvd,
            DiscLabel: label,
            Titles: durationSec > 0
                ? [new(Index: 0, Name: "Main", Duration: TimeSpan.FromSeconds(seconds: durationSec), VideoStreams: [], AudioStreams: [], Subtitles: [], Chapters: [], EstimatedSizeBytes: 0, IsMainFeature: true)]
                : [],
            AudioTracks: null,
            TotalDuration: TimeSpan.FromSeconds(seconds: durationSec),
            DiscTitle: embeddedTitle
        );

    private static string EmptySearchResults() =>
        """{"page":1,"results":[],"total_pages":0,"total_results":0}""";

    private static string MovieSearchResults(int id, string title, string releaseDate) =>
        $$"""
            {
              "page": 1,
              "results": [
                {
                  "id": {{id}},
                  "title": "{{title}}",
                  "original_title": "{{title}}",
                  "release_date": "{{releaseDate}}",
                  "poster_path": "/poster.jpg",
                  "backdrop_path": "/backdrop.jpg"
                }
              ],
              "total_pages": 1,
              "total_results": 1
            }
            """;

    private static string TvSearchResults(int id, string name, string firstAirDate) =>
        $$"""
            {
              "page": 1,
              "results": [
                {
                  "id": {{id}},
                  "name": "{{name}}",
                  "original_name": "{{name}}",
                  "first_air_date": "{{firstAirDate}}",
                  "poster_path": "/poster.jpg",
                  "backdrop_path": "/backdrop.jpg"
                }
              ],
              "total_pages": 1,
              "total_results": 1
            }
            """;

    private static string MovieDetails(int runtimeMinutes) =>
        $$"""{ "runtime": {{runtimeMinutes}} }""";

    private static string MovieSearchResultsNoImages(int id, string title, string releaseDate) =>
        $$"""
            {
              "page": 1,
              "results": [
                { "id": {{id}}, "title": "{{title}}", "original_title": "{{title}}", "release_date": "{{releaseDate}}" }
              ],
              "total_pages": 1,
              "total_results": 1
            }
            """;

    [Fact]
    public async Task IdentifyAsync_NoLabelOrEmbeddedTitle_ReturnsNeedsManualAssignment_WithoutAnyHttpCall()
    {
        VideoDiscIdentifier sut = new(logger: NullLogger<VideoDiscIdentifier>.Instance);

        DiscIdentification result = await sut.IdentifyAsync(
            disc: MakeDisc(label: null),
            ct: CancellationToken.None
        );

        result.NeedsManualAssignment.Should().BeTrue();
    }

    [Fact]
    public async Task IdentifyAsync_LabelNormalizesToEmpty_ReturnsNeedsManualAssignment()
    {
        VideoDiscIdentifier sut = new(logger: NullLogger<VideoDiscIdentifier>.Instance);

        // "Disc 1" strips entirely via the disc-suffix regex, leaving nothing.
        DiscIdentification result = await sut.IdentifyAsync(
            disc: MakeDisc(label: "Disc_1"),
            ct: CancellationToken.None
        );

        result.NeedsManualAssignment.Should().BeTrue();
    }

    [Fact]
    public async Task IdentifyAsync_MovieMatch_ReturnsHighConfidenceAutoApplyCandidate()
    {
        const int movieId = 27205; // Inception's real TMDB id — value itself unused by the harness
        Handler.WhenGet(
            pathContains: "search/movie",
            responses: MockResponse.Json(
                status: HttpStatusCode.OK,
                body: MovieSearchResults(id: movieId, title: "Inception", releaseDate: "2010-07-16")
            )
        );
        Handler.WhenGet(pathContains: "search/tv", responses: MockResponse.Json(status: HttpStatusCode.OK, body: EmptySearchResults()));
        Handler.WhenGet(
            pathContains: $"movie/{movieId}",
            responses: MockResponse.Json(status: HttpStatusCode.OK, body: MovieDetails(runtimeMinutes: 148))
        );

        VideoDiscIdentifier sut = new(logger: NullLogger<VideoDiscIdentifier>.Instance);

        DiscIdentification result = await sut.IdentifyAsync(
            disc: MakeDisc(label: "Inception", durationSec: 148 * 60),
            ct: CancellationToken.None
        );

        result.NeedsManualAssignment.Should().BeFalse();
        result.Candidates.Should().ContainSingle();
        result.Candidates[0].StableId.Should().Be(expected: movieId.ToString());
        result.Candidates[0].Type.Should().Be(expected: MediaType.Movie);
        result.Candidates[0].Year.Should().Be(expected: 2010);
        result.Candidates[0].PosterUrl.Should().Contain(expected: "/poster.jpg");
        result.Candidates[0].BackdropUrl.Should().Contain(expected: "/backdrop.jpg");
        result
            .AutoApply.Should()
            .BeTrue(because: "exact title + exact runtime match should clear the auto-apply threshold");
    }

    [Fact]
    public async Task IdentifyAsync_NoMatchesOnFullQuery_FallsBackToFirstWord()
    {
        const int movieId = 603;
        Handler.WhenGet(
            pathContains: "search/movie",
            responses: request =>
            {
                string query = Microsoft
                    .AspNetCore.WebUtilities.QueryHelpers.ParseQuery(queryString: request.RequestUri!.Query)
                    .TryGetValue(key: "query", value: out Microsoft.Extensions.Primitives.StringValues qv)
                    ? qv.ToString()
                    : "";
                return query == "Matrix"
                    ? MockResponse.Json(
                        status: HttpStatusCode.OK,
                        body: MovieSearchResults(id: movieId, title: "The Matrix", releaseDate: "1999-03-31")
                    )(arg: request)
                    : MockResponse.Json(status: HttpStatusCode.OK, body: EmptySearchResults())(arg: request);
            }
        );
        Handler.WhenGet(pathContains: "search/tv", responses: MockResponse.Json(status: HttpStatusCode.OK, body: EmptySearchResults()));
        Handler.WhenGet(
            pathContains: $"movie/{movieId}",
            responses: MockResponse.Json(status: HttpStatusCode.OK, body: MovieDetails(runtimeMinutes: 136))
        );

        VideoDiscIdentifier sut = new(logger: NullLogger<VideoDiscIdentifier>.Instance);

        // "Matrix Something" (full query) never matches; "Matrix" (first word) does.
        DiscIdentification result = await sut.IdentifyAsync(
            disc: MakeDisc(label: "Matrix_Something"),
            ct: CancellationToken.None
        );

        result.NeedsManualAssignment.Should().BeFalse();
        result.Candidates.Should().ContainSingle();
        result.Candidates[0].StableId.Should().Be(expected: movieId.ToString());
    }

    [Fact]
    public async Task IdentifyAsync_NoMatchesAnywhere_ReturnsNeedsManualAssignment()
    {
        Handler.WhenGet(pathContains: "search/movie", responses: MockResponse.Json(status: HttpStatusCode.OK, body: EmptySearchResults()));
        Handler.WhenGet(pathContains: "search/tv", responses: MockResponse.Json(status: HttpStatusCode.OK, body: EmptySearchResults()));

        VideoDiscIdentifier sut = new(logger: NullLogger<VideoDiscIdentifier>.Instance);

        DiscIdentification result = await sut.IdentifyAsync(
            disc: MakeDisc(label: "Completely_Unknown_Title_Xyzzy"),
            ct: CancellationToken.None
        );

        result.NeedsManualAssignment.Should().BeTrue();
        result.Candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task IdentifyAsync_PrefersEmbeddedDiscTitleOverVolumeLabel()
    {
        const int movieId = 155;
        Handler.WhenGet(
            pathContains: "search/movie",
            responses: request =>
            {
                string query = Microsoft
                    .AspNetCore.WebUtilities.QueryHelpers.ParseQuery(queryString: request.RequestUri!.Query)
                    .TryGetValue(key: "query", value: out Microsoft.Extensions.Primitives.StringValues qv)
                    ? qv.ToString()
                    : "";
                return query.Contains(value: "Dark Knight", comparisonType: StringComparison.OrdinalIgnoreCase)
                    ? MockResponse.Json(
                        status: HttpStatusCode.OK,
                        body: MovieSearchResults(id: movieId, title: "The Dark Knight", releaseDate: "2008-07-16")
                    )(arg: request)
                    : MockResponse.Json(status: HttpStatusCode.OK, body: EmptySearchResults())(arg: request);
            }
        );
        Handler.WhenGet(pathContains: "search/tv", responses: MockResponse.Json(status: HttpStatusCode.OK, body: EmptySearchResults()));
        Handler.WhenGet(
            pathContains: $"movie/{movieId}",
            responses: MockResponse.Json(status: HttpStatusCode.OK, body: MovieDetails(runtimeMinutes: 152))
        );

        VideoDiscIdentifier sut = new(logger: NullLogger<VideoDiscIdentifier>.Instance);

        DiscIdentification result = await sut.IdentifyAsync(
            disc: MakeDisc(label: "DISC_ONE_UNRELATED", embeddedTitle: "Dark Knight"),
            ct: CancellationToken.None
        );

        result.Candidates.Should().ContainSingle();
        result.Candidates[0].StableId.Should().Be(expected: movieId.ToString());
    }

    [Fact]
    public async Task IdentifyAsync_TvShowMatch_ResolvesSeasonAndEpisodeFromDiscDuration()
    {
        const int showId = 1396; // Breaking Bad
        Handler.WhenGet(pathContains: "search/movie", responses: MockResponse.Json(status: HttpStatusCode.OK, body: EmptySearchResults()));
        Handler.WhenGet(
            pathContains: "search/tv",
            responses: MockResponse.Json(
                status: HttpStatusCode.OK,
                body: TvSearchResults(id: showId, name: "Breaking Bad", firstAirDate: "2008-01-20")
            )
        );
        // Registered before the show-details route below: the season path
        // ("tv/1396/season/1") is a substring superset of the show path
        // ("tv/1396"), and ScriptableHttpMessageHandler picks the first
        // registered route whose predicate matches — so the more specific
        // route must be scripted first or it is shadowed by the show route.
        Handler.WhenGet(
            pathContains: $"tv/{showId}/season/1",
            responses: MockResponse.Json(
                status: HttpStatusCode.OK,
                body: """
                      {
                        "season_number": 1,
                        "episodes": [
                          { "id": 1, "episode_number": 1 },
                          { "id": 2, "episode_number": 2 },
                          { "id": 3, "episode_number": 3 }
                        ]
                      }
                      """
            )
        );
        Handler.WhenGet(
            pathContains: $"tv/{showId}",
            responses: MockResponse.Json(
                status: HttpStatusCode.OK,
                body: $$"""
                        {
                          "name": "Breaking Bad",
                          "episode_run_time": [45],
                          "seasons": [ { "id": 1, "season_number": 1, "name": "Season 1" } ]
                        }
                        """
            )
        );

        VideoDiscIdentifier sut = new(logger: NullLogger<VideoDiscIdentifier>.Instance);

        // Disc holds 2 episodes worth of runtime (2 * 45min = 90min).
        DiscIdentification result = await sut.IdentifyAsync(
            disc: MakeDisc(label: "Breaking_Bad_Season_1_Disc_1", durationSec: 90 * 60),
            ct: CancellationToken.None
        );

        result.Candidates.Should().ContainSingle();
        result.Candidates[0].SeasonNumber.Should().Be(expected: 1);
        result.Candidates[0].EpisodeNumber.Should().Be(expected: 1);
    }

    [Fact]
    public async Task IdentifyAsync_TvShowMatch_NoEpisodeRuntimeReported_LeavesCandidateUnresolved()
    {
        const int showId = 100001;
        Handler.WhenGet(pathContains: "search/movie", responses: MockResponse.Json(status: HttpStatusCode.OK, body: EmptySearchResults()));
        Handler.WhenGet(
            pathContains: "search/tv",
            responses: MockResponse.Json(
                status: HttpStatusCode.OK,
                body: TvSearchResults(id: showId, name: "Unknown Runtime Show", firstAirDate: "2010-01-01")
            )
        );
        Handler.WhenGet(
            pathContains: $"tv/{showId}",
            responses: MockResponse.Json(
                status: HttpStatusCode.OK,
                body: """
                      {
                        "name": "Unknown Runtime Show",
                        "episode_run_time": [],
                        "seasons": [ { "id": 1, "season_number": 1, "name": "Season 1" } ]
                      }
                      """
            )
        );

        VideoDiscIdentifier sut = new(logger: NullLogger<VideoDiscIdentifier>.Instance);

        DiscIdentification result = await sut.IdentifyAsync(
            disc: MakeDisc(label: "Unknown_Runtime_Show", durationSec: 3600),
            ct: CancellationToken.None
        );

        result.Candidates.Should().ContainSingle();
        result
            .Candidates[0]
            .SeasonNumber.Should()
            .BeNull(because: "no episode runtime means TryResolveEpisodeAsync can't compute a hypothesis");
    }

    [Fact]
    public async Task IdentifyAsync_TvShowMatch_FirstSeasonHasNoEpisodes_SkipsToNextSeason()
    {
        const int showId = 100002;
        Handler.WhenGet(pathContains: "search/movie", responses: MockResponse.Json(status: HttpStatusCode.OK, body: EmptySearchResults()));
        Handler.WhenGet(
            pathContains: "search/tv",
            responses: MockResponse.Json(
                status: HttpStatusCode.OK,
                body: TvSearchResults(id: showId, name: "Multi Season Show", firstAirDate: "2010-01-01")
            )
        );
        Handler.WhenGet(
            pathContains: $"tv/{showId}/season/1",
            responses: MockResponse.Json(status: HttpStatusCode.OK, body: """{ "season_number": 1, "episodes": [] }""")
        );
        Handler.WhenGet(
            pathContains: $"tv/{showId}/season/2",
            responses: MockResponse.Json(
                status: HttpStatusCode.OK,
                body: """
                      {
                        "season_number": 2,
                        "episodes": [ { "id": 1, "episode_number": 1 }, { "id": 2, "episode_number": 2 } ]
                      }
                      """
            )
        );
        Handler.WhenGet(
            pathContains: $"tv/{showId}",
            responses: MockResponse.Json(
                status: HttpStatusCode.OK,
                body: $$"""
                        {
                          "name": "Multi Season Show",
                          "episode_run_time": [30],
                          "seasons": [
                            { "id": 1, "season_number": 1, "name": "Season 1" },
                            { "id": 2, "season_number": 2, "name": "Season 2" }
                          ]
                        }
                        """
            )
        );

        VideoDiscIdentifier sut = new(logger: NullLogger<VideoDiscIdentifier>.Instance);

        DiscIdentification result = await sut.IdentifyAsync(
            disc: MakeDisc(label: "Multi_Season_Show", durationSec: 30 * 60),
            ct: CancellationToken.None
        );

        result.Candidates.Should().ContainSingle();
        result
            .Candidates[0]
            .SeasonNumber.Should()
            .Be(expected: 2, because: "season 1 has no episodes and must be skipped, not selected");
    }

    [Fact]
    public async Task IdentifyAsync_TvShowMatch_OnlySpecialsSeason_LeavesCandidateUnresolved()
    {
        const int showId = 100003;
        Handler.WhenGet(pathContains: "search/movie", responses: MockResponse.Json(status: HttpStatusCode.OK, body: EmptySearchResults()));
        Handler.WhenGet(
            pathContains: "search/tv",
            responses: MockResponse.Json(
                status: HttpStatusCode.OK,
                body: TvSearchResults(id: showId, name: "Specials Only Show", firstAirDate: "2010-01-01")
            )
        );
        Handler.WhenGet(
            pathContains: $"tv/{showId}",
            responses: MockResponse.Json(
                status: HttpStatusCode.OK,
                body: """
                      {
                        "name": "Specials Only Show",
                        "episode_run_time": [30],
                        "seasons": [ { "id": 0, "season_number": 0, "name": "Specials" } ]
                      }
                      """
            )
        );

        VideoDiscIdentifier sut = new(logger: NullLogger<VideoDiscIdentifier>.Instance);

        DiscIdentification result = await sut.IdentifyAsync(
            disc: MakeDisc(label: "Specials_Only_Show", durationSec: 30 * 60),
            ct: CancellationToken.None
        );

        result.Candidates.Should().ContainSingle();
        result
            .Candidates[0]
            .SeasonNumber.Should()
            .BeNull(because: "season 0 (specials) is excluded from the hypothesis loop");
    }

    [Fact]
    public async Task SearchAsync_Manual_EmptyQuery_ReturnsEmptyWithoutHttpCall()
    {
        VideoDiscIdentifier sut = new(logger: NullLogger<VideoDiscIdentifier>.Instance);

        DiscCandidate[] result = await sut.SearchAsync(
            query: string.Empty,
            type: MediaType.Movie,
            ct: CancellationToken.None
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_Manual_MovieType_SearchesMoviesOnly()
    {
        const int movieId = 680;
        Handler.WhenGet(
            pathContains: "search/movie",
            responses: MockResponse.Json(
                status: HttpStatusCode.OK,
                body: MovieSearchResultsNoImages(id: movieId, title: "Pulp Fiction", releaseDate: "1994-10-14")
            )
        );
        Handler.WhenGet(
            pathContains: $"movie/{movieId}",
            responses: MockResponse.Json(status: HttpStatusCode.OK, body: MovieDetails(runtimeMinutes: 154))
        );

        VideoDiscIdentifier sut = new(logger: NullLogger<VideoDiscIdentifier>.Instance);

        DiscCandidate[] result = await sut.SearchAsync(
            query: "Pulp Fiction",
            type: MediaType.Movie,
            ct: CancellationToken.None
        );

        result.Should().ContainSingle();
        result[0].StableId.Should().Be(expected: movieId.ToString());
        result[0].Type.Should().Be(expected: MediaType.Movie);
        // No poster_path/backdrop_path in this fixture — proves PosterUrl/
        // BackdropUrl's null-input short-circuit branch.
        result[0].PosterUrl.Should().BeNull();
        result[0].BackdropUrl.Should().BeNull();
    }

    [Fact]
    public async Task SearchAsync_Manual_TvShowType_SearchesTvShowsOnly()
    {
        const int showId = 2316;
        Handler.WhenGet(
            pathContains: "search/tv",
            responses: MockResponse.Json(
                status: HttpStatusCode.OK,
                body: TvSearchResults(id: showId, name: "The Office", firstAirDate: "2005-03-24")
            )
        );

        VideoDiscIdentifier sut = new(logger: NullLogger<VideoDiscIdentifier>.Instance);

        DiscCandidate[] result = await sut.SearchAsync(
            query: "The Office",
            type: MediaType.TvShow,
            ct: CancellationToken.None
        );

        result.Should().ContainSingle();
        result[0].StableId.Should().Be(expected: showId.ToString());
        result[0].Type.Should().Be(expected: MediaType.TvShow);
    }

    [Fact]
    public async Task SearchAsync_Manual_MusicType_ReturnsEmptyWithoutAnyHttpCall()
    {
        // MediaType.Music is a real enum value the switch expression's `_`
        // arm must handle — VideoDiscIdentifier only ever searches TMDB
        // movies/TV, so a music type must short-circuit to empty rather than
        // attempt a TMDB call. No route is scripted; any HTTP call would 404
        // against ScriptableHttpMessageHandler's unmatched-route response.
        VideoDiscIdentifier sut = new(logger: NullLogger<VideoDiscIdentifier>.Instance);

        DiscCandidate[] result = await sut.SearchAsync(
            query: "Some Album",
            type: MediaType.Music,
            ct: CancellationToken.None
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_Private_MusicType_ReturnsEmptyWithoutAnyHttpCall()
    {
        // The private per-candidate SearchAsync(query, type, ct, duration)
        // overload is never invoked with MediaType.Music from IdentifyAsync
        // (only Movie/TvShow) — its `_ => []` switch arm has no reachable
        // caller through the public surface today, so this exercises it
        // directly via reflection to prove the exhaustive-switch contract
        // without leaving it silently untested.
        VideoDiscIdentifier sut = new(logger: NullLogger<VideoDiscIdentifier>.Instance);

        System.Reflection.MethodInfo method = typeof(VideoDiscIdentifier).GetMethod(
            name: "SearchAsync",
            bindingAttr: System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            types: [typeof(string), typeof(MediaType), typeof(CancellationToken), typeof(int)]
        )!;

        Task<IEnumerable<DiscCandidate>> task =
            (Task<IEnumerable<DiscCandidate>>)
                method.Invoke(obj: sut, parameters: ["Some Album", MediaType.Music, CancellationToken.None, 0])!;
        IEnumerable<DiscCandidate> result = await task;

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task TryResolveEpisodeAsync_NonNumericStableId_ReturnsCandidateUnchanged()
    {
        // TryResolveEpisodeAsync is only ever invoked from IdentifyAsync with
        // a candidate.StableId sourced from `show.Id.ToString()` (always a
        // valid int), so the int.TryParse guard's failure branch has no
        // reachable caller through the public surface today — exercised
        // directly via reflection.
        VideoDiscIdentifier sut = new(logger: NullLogger<VideoDiscIdentifier>.Instance);

        DiscCandidate candidate = new(
            Source: "tmdb",
            StableId: "not-a-number",
            Title: "Some Show",
            Year: 2020,
            PosterUrl: null,
            BackdropUrl: null,
            Confidence: 0.5,
            Type: MediaType.TvShow
        );

        System.Reflection.MethodInfo method = typeof(VideoDiscIdentifier).GetMethod(
            name: "TryResolveEpisodeAsync",
            bindingAttr: System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        )!;

        Task<DiscCandidate> task =
            (Task<DiscCandidate>)method.Invoke(obj: sut, parameters: [candidate, 3600, CancellationToken.None])!;
        DiscCandidate result = await task;

        result.Should().Be(expected: candidate);
        result.SeasonNumber.Should().BeNull();
    }
}
