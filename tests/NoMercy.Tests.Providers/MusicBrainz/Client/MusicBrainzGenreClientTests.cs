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
public sealed class MusicBrainzGenreClientTests : ProviderHttpHarness
{
    public MusicBrainzGenreClientTests()
        : base(HttpClientNames.MusicBrainz) { }

    [Fact]
    public async Task FirstPage_RequestsGenreAllWithLimit100Offset0()
    {
        MusicBrainzAllGenres body = new()
        {
            Genres = [new() { Id = Guid.NewGuid(), Name = "ambient" }],
            GenreCount = 1,
        };
        Handler.WhenGet("genre/all", MockResponse.Json(HttpStatusCode.OK, body));

        using MusicBrainzGenreClient client = new();
        MusicBrainzAllGenres? result = await client.FirstPage();

        result.Should().NotBeNull();
        result!.Genres.Should().ContainSingle(g => g.Name == "ambient");

        CapturedRequest request = Handler.Requests.Should().ContainSingle().Which;
        request.Path.Should().Be("/ws/2/genre/all");
        request.Query.Should().ContainKey("limit").WhoseValue.Should().Be("100");
        request.Query.Should().ContainKey("offset").WhoseValue.Should().Be("0");
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
        Handler.WhenGet("genre/all", MockResponse.Json(HttpStatusCode.OK, secondPage));

        using MusicBrainzGenreClient client = new();
        List<MusicBrainzGenre> remaining = await client.RemainingPages(firstPage);

        remaining.Should().ContainSingle(g => g.Name == "downtempo");

        CapturedRequest request = Handler.Requests.Should().ContainSingle().Which;
        request.Query.Should().ContainKey("offset").WhoseValue.Should().Be("2");
        request.Query.Should().ContainKey("limit").WhoseValue.Should().Be("2");
    }

    [Fact]
    public async Task SearchGenre_ReturnsFirstMatchOrNullWhenEmpty()
    {
        string query = $"ambient {Guid.NewGuid():N}";
        MusicBrainzAllGenres body = new()
        {
            Genres = [new() { Id = Guid.NewGuid(), Name = "ambient" }],
        };
        Handler.WhenGet("genre", MockResponse.Json(HttpStatusCode.OK, body));

        using MusicBrainzGenreClient client = new();
        MusicBrainzGenre? result = await client.SearchGenre(query);

        result.Should().NotBeNull();
        result!.Name.Should().Be("ambient");

        CapturedRequest request = Handler.Requests.Should().ContainSingle().Which;
        request.Path.Should().Be("/ws/2/genre");
        request.Query.Should().ContainKey("query").WhoseValue.Should().Be(query);
    }
}
