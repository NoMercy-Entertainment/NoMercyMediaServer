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
using NoMercy.Providers.Helpers;
using NoMercy.Providers.TVDB.Client;
using NoMercy.Providers.TVDB.Models.Auth;
using NoMercy.Providers.TVDB.Models.Series;
using NoMercy.Providers.TVDB.Models.Shared;
using NoMercy.Tests.Providers.Infrastructure;

namespace NoMercy.Tests.Providers.TVDB.Client;

/// <summary>
/// Requirement-driven request/response coverage for <see cref="TvdbSeriesClient"/>
/// — the concrete client used by the show-import pipeline. TVDB's static
/// login token (see <see cref="TvdbTokenAccess"/>) is pre-seeded per test so
/// these tests exercise URL/query building and response mapping without also
/// re-testing the login flow (covered by <see cref="TvdbBaseClientHarnessTests"/>).
/// </summary>
[Collection(name: "HttpClientProvider")]
public sealed class TvdbSeriesClientTests : ProviderHttpHarness
{
    public TvdbSeriesClientTests()
        : base(httpClientNames: [HttpClientNames.Tvdb, HttpClientNames.TvdbLogin])
    {
        TvdbTokenAccess.Set(
            token: new()
            {
                Status = "success",
                Data = new() { Token = "test-token", ExpiresAt = DateTime.UtcNow.AddMonths(months: 1) },
            }
        );
    }

    public override void Dispose()
    {
        TvdbTokenAccess.Reset();
        base.Dispose();
    }

    [Fact]
    public async Task Details_RequestsSeriesByIdAndMapsName()
    {
        const int seriesId = 121361; // Game of Thrones
        TvdbSeriesResponse body = new()
        {
            Status = "success",
            Data = new() { Id = seriesId, Name = "Game of Thrones" },
        };
        Handler.WhenGet(pathContains: $"series/{seriesId}", responses: MockResponse.Json(status: HttpStatusCode.OK, body: body));

        using TvdbSeriesClient client = new(id: seriesId);
        TvdbSeriesResponse? result = await client.Details();

        result.Should().NotBeNull();
        result!.Data.Name.Should().Be(expected: "Game of Thrones");

        CapturedRequest request = Handler
            .Requests.Should()
            .ContainSingle(predicate: r =>
                r.Path.Contains($"series/{seriesId}") && !r.Path.Contains("extended")
            )
            .Which;
        request.Path.Should().Be(expected: $"/v4/series/{seriesId}");
    }

    [Fact]
    public async Task Extended_WithMetaAndShort_BuildsMetaAndShortQueryParameters()
    {
        const int seriesId = 121361;
        TvdbSeriesExtendedResponse body = new()
        {
            Status = "success",
            Data = new() { Id = seriesId, Name = "Game of Thrones" },
        };
        Handler.WhenGet(pathContains: $"series/{seriesId}/extended", responses: MockResponse.Json(status: HttpStatusCode.OK, body: body));

        using TvdbSeriesClient client = new(id: seriesId);
        TvdbSeriesExtendedResponse? result = await client.Extended(meta: "translations,episodes", shortMeta: true);

        result.Should().NotBeNull();

        CapturedRequest request = Handler.Requests.Should().ContainSingle().Which;
        request.Path.Should().Be(expected: $"/v4/series/{seriesId}/extended");
        request.Query.Should().ContainKey(expected: "meta").WhoseValue.Should().Be(expected: "translations,episodes");
        request.Query.Should().ContainKey(expected: "short").WhoseValue.Should().Be(expected: "true");
    }

    [Fact]
    public async Task Extended_WithoutMetaOrShort_OmitsBothQueryParameters()
    {
        const int seriesId = 121361;
        TvdbSeriesExtendedResponse body = new()
        {
            Status = "success",
            Data = new() { Id = seriesId },
        };
        Handler.WhenGet(pathContains: $"series/{seriesId}/extended", responses: MockResponse.Json(status: HttpStatusCode.OK, body: body));

        using TvdbSeriesClient client = new(id: seriesId);
        await client.Extended();

        CapturedRequest request = Handler.Requests.Should().ContainSingle().Which;
        request.Query.Should().NotContainKey(unexpected: "meta");
        request.Query.Should().NotContainKey(unexpected: "short");
    }

    [Fact]
    public async Task Episodes_BuildsSeasonTypeInPathAndPageInQuery()
    {
        const int seriesId = 121361;
        TvdbSeriesEpisodesResponse body = new() { Status = "success" };
        Handler.WhenGet(
            pathContains: $"series/{seriesId}/episodes/official",
            responses: MockResponse.Json(status: HttpStatusCode.OK, body: body)
        );

        using TvdbSeriesClient client = new(id: seriesId);
        TvdbSeriesEpisodesResponse? result = await client.Episodes(seasonType: "official", page: 2);

        result.Should().NotBeNull();

        CapturedRequest request = Handler.Requests.Should().ContainSingle().Which;
        request.Path.Should().Be(expected: $"/v4/series/{seriesId}/episodes/official");
        request.Query.Should().ContainKey(expected: "page").WhoseValue.Should().Be(expected: "2");
    }

    [Fact]
    public async Task Filter_MapsEveryFilterFieldToItsOwnQueryParameter()
    {
        TvdbSeriesFilter filter = new()
        {
            Country = "usa",
            Language = "eng",
            CompanyId = 42,
            ContentRatingId = 7,
            GenreIds = "18,10765",
            SortBy = 1,
            SortType = "asc",
            Status = 1,
            Year = 2011,
            Page = 3,
        };
        TvdbPaginatedResponse<TvdbSeries> body = new() { Status = "success" };
        Handler.WhenGet(pathContains: "series/filter", responses: MockResponse.Json(status: HttpStatusCode.OK, body: body));

        using TvdbSeriesClient client = new();
        await client.Filter(filter: filter);

        CapturedRequest request = Handler.Requests.Should().ContainSingle().Which;
        request.Query[key: "country"].Should().Be(expected: "usa");
        request.Query[key: "lang"].Should().Be(expected: "eng");
        request.Query[key: "company"].Should().Be(expected: "42");
        request.Query[key: "contentRating"].Should().Be(expected: "7");
        request.Query[key: "genre"].Should().Be(expected: "18,10765");
        request.Query[key: "sort"].Should().Be(expected: "1");
        request.Query[key: "sortType"].Should().Be(expected: "asc");
        request.Query[key: "status"].Should().Be(expected: "1");
        request.Query[key: "year"].Should().Be(expected: "2011");
        request.Query[key: "page"].Should().Be(expected: "3");
    }

    [Fact]
    public async Task Details_UnknownSeriesId_ReturnsNull()
    {
        const int unknownId = 999_999_999;
        Handler.WhenGet(pathContains: $"series/{unknownId}", responses: MockResponse.Status(status: HttpStatusCode.NotFound));

        using TvdbSeriesClient client = new(id: unknownId);
        TvdbSeriesResponse? result = await client.Details();

        result.Should().BeNull();
    }
}
