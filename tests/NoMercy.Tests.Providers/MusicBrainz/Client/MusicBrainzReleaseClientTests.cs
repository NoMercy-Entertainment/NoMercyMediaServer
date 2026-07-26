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
public sealed class MusicBrainzReleaseClientTests : ProviderHttpHarness
{
    public MusicBrainzReleaseClientTests()
        : base(HttpClientNames.MusicBrainz) { }

    [Fact]
    public async Task WithAllAppends_ById_RequestsReleasePathWithFullIncList()
    {
        // Requirement: the overload that takes an explicit id must target that
        // id's path even when the client itself was constructed without one.
        Guid releaseId = Guid.NewGuid();
        MusicBrainzReleaseAppends body = new() { Id = releaseId, Title = "In Rainbows" };
        Handler.WhenGet($"release/{releaseId}", MockResponse.Json(HttpStatusCode.OK, body));

        using MusicBrainzReleaseClient client = new();
        MusicBrainzReleaseAppends? result = await client.WithAllAppends(releaseId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(releaseId);
        result.Title.Should().Be("In Rainbows");

        CapturedRequest request = Handler
            .Requests.Should()
            .ContainSingle(r => r.Path.Contains($"release/{releaseId}"))
            .Which;
        request.Query.Should().ContainKey("fmt").WhoseValue.Should().Be("json");
        request.Query["inc"].Should().Contain("artist-credits").And.Contain("recordings");
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
        Handler.WhenGet("release", MockResponse.Json(HttpStatusCode.OK, body));

        using MusicBrainzReleaseClient client = new();
        MusicBrainzReleaseSearchResponse? result = await client.SearchReleases(query);

        result.Should().NotBeNull();
        result!.Releases.Should().HaveCount(1);

        CapturedRequest request = Handler.Requests.Should().ContainSingle().Which;
        request.Path.Should().Be("/ws/2/release");
        request.Query.Should().ContainKey("query").WhoseValue.Should().Be(query);
        request.Query.Should().ContainKey("inc").WhoseValue.Should().Be("recordings");
        request.Query.Should().ContainKey("fmt").WhoseValue.Should().Be("json");
    }

    [Fact]
    public async Task WithAppends_UnknownMbid_ReturnsNull()
    {
        Guid unknownId = Guid.NewGuid();
        Handler.WhenGet($"release/{unknownId}", MockResponse.Status(HttpStatusCode.NotFound));

        using MusicBrainzReleaseClient client = new(unknownId);
        MusicBrainzReleaseAppends? result = await client.WithAppends(["artists"]);

        result.Should().BeNull();
    }
}
