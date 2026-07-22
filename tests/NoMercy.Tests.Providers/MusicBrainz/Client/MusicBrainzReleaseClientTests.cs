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
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Tests.Providers.Infrastructure;

namespace NoMercy.Tests.Providers.MusicBrainz.Client;

[Collection(name: "HttpClientProvider")]
public sealed class MusicBrainzReleaseClientTests : ProviderHttpHarness
{
    public MusicBrainzReleaseClientTests()
        : base(httpClientNames: HttpClientNames.MusicBrainz) { }

    [Fact]
    public async Task WithAllAppends_ById_RequestsReleasePathWithFullIncList()
    {
        // Requirement: the overload that takes an explicit id must target that
        // id's path even when the client itself was constructed without one.
        Guid releaseId = Guid.NewGuid();
        MusicBrainzReleaseAppends body = new() { Id = releaseId, Title = "In Rainbows" };
        Handler.WhenGet(pathContains: $"release/{releaseId}", responses: MockResponse.Json(status: HttpStatusCode.OK, body: body));

        using MusicBrainzReleaseClient client = new();
        MusicBrainzReleaseAppends? result = await client.WithAllAppends(id: releaseId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: releaseId);
        result.Title.Should().Be(expected: "In Rainbows");

        CapturedRequest request = Handler
            .Requests.Should()
            .ContainSingle(predicate: r => r.Path.Contains($"release/{releaseId}"))
            .Which;
        request.Query.Should().ContainKey(expected: "fmt").WhoseValue.Should().Be(expected: "json");
        request.Query[key: "inc"].Should().Contain(expected: "artist-credits").And.Contain(expected: "recordings");
    }

    [Fact]
    public async Task SearchReleases_BuildsQueryIncAndFmtParameters()
    {
        // Requirement: SearchReleases fixes inc=recordings (unlike WithAppends,
        // which takes the caller's append list) and forwards the query text.
        string query = $"In Rainbows {Guid.NewGuid():N}";
        MusicBrainzReleaseSearchResponse body = new()
        {
            Releases = [new() { Id = Guid.NewGuid(), Barcode = "test" }],
        };
        Handler.WhenGet(pathContains: "release", responses: MockResponse.Json(status: HttpStatusCode.OK, body: body));

        using MusicBrainzReleaseClient client = new();
        MusicBrainzReleaseSearchResponse? result = await client.SearchReleases(query: query);

        result.Should().NotBeNull();
        result!.Releases.Should().HaveCount(expected: 1);

        CapturedRequest request = Handler.Requests.Should().ContainSingle().Which;
        request.Path.Should().Be(expected: "/ws/2/release");
        request.Query.Should().ContainKey(expected: "query").WhoseValue.Should().Be(expected: query);
        request.Query.Should().ContainKey(expected: "inc").WhoseValue.Should().Be(expected: "recordings");
        request.Query.Should().ContainKey(expected: "fmt").WhoseValue.Should().Be(expected: "json");
    }

    [Fact]
    public async Task WithAppends_UnknownMbid_ReturnsNull()
    {
        Guid unknownId = Guid.NewGuid();
        Handler.WhenGet(pathContains: $"release/{unknownId}", responses: MockResponse.Status(status: HttpStatusCode.NotFound));

        using MusicBrainzReleaseClient client = new(id: unknownId);
        MusicBrainzReleaseAppends? result = await client.WithAppends(appendices: ["artists"]);

        result.Should().BeNull();
    }
}
