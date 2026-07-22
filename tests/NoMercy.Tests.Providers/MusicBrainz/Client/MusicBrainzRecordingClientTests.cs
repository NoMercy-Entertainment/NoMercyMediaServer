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
public sealed class MusicBrainzRecordingClientTests : ProviderHttpHarness
{
    public MusicBrainzRecordingClientTests()
        : base(httpClientNames: HttpClientNames.MusicBrainz) { }

    [Fact]
    public async Task WithAllAppends_ById_RequestsRecordingPathAndMapsTitle()
    {
        Guid recordingId = Guid.NewGuid();
        MusicBrainzRecordingAppends body = new() { Id = recordingId, Title = "Reckoner" };
        Handler.WhenGet(pathContains: $"recording/{recordingId}", responses: MockResponse.Json(status: HttpStatusCode.OK, body: body));

        using MusicBrainzRecordingClient client = new();
        MusicBrainzRecordingAppends? result = await client.WithAllAppends(id: recordingId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: recordingId);
        result.Title.Should().Be(expected: "Reckoner");

        CapturedRequest request = Handler
            .Requests.Should()
            .ContainSingle(predicate: r => r.Path.Contains($"recording/{recordingId}"))
            .Which;
        request
            .Query.Should()
            .ContainKey(expected: "inc")
            .WhoseValue.Should()
            .Be(expected: "artist-credits+artists+releases+tags+genres");
    }

    [Fact]
    public async Task SearchRecordingsDynamic_MapsPaginatedSearchResponse()
    {
        // Requirement: the "…Dynamic" overload deserializes into the search
        // envelope (count/offset/recordings), not the single-recording appends
        // shape the plain SearchRecordings overload uses for the same endpoint.
        string query = $"Reckoner {Guid.NewGuid():N}";
        MusicBrainzSearchResponse body = new()
        {
            Count = 1,
            Offset = 0,
            Recordings =
            [
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = "Reckoner",
                    Score = 100,
                },
            ],
        };
        Handler.WhenGet(pathContains: "recording", responses: MockResponse.Json(status: HttpStatusCode.OK, body: body));

        using MusicBrainzRecordingClient client = new();
        MusicBrainzSearchResponse? result = await client.SearchRecordingsDynamic(query: query);

        result.Should().NotBeNull();
        result!.Count.Should().Be(expected: 1);
        result.Recordings.Should().ContainSingle(predicate: r => r.Title == "Reckoner" && r.Score == 100);

        CapturedRequest request = Handler.Requests.Should().ContainSingle().Which;
        request.Query.Should().ContainKey(expected: "query").WhoseValue.Should().Be(expected: query);
        request.Query.Should().ContainKey(expected: "inc").WhoseValue.Should().Be(expected: "releases");
    }

    [Fact]
    public async Task WithAppends_UnknownMbid_ReturnsNull()
    {
        Guid unknownId = Guid.NewGuid();
        Handler.WhenGet(pathContains: $"recording/{unknownId}", responses: MockResponse.Status(status: HttpStatusCode.NotFound));

        using MusicBrainzRecordingClient client = new(id: unknownId);
        MusicBrainzRecordingAppends? result = await client.WithAllAppends();

        result.Should().BeNull();
    }
}
