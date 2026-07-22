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
public sealed class MusicBrainzReleaseGroupClientTests : ProviderHttpHarness
{
    public MusicBrainzReleaseGroupClientTests()
        : base(httpClientNames: HttpClientNames.MusicBrainz) { }

    [Fact]
    public async Task WithAllAppends_RequestsReleaseGroupPathWithArtistsAndReleasesInc()
    {
        Guid releaseGroupId = Guid.NewGuid();
        MusicBrainzReleaseGroupDetails body = new() { Id = releaseGroupId, Title = "Kid A" };
        Handler.WhenGet(
            pathContains: $"release-group/{releaseGroupId}",
            responses: MockResponse.Json(status: HttpStatusCode.OK, body: body)
        );

        using MusicBrainzReleaseGroupClient client = new(id: releaseGroupId);
        MusicBrainzReleaseGroupDetails? result = await client.WithAllAppends();

        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: releaseGroupId);
        result.Title.Should().Be(expected: "Kid A");

        CapturedRequest request = Handler
            .Requests.Should()
            .ContainSingle(predicate: r => r.Path.Contains($"release-group/{releaseGroupId}"))
            .Which;
        request.Query.Should().ContainKey(expected: "inc").WhoseValue.Should().Be(expected: "artists+releases");
        request.Query.Should().ContainKey(expected: "fmt").WhoseValue.Should().Be(expected: "json");
    }

    [Fact]
    public async Task SearchReleaseGroups_BuildsQueryAndFmtParameters_NoIncParameter()
    {
        // Requirement: unlike SearchReleases, SearchReleaseGroups sends no
        // inc= at all — pinning that so a future "make search consistent"
        // refactor is a deliberate, reviewed change rather than an accident.
        string query = $"Kid A {Guid.NewGuid():N}";
        MusicBrainzReleaseGroupSearchResponse body = new()
        {
            ReleaseGroups = [new() { Id = Guid.NewGuid(), Title = "Kid A" }],
        };
        Handler.WhenGet(pathContains: "release-group", responses: MockResponse.Json(status: HttpStatusCode.OK, body: body));

        using MusicBrainzReleaseGroupClient client = new(id: Guid.NewGuid());
        MusicBrainzReleaseGroupSearchResponse? result = await client.SearchReleaseGroups(query: query);

        result.Should().NotBeNull();
        result!.ReleaseGroups.Should().ContainSingle(predicate: rg => rg.Title == "Kid A");

        CapturedRequest request = Handler.Requests.Should().ContainSingle().Which;
        request.Path.Should().Be(expected: "/ws/2/release-group");
        request.Query.Should().ContainKey(expected: "query").WhoseValue.Should().Be(expected: query);
        request.Query.Should().NotContainKey(unexpected: "inc");
    }

    [Fact]
    public async Task WithAllAppends_UnknownMbid_ReturnsNull()
    {
        Guid unknownId = Guid.NewGuid();
        Handler.WhenGet(pathContains: $"release-group/{unknownId}", responses: MockResponse.Status(status: HttpStatusCode.NotFound));

        using MusicBrainzReleaseGroupClient client = new(id: unknownId);
        MusicBrainzReleaseGroupDetails? result = await client.WithAllAppends();

        result.Should().BeNull();
    }
}
