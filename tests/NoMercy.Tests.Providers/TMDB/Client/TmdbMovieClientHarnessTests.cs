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
using NoMercy.NmSystem.Configuration;
using NoMercy.Providers.Helpers;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Tests.Providers.Infrastructure;
using NoMercy.Tests.Providers.TMDB.Mocks;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// Requirement-driven coverage for <see cref="TmdbMovieClient"/> through the
/// mock HTTP harness — no real network call, no dependency on a live TMDB
/// token. This complements (does not replace) the existing <c>[Collection("TmdbApi")]</c>
/// suite, which exercises the client against the real TMDB API for behavioral
/// smoke coverage but never asserts on the outgoing request itself.
/// </summary>
[Collection(name: "HttpClientProvider")]
public sealed class TmdbMovieClientHarnessTests : ProviderHttpHarness
{
    public TmdbMovieClientHarnessTests()
        : base(httpClientNames: HttpClientNames.Tmdb) { }

    [Fact]
    public async Task Details_WithPriority_SendsAuthHeaderAndCallerLanguage()
    {
        // Requirement: Details(priority: true) must carry the Bearer token
        // from ApiKeyStore and the client's constructed language, joined with
        // ",null" (TmdbBaseClient's fallback-language convention).
        // Every test in this file uses its own movie id so the shared on-disk
        // CacheController cache (keyed by URL, incl. query) never answers one
        // test's request with another test's cached body.
        TestApiKeyStore.Instance.TmdbToken = "tmdb-test-token";
        const int movieId = 900_001;
        TmdbMovieDetails body = TmdbMovieMockData.GetSampleMovieDetails();
        Handler.WhenGet(pathContains: $"movie/{movieId}", responses: MockResponse.Json(status: HttpStatusCode.OK, body: body));

        using TmdbMovieClient client = new(id: movieId, language: "nl-NL");
        TmdbMovieDetails? result = await client.Details(priority: true);

        result.Should().NotBeNull();
        result!.Title.Should().Be(expected: "The Dark Knight");

        CapturedRequest request = Handler.Requests.Should().ContainSingle().Which;
        request.Path.Should().Be(expected: $"/3/movie/{movieId}");
        request.HeaderValue(name: "Authorization").Should().Be(expected: "Bearer tmdb-test-token");
        request.Query.Should().ContainKey(expected: "language").WhoseValue.Should().Be(expected: "nl-NL,null");
    }

    [Fact]
    public async Task Details_WithoutPriority_SendsEmptyLanguageQuery()
    {
        // Requirement: non-priority calls deliberately send language="" so
        // TMDB falls back to its own default rather than forcing a
        // background/bulk-fill request to wait on translation availability.
        TestApiKeyStore.Instance.TmdbToken = "tmdb-test-token";
        const int movieId = 900_002;
        TmdbMovieDetails body = TmdbMovieMockData.GenerateMovieWithId(id: movieId);
        Handler.WhenGet(pathContains: $"movie/{movieId}", responses: MockResponse.Json(status: HttpStatusCode.OK, body: body));

        using TmdbMovieClient client = new(id: movieId, language: "nl-NL");
        TmdbMovieDetails? result = await client.Details();

        result.Should().NotBeNull();

        CapturedRequest request = Handler.Requests.Should().ContainSingle().Which;
        request.Query.Should().ContainKey(expected: "language").WhoseValue.Should().Be(expected: string.Empty);
    }

    [Theory]
    [InlineData(data: [true, "true"])]
    [InlineData(data: [false, "false"])]
    public async Task Details_ReflectsAdultContentSetting_InIncludeAdultQuery(
        bool allowAdultContent,
        string expectedQueryValue
    )
    {
        TestApiKeyStore.Instance.TmdbToken = "tmdb-test-token";
        RuntimeServerSettings.Current.AllowAdultContent = allowAdultContent;
        try
        {
            int movieId = allowAdultContent ? 900_003 : 900_004;
            TmdbMovieDetails body = TmdbMovieMockData.GenerateMovieWithId(id: movieId);
            Handler.WhenGet(pathContains: $"movie/{movieId}", responses: MockResponse.Json(status: HttpStatusCode.OK, body: body));

            using TmdbMovieClient client = new(id: movieId);
            await client.Details();

            CapturedRequest request = Handler.Requests.Should().ContainSingle().Which;
            request
                .Query.Should()
                .ContainKey(expected: "include_adult")
                .WhoseValue.Should()
                .Be(expected: expectedQueryValue);
        }
        finally
        {
            RuntimeServerSettings.Current.AllowAdultContent = null;
        }
    }

    [Fact]
    public async Task WithAllAppends_NoMockProviderConfigured_BuildsFixedAppendToResponseList()
    {
        // Requirement: WithAllAppends() (real path, no mockAppendsProvider
        // delegate) must request exactly the fixed append set the movie
        // import pipeline expects, comma-joined via append_to_response.
        TestApiKeyStore.Instance.TmdbToken = "tmdb-test-token";
        const int movieId = 900_005;
        TmdbMovieAppends body = TmdbMovieMockData.GetSampleMovieAppends();
        Handler.WhenGet(pathContains: $"movie/{movieId}", responses: MockResponse.Json(status: HttpStatusCode.OK, body: body));

        using TmdbMovieClient client = new(id: movieId);
        TmdbMovieAppends? result = await client.WithAllAppends();

        result.Should().NotBeNull();
        result!.Credits.Should().NotBeNull();

        CapturedRequest request = Handler.Requests.Should().ContainSingle().Which;
        request
            .Query.Should()
            .ContainKey(expected: "append_to_response")
            .WhoseValue.Should()
            .Be(
                expected: "alternative_titles,release_dates,changes,credits,keywords,recommendations,"
                          + "similar,translations,external_ids,videos,images,watch/providers"
            );
    }

    [Fact]
    public async Task Details_UnknownMovieId_ReturnsNullWithoutThrowing()
    {
        TestApiKeyStore.Instance.TmdbToken = "tmdb-test-token";
        const int unknownId = 999_999_999;
        Handler.WhenGet(pathContains: $"movie/{unknownId}", responses: MockResponse.Status(status: HttpStatusCode.NotFound));

        using TmdbMovieClient client = new(id: unknownId);
        TmdbMovieDetails? result = await client.Details();

        result.Should().BeNull();
    }

    [Fact]
    public async Task Details_TransientServiceUnavailable_RetriesOnceThenReturnsData()
    {
        // Requirement: the shared Queue retries a transient 503 before the
        // caller ever sees a failure.
        TestApiKeyStore.Instance.TmdbToken = "tmdb-test-token";
        const int movieId = 900_006;
        TmdbMovieDetails body = TmdbMovieMockData.GenerateMovieWithId(id: movieId);
        Handler.WhenGet(
            pathContains: $"movie/{movieId}", responses: [MockResponse.Status(status: HttpStatusCode.ServiceUnavailable), MockResponse.Json(status: HttpStatusCode.OK, body: body)]
        );

        using TmdbMovieClient client = new(id: movieId);
        TmdbMovieDetails? result = await client.Details();

        result.Should().NotBeNull();
        Handler.RequestCountFor(pathContains: $"movie/{movieId}").Should().Be(expected: 2);
    }

    [Fact]
    public async Task Details_TransientFailureExhaustsRetryBudget_SoftFailsToNullRatherThanThrowing()
    {
        // Requirement: unlike MusicBrainzBaseClient (which only soft-fails
        // 404), TmdbBaseClient.Get<T> explicitly catches ServiceUnavailable
        // and TooManyRequests too — so once the shared Queue's retry budget is
        // exhausted, TMDB degrades to null instead of throwing.
        TestApiKeyStore.Instance.TmdbToken = "tmdb-test-token";
        const int movieId = 900_007;
        Handler.WhenGet(pathContains: $"movie/{movieId}", responses: MockResponse.Status(status: HttpStatusCode.ServiceUnavailable));

        using TmdbMovieClient client = new(id: movieId);
        TmdbMovieDetails? result = await client.Details();

        result.Should().BeNull();
        Handler.RequestCountFor(pathContains: $"movie/{movieId}").Should().Be(expected: 4); // 1 initial + 3 retries
    }
}
