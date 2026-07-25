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

[Collection("HttpClientProvider")]
public sealed class MusicBrainzArtistClientTests : ProviderHttpHarness
{
    public MusicBrainzArtistClientTests()
        : base(HttpClientNames.MusicBrainz) { }

    [Fact]
    public async Task WithAllAppends_BuildsIncAndFmtQuery_AndMapsAppends()
    {
        // Requirement: WithAllAppends() must request exactly the artist
        // append set the metadata pipeline depends on, '+'-joined (MusicBrainz's
        // inc= separator, unlike TMDB's comma), plus fmt=json.
        // Each test uses a freshly generated artist id, so the shared on-disk
        // CacheController cache (keyed by URL) can never answer this request
        // with another test's cached body.
        Guid artistId = Guid.NewGuid();
        MusicBrainzArtistAppends body = new()
        {
            Id = artistId,
            Name = "Boards of Canada",
            Gender = "unknown",
        };
        Handler.WhenGet($"artist/{artistId}", MockResponse.Json(HttpStatusCode.OK, body));

        using MusicBrainzArtistClient client = new(artistId);

        MusicBrainzArtistAppends? result = await client.WithAllAppends();

        result.Should().NotBeNull();
        result!.Id.Should().Be(artistId);
        result.Name.Should().Be("Boards of Canada");

        CapturedRequest request = Handler
            .Requests.Should()
            .ContainSingle(r => r.Path.Contains($"artist/{artistId}"))
            .Which;
        request.Query.Should().ContainKey("fmt").WhoseValue.Should().Be("json");
        request
            .Query.Should()
            .ContainKey("inc")
            .WhoseValue.Should()
            .Be("genres+recordings+releases+release-groups+works");
    }

    [Fact]
    public async Task SearchArtists_BuildsQueryAndFmtParameters()
    {
        // Requirement: SearchArtists must forward the caller's raw query text
        // verbatim (no client-side escaping/mangling — QueryHelpers handles
        // percent-encoding on the wire) plus fmt=json, and hit the collection
        // endpoint "artist" rather than "artist/{id}".
        // A per-test unique query string keeps this request out of the shared
        // on-disk CacheController cache (keyed by full URL incl. query).
        string query = $"Boards of Canada {Guid.NewGuid():N}";
        MusicBrainzArtistAppends body = new() { Id = Guid.NewGuid(), Name = query };
        Handler.WhenGet("artist", MockResponse.Json(HttpStatusCode.OK, body));

        using MusicBrainzArtistClient client = new();
        MusicBrainzArtistAppends? result = await client.SearchArtists(query);

        result.Should().NotBeNull();
        result!.Name.Should().Be(query);

        CapturedRequest request = Handler.Requests.Should().ContainSingle().Which;
        request.Path.Should().Be("/ws/2/artist");
        request.Query.Should().ContainKey("query").WhoseValue.Should().Be(query);
        request.Query.Should().ContainKey("fmt").WhoseValue.Should().Be("json");
    }

    [Fact]
    public async Task WithAppends_UnknownMbid_ReturnsNull()
    {
        Guid unknownId = Guid.NewGuid();
        Handler.WhenGet($"artist/{unknownId}", MockResponse.Status(HttpStatusCode.NotFound));

        using MusicBrainzArtistClient client = new(unknownId);
        MusicBrainzArtistAppends? result = await client.WithAllAppends();

        result.Should().BeNull();
    }
}
