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
public sealed class MusicBrainzRecordingClientTests : ProviderHttpHarness
{
    public MusicBrainzRecordingClientTests()
        : base(HttpClientNames.MusicBrainz) { }

    [Fact]
    public async Task WithAllAppends_ById_RequestsRecordingPathAndMapsTitle()
    {
        Guid recordingId = Guid.NewGuid();
        MusicBrainzRecordingAppends body = new() { Id = recordingId, Title = "Reckoner" };
        Handler.WhenGet($"recording/{recordingId}", MockResponse.Json(HttpStatusCode.OK, body));

        using MusicBrainzRecordingClient client = new();
        MusicBrainzRecordingAppends? result = await client.WithAllAppends(recordingId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(recordingId);
        result.Title.Should().Be("Reckoner");

        CapturedRequest request = Handler
            .Requests.Should()
            .ContainSingle(r => r.Path.Contains($"recording/{recordingId}"))
            .Which;
        request
            .Query.Should()
            .ContainKey("inc")
            .WhoseValue.Should()
            .Be("artist-credits+artists+releases+tags+genres");
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
        Handler.WhenGet("recording", MockResponse.Json(HttpStatusCode.OK, body));

        using MusicBrainzRecordingClient client = new();
        MusicBrainzSearchResponse? result = await client.SearchRecordingsDynamic(query);

        result.Should().NotBeNull();
        result!.Count.Should().Be(1);
        result.Recordings.Should().ContainSingle(r => r.Title == "Reckoner" && r.Score == 100);

        CapturedRequest request = Handler.Requests.Should().ContainSingle().Which;
        request.Query.Should().ContainKey("query").WhoseValue.Should().Be(query);
        request.Query.Should().ContainKey("inc").WhoseValue.Should().Be("releases");
    }

    [Fact]
    public async Task WithAppends_UnknownMbid_ReturnsNull()
    {
        Guid unknownId = Guid.NewGuid();
        Handler.WhenGet($"recording/{unknownId}", MockResponse.Status(HttpStatusCode.NotFound));

        using MusicBrainzRecordingClient client = new(unknownId);
        MusicBrainzRecordingAppends? result = await client.WithAllAppends();

        result.Should().BeNull();
    }
}
