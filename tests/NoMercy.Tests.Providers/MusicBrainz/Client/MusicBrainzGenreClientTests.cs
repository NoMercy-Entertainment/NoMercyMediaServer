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
public sealed class MusicBrainzGenreClientTests : ProviderHttpHarness
{
    public MusicBrainzGenreClientTests()
        : base(httpClientNames: HttpClientNames.MusicBrainz) { }

    [Fact]
    public async Task FirstPage_RequestsGenreAllWithLimit100Offset0()
    {
        MusicBrainzAllGenres body = new()
        {
            Genres = [new() { Id = Guid.NewGuid(), Name = "ambient" }],
            GenreCount = 1,
        };
        Handler.WhenGet(pathContains: "genre/all", responses: MockResponse.Json(status: HttpStatusCode.OK, body: body));

        using MusicBrainzGenreClient client = new();
        MusicBrainzAllGenres? result = await client.FirstPage();

        result.Should().NotBeNull();
        result!.Genres.Should().ContainSingle(predicate: g => g.Name == "ambient");

        CapturedRequest request = Handler.Requests.Should().ContainSingle().Which;
        request.Path.Should().Be(expected: "/ws/2/genre/all");
        request.Query.Should().ContainKey(expected: "limit").WhoseValue.Should().Be(expected: "100");
        request.Query.Should().ContainKey(expected: "offset").WhoseValue.Should().Be(expected: "0");
    }

    [Fact]
    public async Task RemainingPages_WalksOffsetsUntilGenreCountExhausted()
    {
        // Requirement: RemainingPages must keep requesting page-sized chunks,
        // advancing "offset" by the first page's size each time, until the
        // running offset reaches the server-reported total genre count.
        MusicBrainzGenre[] firstPageGenres =
        [
            new() { Id = Guid.NewGuid(), Name = "ambient" },
            new() { Id = Guid.NewGuid(), Name = "techno" },
        ];
        MusicBrainzAllGenres firstPage = new() { Genres = firstPageGenres, GenreCount = 3 };
        MusicBrainzAllGenres secondPage = new()
        {
            Genres = [new() { Id = Guid.NewGuid(), Name = "downtempo" }],
            GenreCount = 3,
        };
        Handler.WhenGet(pathContains: "genre/all", responses: MockResponse.Json(status: HttpStatusCode.OK, body: secondPage));

        using MusicBrainzGenreClient client = new();
        List<MusicBrainzGenre> remaining = await client.RemainingPages(firstPage: firstPage);

        remaining.Should().ContainSingle(predicate: g => g.Name == "downtempo");

        CapturedRequest request = Handler.Requests.Should().ContainSingle().Which;
        request.Query.Should().ContainKey(expected: "offset").WhoseValue.Should().Be(expected: "2");
        request.Query.Should().ContainKey(expected: "limit").WhoseValue.Should().Be(expected: "2");
    }

    [Fact]
    public async Task SearchGenre_ReturnsFirstMatchOrNullWhenEmpty()
    {
        string query = $"ambient {Guid.NewGuid():N}";
        MusicBrainzAllGenres body = new()
        {
            Genres = [new() { Id = Guid.NewGuid(), Name = "ambient" }],
        };
        Handler.WhenGet(pathContains: "genre", responses: MockResponse.Json(status: HttpStatusCode.OK, body: body));

        using MusicBrainzGenreClient client = new();
        MusicBrainzGenre? result = await client.SearchGenre(query: query);

        result.Should().NotBeNull();
        result!.Name.Should().Be(expected: "ambient");

        CapturedRequest request = Handler.Requests.Should().ContainSingle().Which;
        request.Path.Should().Be(expected: "/ws/2/genre");
        request.Query.Should().ContainKey(expected: "query").WhoseValue.Should().Be(expected: query);
    }
}
