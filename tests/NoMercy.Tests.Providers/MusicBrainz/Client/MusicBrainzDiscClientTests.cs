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
public sealed class MusicBrainzDiscClientTests : ProviderHttpHarness
{
    public MusicBrainzDiscClientTests()
        : base(HttpClientNames.MusicBrainz) { }

    [Fact]
    public async Task LookupByDiscId_RequestsDiscIdPathWithDefaultIncludes()
    {
        string discId = $"disc-{Guid.NewGuid():N}";
        DiscIdLookupResponse body = new()
        {
            Id = discId,
            Releases = [new() { Id = Guid.NewGuid(), Barcode = "test" }],
        };
        Handler.WhenGet($"discid/{discId}", MockResponse.Json(HttpStatusCode.OK, body));

        using MusicBrainzDiscClient client = new();
        DiscIdLookupResponse? result = await client.LookupByDiscId(discId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(discId);
        result.Releases.Should().HaveCount(1);

        CapturedRequest request = Handler
            .Requests.Should()
            .ContainSingle(r => r.Path.Contains($"discid/{discId}"))
            .Which;
        request
            .Query.Should()
            .ContainKey("inc")
            .WhoseValue.Should()
            .Be("recordings+artist-credits+release-groups");
        request.Query.Should().ContainKey("fmt").WhoseValue.Should().Be("json");
    }

    [Fact]
    public async Task LookupByTocString_RequestsFuzzyDiscidPathWithTocParameter()
    {
        // Requirement: the fuzzy path always hits "discid/-" (never a literal
        // id), and forwards the caller-built toc= string verbatim.
        string toc = $"1 2 150 {Guid.NewGuid():N}";
        DiscIdLookupResponse body = new() { Releases = [] };
        Handler.WhenGet("discid/-", MockResponse.Json(HttpStatusCode.OK, body));

        using MusicBrainzDiscClient client = new();
        DiscIdLookupResponse? result = await client.LookupByTocString(toc);

        result.Should().NotBeNull();

        CapturedRequest request = Handler.Requests.Should().ContainSingle().Which;
        request.Path.Should().Be("/ws/2/discid/-");
        request.Query.Should().ContainKey("toc").WhoseValue.Should().Be(toc);
    }

    [Fact]
    public async Task LookupByDiscId_UnknownDisc_ReturnsNull()
    {
        string discId = $"disc-{Guid.NewGuid():N}";
        Handler.WhenGet($"discid/{discId}", MockResponse.Status(HttpStatusCode.NotFound));

        using MusicBrainzDiscClient client = new();
        DiscIdLookupResponse? result = await client.LookupByDiscId(discId);

        result.Should().BeNull();
    }
}
