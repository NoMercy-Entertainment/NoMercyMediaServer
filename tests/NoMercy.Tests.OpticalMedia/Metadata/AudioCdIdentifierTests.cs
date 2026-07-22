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
using Moq;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Sources;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Tests.OpticalMedia.Infrastructure;

namespace NoMercy.Tests.OpticalMedia.Metadata;

[Trait(name: "Category", value: "Unit")]
public class AudioCdIdentifierTests
{
    private static DiscInfo MakeCdDisc(string label = "AUDIO_CD") =>
        new(
            Type: OpticalDiscType.Cd,
            DiscLabel: label,
            Titles: [],
            AudioTracks:
            [
                new(Index: 1, Title: null, Artist: null, Duration: TimeSpan.FromSeconds(seconds: 180), SampleRate: 44100, Channels: 2),
                new(Index: 2, Title: null, Artist: null, Duration: TimeSpan.FromSeconds(seconds: 210), SampleRate: 44100, Channels: 2),
            ],
            TotalDuration: TimeSpan.FromSeconds(seconds: 390)
        );

    private static AudioCdIdentifier MakeSut(
        ITocReader? tocReader = null,
        MusicBrainzDiscClient? discClient = null
    )
    {
        tocReader ??= new NullTocReader();
        discClient ??= new();
        return new(tocReader: tocReader, discClient: discClient, logger: NullLogger<AudioCdIdentifier>.Instance);
    }

    // ── CanHandle ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(data: [OpticalDiscType.Cd, true])]
    [InlineData(data: [OpticalDiscType.Dvd, false])]
    [InlineData(data: [OpticalDiscType.BluRay, false])]
    [InlineData(data: [OpticalDiscType.None, false])]
    public void CanHandle_ReturnsCorrectValueForDiscType(OpticalDiscType type, bool expectedResult)
    {
        AudioCdIdentifier sut = MakeSut();
        sut.CanHandle(type: type).Should().Be(expected: expectedResult);
    }

    // ── TOC unavailable → NeedsManualAssignment ────────────────────────────

    [Fact]
    public async Task IdentifyAsync_WhenTocIsNull_ReturnsNeedsManualAssignment()
    {
        Mock<ITocReader> tocMock = new();
        tocMock
            .Setup(expression: r => r.ReadTocAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (DiscToc?)null);

        AudioCdIdentifier sut = MakeSut(tocReader: tocMock.Object);
        DiscIdentification result = await sut.IdentifyAsync(disc: MakeCdDisc(), ct: CancellationToken.None);

        result.NeedsManualAssignment.Should().BeTrue();
        result.AutoApply.Should().BeFalse();
        result.Candidates.Should().BeEmpty();
        result.Kind.Should().Be(expected: MediaKind.Music);
    }

    [Fact]
    public async Task IdentifyAsync_WithNullTocReaderDefault_ReturnsNeedsManualAssignment()
    {
        AudioCdIdentifier sut = MakeSut(tocReader: new NullTocReader());
        DiscIdentification result = await sut.IdentifyAsync(disc: MakeCdDisc(), ct: CancellationToken.None);

        result.NeedsManualAssignment.Should().BeTrue();
    }

    // ── VideoDiscIdentifier CanHandle ──────────────────────────────────────

    [Theory]
    [InlineData(data: [OpticalDiscType.Dvd, true])]
    [InlineData(data: [OpticalDiscType.BluRay, true])]
    [InlineData(data: [OpticalDiscType.Cd, false])]
    [InlineData(data: [OpticalDiscType.None, false])]
    public void VideoDiscIdentifier_CanHandle_CorrectDiscTypes(
        OpticalDiscType type,
        bool expectedResult
    )
    {
        VideoDiscIdentifier sut = new(logger: NullLogger<VideoDiscIdentifier>.Instance);
        sut.CanHandle(type: type).Should().Be(expected: expectedResult);
    }

    // ── BuildTocString ──────────────────────────────────────────────────────

    [Fact]
    public void BuildTocString_FormatsFirstLastLeadOutAndTrackOffsetsWith150Pregap()
    {
        DiscToc toc = new(
            FirstTrack: 1,
            LastTrack: 2,
            LeadOutOffsetSectors: 30000,
            TrackOffsetsSectors: [150, 15150]
        );

        string result = AudioCdIdentifier.BuildTocString(toc: toc);

        result.Should().Be(expected: "1+2+30150+300+15300");
    }
}

/// <summary>
/// End-to-end tests for <see cref="AudioCdIdentifier.IdentifyAsync"/> against
/// the MusicBrainz + Cover Art Archive HTTP contract, using
/// <see cref="ProviderHttpHarness"/> to script real request/response bodies
/// (no mock of the identifier itself) so the exact/fuzzy fallback, retry, and
/// confidence-ranking logic all run for real.
/// </summary>
[Trait(name: "Category", value: "Unit")]
[Collection(name: "HttpClientProvider")]
public sealed class AudioCdIdentifierHttpTests : ProviderHttpHarness
{
    public AudioCdIdentifierHttpTests()
        : base(httpClientNames: [NoMercy.Providers.Helpers.HttpClientNames.MusicBrainz, NoMercy.Providers.Helpers.HttpClientNames.CoverArt]
        ) { }

    private static DiscInfo MakeCdDisc() =>
        new(
            Type: OpticalDiscType.Cd,
            DiscLabel: "AUDIO_CD",
            Titles: [],
            AudioTracks: [new(Index: 1, Title: null, Artist: null, Duration: TimeSpan.FromSeconds(seconds: 180), SampleRate: 44100, Channels: 2)],
            TotalDuration: TimeSpan.FromSeconds(seconds: 180)
        );

    private static DiscToc MakeToc(int leadOut) =>
        new(FirstTrack: 1, LastTrack: 1, LeadOutOffsetSectors: leadOut, TrackOffsetsSectors: [150]);

    private static Mock<ITocReader> MakeTocReader(DiscToc toc)
    {
        Mock<ITocReader> reader = new();
        reader
            .Setup(expression: r => r.ReadTocAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: toc);
        return reader;
    }

    private static string ReleaseJson(
        Guid releaseId,
        string title,
        string artist,
        bool hasFrontCover,
        int trackCount = 1
    )
    {
        string tracksJson = string.Join(
            separator: ",",
            values: Enumerable
                .Range(start: 1, count: trackCount)
                .Select(selector: i =>
                    $$"""
                    {
                      "position": {{i}},
                      "id": "{{Guid.NewGuid()}}",
                      "length": {{180000 + i}},
                      "title": "Track {{i}}",
                      "artist-credit": [ { "name": "{{artist}}", "joinphrase": "" } ],
                      "recording": { "id": "{{Guid.NewGuid()}}", "title": "Track {{i}}" }
                    }
                    """
                )
        );

        return $$"""
            {
              "releases": [
                {
                  "id": "{{releaseId}}",
                  "title": "{{title}}",
                  "artist-credit": [ { "name": "{{artist}}", "joinphrase": "" } ],
                  "date": "2020-05-01",
                  "media": [ { "track-count": {{trackCount}}, "tracks": [ {{tracksJson}} ] } ],
                  "cover-art-archive": { "front": {{(hasFrontCover ? "true" : "false")}} }
                }
              ]
            }
            """;
    }

    private static string EmptyReleasesJson() => """{"releases":[]}""";

    private static string CoverArtJson(string imageUrl) =>
        $$"""
            {
              "images": [ { "front": true, "types": ["Front"], "image": "{{imageUrl}}" } ]
            }
            """;

    [Fact]
    public async Task IdentifyAsync_ExactMatch_ReturnsHighConfidenceCandidate()
    {
        DiscToc toc = MakeToc(leadOut: 20000);
        string discId = MusicBrainzDiscId.Compute(toc: toc);
        Guid releaseId = Guid.NewGuid();

        Handler.WhenGet(
            pathContains: $"discid/{discId}",
            responses: MockResponse.Json(
                status: HttpStatusCode.OK,
                body: ReleaseJson(releaseId: releaseId, title: "Test Album", artist: "Test Artist", hasFrontCover: true)
            )
        );

        AudioCdIdentifier sut = new(
            tocReader: MakeTocReader(toc: toc).Object,
            discClient: new MusicBrainzDiscClient(),
            logger: NullLogger<AudioCdIdentifier>.Instance
        );

        DiscIdentification result = await sut.IdentifyAsync(disc: MakeCdDisc(), ct: CancellationToken.None);

        result.Kind.Should().Be(expected: MediaKind.Music);
        result.NeedsManualAssignment.Should().BeFalse();
        result.Candidates.Should().HaveCount(expected: 1);
        result.Candidates[0].StableId.Should().Be(expected: releaseId.ToString());
        result.Candidates[0].Title.Should().Contain(expected: "Test Album");
        result.Candidates[0].Confidence.Should().BeApproximately(expectedValue: 0.97, precision: 0.001);
        result.AutoApply.Should().BeTrue(because: "single exact match above the auto-apply threshold");
    }

    [Fact]
    public async Task IdentifyAsync_ExactMatch_BuildsTrackMappingWhenTrackCountMatches()
    {
        DiscToc toc = MakeToc(leadOut: 20100);
        string discId = MusicBrainzDiscId.Compute(toc: toc);
        Guid releaseId = Guid.NewGuid();

        Handler.WhenGet(
            pathContains: $"discid/{discId}",
            responses: MockResponse.Json(status: HttpStatusCode.OK, body: ReleaseJson(releaseId: releaseId, title: "Album", artist: "Artist", hasFrontCover: false))
        );

        AudioCdIdentifier sut = new(
            tocReader: MakeTocReader(toc: toc).Object,
            discClient: new MusicBrainzDiscClient(),
            logger: NullLogger<AudioCdIdentifier>.Instance
        );

        DiscIdentification result = await sut.IdentifyAsync(disc: MakeCdDisc(), ct: CancellationToken.None);

        result.Candidates[0].TrackMapping.Should().HaveCount(expected: 1);
        result.Candidates[0].TrackMapping![0].TrackIndex.Should().Be(expected: 1);
    }

    [Fact]
    public async Task IdentifyAsync_ExactMatchEmpty_FallsBackToFuzzy_ReturnsFuzzyCandidate()
    {
        DiscToc toc = MakeToc(leadOut: 20200);
        string discId = MusicBrainzDiscId.Compute(toc: toc);
        Guid releaseId = Guid.NewGuid();

        Handler.WhenGet(
            pathContains: $"discid/{discId}",
            responses: MockResponse.Json(status: HttpStatusCode.OK, body: EmptyReleasesJson())
        );
        Handler.WhenGet(
            pathContains: "discid/-",
            responses: MockResponse.Json(
                status: HttpStatusCode.OK,
                body: ReleaseJson(releaseId: releaseId, title: "Fuzzy Album", artist: "Artist", hasFrontCover: false)
            )
        );

        AudioCdIdentifier sut = new(
            tocReader: MakeTocReader(toc: toc).Object,
            discClient: new MusicBrainzDiscClient(),
            logger: NullLogger<AudioCdIdentifier>.Instance
        );

        DiscIdentification result = await sut.IdentifyAsync(disc: MakeCdDisc(), ct: CancellationToken.None);

        result.NeedsManualAssignment.Should().BeFalse();
        result.Candidates.Should().HaveCount(expected: 1);
        result.Candidates[0].StableId.Should().Be(expected: releaseId.ToString());
        result.Candidates[0].Confidence.Should().BeApproximately(expectedValue: 0.70, precision: 0.001);
    }

    [Fact]
    public async Task IdentifyAsync_ExactLookupThrows_FallsBackToFuzzy()
    {
        DiscToc toc = MakeToc(leadOut: 20300);
        string discId = MusicBrainzDiscId.Compute(toc: toc);
        Guid releaseId = Guid.NewGuid();

        // 500 status: FromJson<T> is a "forgiving parse" (malformed JSON
        // degrades to null, never throws — see JsonHelper.FromJson), so a
        // real throw out of ExternalApiClient.Get<T> needs an HTTP failure
        // status that MusicBrainzBaseClient.ShouldSoftFail does NOT treat as
        // soft-fail (only 404 is) and that Queue does not auto-retry (only
        // 429/502/503/504 are) — this is what AudioCdIdentifier's own
        // try/catch around the exact lookup exists to absorb.
        Handler.WhenGet(
            pathContains: $"discid/{discId}",
            responses: MockResponse.Status(status: HttpStatusCode.InternalServerError)
        );
        Handler.WhenGet(
            pathContains: "discid/-",
            responses: MockResponse.Json(
                status: HttpStatusCode.OK,
                body: ReleaseJson(releaseId: releaseId, title: "Recovered Album", artist: "Artist", hasFrontCover: false)
            )
        );

        AudioCdIdentifier sut = new(
            tocReader: MakeTocReader(toc: toc).Object,
            discClient: new MusicBrainzDiscClient(),
            logger: NullLogger<AudioCdIdentifier>.Instance
        );

        DiscIdentification result = await sut.IdentifyAsync(disc: MakeCdDisc(), ct: CancellationToken.None);

        result.NeedsManualAssignment.Should().BeFalse();
        result.Candidates[0].StableId.Should().Be(expected: releaseId.ToString());
    }

    [Fact]
    public async Task IdentifyAsync_BothLookupsReturnNoReleases_ReturnsNeedsManualAssignment()
    {
        DiscToc toc = MakeToc(leadOut: 20400);
        string discId = MusicBrainzDiscId.Compute(toc: toc);

        Handler.WhenGet(
            pathContains: $"discid/{discId}",
            responses: MockResponse.Json(status: HttpStatusCode.OK, body: EmptyReleasesJson())
        );
        Handler.WhenGet(pathContains: "discid/-", responses: MockResponse.Json(status: HttpStatusCode.OK, body: EmptyReleasesJson()));

        AudioCdIdentifier sut = new(
            tocReader: MakeTocReader(toc: toc).Object,
            discClient: new MusicBrainzDiscClient(),
            logger: NullLogger<AudioCdIdentifier>.Instance
        );

        DiscIdentification result = await sut.IdentifyAsync(disc: MakeCdDisc(), ct: CancellationToken.None);

        result.NeedsManualAssignment.Should().BeTrue();
        result.Candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task IdentifyAsync_ExactMatchNotFound_FuzzyThrows_ReturnsNeedsManualAssignment()
    {
        DiscToc toc = MakeToc(leadOut: 20500);
        string discId = MusicBrainzDiscId.Compute(toc: toc);

        Handler.WhenGet(pathContains: $"discid/{discId}", responses: MockResponse.Status(status: HttpStatusCode.NotFound));
        Handler.WhenGet(pathContains: "discid/-", responses: MockResponse.Status(status: HttpStatusCode.InternalServerError));

        AudioCdIdentifier sut = new(
            tocReader: MakeTocReader(toc: toc).Object,
            discClient: new MusicBrainzDiscClient(),
            logger: NullLogger<AudioCdIdentifier>.Instance
        );

        DiscIdentification result = await sut.IdentifyAsync(disc: MakeCdDisc(), ct: CancellationToken.None);

        result.NeedsManualAssignment.Should().BeTrue();
    }

    [Fact]
    public async Task IdentifyAsync_MultipleReleases_AppliesPerRankConfidencePenaltyAndOrdersDescending()
    {
        DiscToc toc = MakeToc(leadOut: 20600);
        string discId = MusicBrainzDiscId.Compute(toc: toc);
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();

        string json = $$"""
            {
              "releases": [
                {
                  "id": "{{firstId}}",
                  "title": "First Pressing",
                  "artist-credit": [ { "name": "Artist", "joinphrase": "" } ],
                  "date": "2020-01-01",
                  "media": [],
                  "cover-art-archive": { "front": false }
                },
                {
                  "id": "{{secondId}}",
                  "title": "Second Pressing",
                  "artist-credit": [ { "name": "Artist", "joinphrase": "" } ],
                  "date": "2021-01-01",
                  "media": [],
                  "cover-art-archive": { "front": false }
                }
              ]
            }
            """;
        Handler.WhenGet(pathContains: $"discid/{discId}", responses: MockResponse.Json(status: HttpStatusCode.OK, body: json));

        AudioCdIdentifier sut = new(
            tocReader: MakeTocReader(toc: toc).Object,
            discClient: new MusicBrainzDiscClient(),
            logger: NullLogger<AudioCdIdentifier>.Instance
        );

        DiscIdentification result = await sut.IdentifyAsync(disc: MakeCdDisc(), ct: CancellationToken.None);

        result.Candidates.Should().HaveCount(expected: 2);
        result
            .Candidates[0]
            .StableId.Should()
            .Be(expected: firstId.ToString(), because: "rank 0 keeps the full exact-match confidence");
        result.Candidates[0].Confidence.Should().BeApproximately(expectedValue: 0.97, precision: 0.001);
        result.Candidates[1].StableId.Should().Be(expected: secondId.ToString());
        result
            .Candidates[1]
            .Confidence.Should()
            .BeApproximately(expectedValue: 0.92, precision: 0.001, because: "rank 1 is penalised by 0.05");
        result.Candidates[0].Confidence.Should().BeGreaterThan(expected: result.Candidates[1].Confidence);
        result
            .AutoApply.Should()
            .BeFalse(because: "multiple releases never auto-apply regardless of confidence");
    }

    [Fact]
    public async Task IdentifyAsync_TrackCountDoesNotMatchAnyMedium_NoTrackMappingBuilt()
    {
        DiscToc toc = MakeToc(leadOut: 20700);
        string discId = MusicBrainzDiscId.Compute(toc: toc);
        Guid releaseId = Guid.NewGuid();

        // track-count 5 never matches the 1-track disc used by MakeCdDisc.
        string json = $$"""
            {
              "releases": [
                {
                  "id": "{{releaseId}}",
                  "title": "Album",
                  "artist-credit": [],
                  "date": "2020-01-01",
                  "media": [ { "track-count": 5, "tracks": [] } ],
                  "cover-art-archive": { "front": false }
                }
              ]
            }
            """;
        Handler.WhenGet(pathContains: $"discid/{discId}", responses: MockResponse.Json(status: HttpStatusCode.OK, body: json));

        AudioCdIdentifier sut = new(
            tocReader: MakeTocReader(toc: toc).Object,
            discClient: new MusicBrainzDiscClient(),
            logger: NullLogger<AudioCdIdentifier>.Instance
        );

        DiscIdentification result = await sut.IdentifyAsync(disc: MakeCdDisc(), ct: CancellationToken.None);

        result.Candidates[0].TrackMapping.Should().BeEmpty();
    }

    [Fact]
    public async Task IdentifyAsync_CoverArtFound_PopulatesPosterUrl()
    {
        DiscToc toc = MakeToc(leadOut: 20900);
        string discId = MusicBrainzDiscId.Compute(toc: toc);
        Guid releaseId = Guid.NewGuid();
        const string imageUrl = "https://coverartarchive.org/release/x/front.jpg";

        Handler.WhenGet(
            pathContains: $"discid/{discId}",
            responses: MockResponse.Json(status: HttpStatusCode.OK, body: ReleaseJson(releaseId: releaseId, title: "Album", artist: "Artist", hasFrontCover: true))
        );
        Handler.WhenGet(
            pathContains: $"release/{releaseId}",
            responses: MockResponse.Json(status: HttpStatusCode.OK, body: CoverArtJson(imageUrl: imageUrl))
        );

        AudioCdIdentifier sut = new(
            tocReader: MakeTocReader(toc: toc).Object,
            discClient: new MusicBrainzDiscClient(),
            logger: NullLogger<AudioCdIdentifier>.Instance
        );

        DiscIdentification result = await sut.IdentifyAsync(disc: MakeCdDisc(), ct: CancellationToken.None);

        result.Candidates[0].PosterUrl.Should().Be(expected: imageUrl);
    }

    [Fact]
    public async Task IdentifyAsync_CoverArtLookupFails_PosterUrlIsNull()
    {
        DiscToc toc = MakeToc(leadOut: 21000);
        string discId = MusicBrainzDiscId.Compute(toc: toc);
        Guid releaseId = Guid.NewGuid();

        Handler.WhenGet(
            pathContains: $"discid/{discId}",
            responses: MockResponse.Json(status: HttpStatusCode.OK, body: ReleaseJson(releaseId: releaseId, title: "Album", artist: "Artist", hasFrontCover: false))
        );
        Handler.WhenGet(pathContains: $"release/{releaseId}", responses: MockResponse.Status(status: HttpStatusCode.NotFound));

        AudioCdIdentifier sut = new(
            tocReader: MakeTocReader(toc: toc).Object,
            discClient: new MusicBrainzDiscClient(),
            logger: NullLogger<AudioCdIdentifier>.Instance
        );

        DiscIdentification result = await sut.IdentifyAsync(disc: MakeCdDisc(), ct: CancellationToken.None);

        result.Candidates[0].PosterUrl.Should().BeNull();
    }

    [Fact]
    public async Task IdentifyAsync_CoverArtLookupThrows_PosterUrlIsNull_WithoutCrashingIdentify()
    {
        // 500 (not a CoverArtBaseClient soft-fail status, not Queue-retried)
        // makes coverClient.Cover()'s returned Task genuinely fault — since
        // CoverArtCoverArtClient.Cover()'s own try/catch wraps only the
        // synchronous call to Get<T>, not an awaited result, the fault
        // surfaces at FetchCoverUrlAsync's `await coverClient.Cover()` and
        // must be absorbed by its own catch.
        DiscToc toc = MakeToc(leadOut: 21100);
        string discId = MusicBrainzDiscId.Compute(toc: toc);
        Guid releaseId = Guid.NewGuid();

        Handler.WhenGet(
            pathContains: $"discid/{discId}",
            responses: MockResponse.Json(status: HttpStatusCode.OK, body: ReleaseJson(releaseId: releaseId, title: "Album", artist: "Artist", hasFrontCover: false))
        );
        Handler.WhenGet(
            pathContains: $"release/{releaseId}",
            responses: MockResponse.Status(status: HttpStatusCode.InternalServerError)
        );

        AudioCdIdentifier sut = new(
            tocReader: MakeTocReader(toc: toc).Object,
            discClient: new MusicBrainzDiscClient(),
            logger: NullLogger<AudioCdIdentifier>.Instance
        );

        DiscIdentification result = await sut.IdentifyAsync(disc: MakeCdDisc(), ct: CancellationToken.None);

        result.Candidates[0].PosterUrl.Should().BeNull();
    }

    [Fact]
    public async Task IdentifyAsync_NoArtistCredit_TitleHasNoArtistPrefix()
    {
        DiscToc toc = MakeToc(leadOut: 20800);
        string discId = MusicBrainzDiscId.Compute(toc: toc);
        Guid releaseId = Guid.NewGuid();

        string json = $$"""
            {
              "releases": [
                {
                  "id": "{{releaseId}}",
                  "title": "No Artist Album",
                  "artist-credit": [],
                  "date": "2020-01-01",
                  "media": [],
                  "cover-art-archive": { "front": false }
                }
              ]
            }
            """;
        Handler.WhenGet(pathContains: $"discid/{discId}", responses: MockResponse.Json(status: HttpStatusCode.OK, body: json));

        AudioCdIdentifier sut = new(
            tocReader: MakeTocReader(toc: toc).Object,
            discClient: new MusicBrainzDiscClient(),
            logger: NullLogger<AudioCdIdentifier>.Instance
        );

        DiscIdentification result = await sut.IdentifyAsync(disc: MakeCdDisc(), ct: CancellationToken.None);

        result.Candidates[0].Title.Should().Be(expected: "No Artist Album");
    }
}
