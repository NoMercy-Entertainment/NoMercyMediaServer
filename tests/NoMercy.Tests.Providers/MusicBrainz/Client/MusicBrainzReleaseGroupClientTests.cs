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
using NoMercy.Tests.Common.Providers;

namespace NoMercy.Tests.Providers.MusicBrainz.Client;

[Collection("HttpClientProvider")]
public sealed class MusicBrainzReleaseGroupClientTests : ProviderHttpHarness
{
    public MusicBrainzReleaseGroupClientTests()
        : base(HttpClientNames.MusicBrainz) { }

    [Fact]
    public async Task WithAllAppends_RequestsReleaseGroupPathWithArtistsAndReleasesInc()
    {
        Guid releaseGroupId = Guid.NewGuid();
        MusicBrainzReleaseGroupDetails body = new() { Id = releaseGroupId, Title = "Kid A" };
        Handler.WhenGet(
            $"release-group/{releaseGroupId}",
            MockResponse.Json(HttpStatusCode.OK, body)
        );

        using MusicBrainzReleaseGroupClient client = new(releaseGroupId);
        MusicBrainzReleaseGroupDetails? result = await client.WithAllAppends();

        result.Should().NotBeNull();
        result!.Id.Should().Be(releaseGroupId);
        result.Title.Should().Be("Kid A");

        CapturedRequest request = Handler
            .Requests.Should()
            .ContainSingle(r => r.Path.Contains($"release-group/{releaseGroupId}"))
            .Which;
        request.Query.Should().ContainKey("inc").WhoseValue.Should().Be("artists+releases");
        request.Query.Should().ContainKey("fmt").WhoseValue.Should().Be("json");
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
        Handler.WhenGet("release-group", MockResponse.Json(HttpStatusCode.OK, body));

        using MusicBrainzReleaseGroupClient client = new(Guid.NewGuid());
        MusicBrainzReleaseGroupSearchResponse? result = await client.SearchReleaseGroups(query);

        result.Should().NotBeNull();
        result!.ReleaseGroups.Should().ContainSingle(rg => rg.Title == "Kid A");

        CapturedRequest request = Handler.Requests.Should().ContainSingle().Which;
        request.Path.Should().Be("/ws/2/release-group");
        request.Query.Should().ContainKey("query").WhoseValue.Should().Be(query);
        request.Query.Should().NotContainKey("inc");
    }

    [Fact]
    public async Task WithAllAppends_UnknownMbid_ReturnsNull()
    {
        Guid unknownId = Guid.NewGuid();
        Handler.WhenGet($"release-group/{unknownId}", MockResponse.Status(HttpStatusCode.NotFound));

        using MusicBrainzReleaseGroupClient client = new(unknownId);
        MusicBrainzReleaseGroupDetails? result = await client.WithAllAppends();

        result.Should().BeNull();
    }
}
