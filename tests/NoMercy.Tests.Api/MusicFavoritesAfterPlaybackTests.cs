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
using System.Text;
using System.Text.Json;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

/// <summary>
/// The favorites row only has anything to show once something has been played,
/// so every test that called it on an untouched database saw the empty case and
/// passed. With one play recorded the card is built for real, and a top item
/// missing its link took the whole response down.
/// </summary>
[Trait("Category", "Characterization")]
public class MusicFavoritesAfterPlaybackTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _client;

    public MusicFavoritesAfterPlaybackTests(NoMercyApiFactory factory)
    {
        _client = factory.CreateClient().AsAuthenticated();
    }

    private static StringContent JsonBody(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    [Fact]
    public async Task Favorites_AfterATrackHasBeenPlayed_ReturnsTheTopItemsWithLinks()
    {
        HttpResponseMessage playback = await _client.PostAsync(
            $"/api/v1/music/tracks/{NoMercyApiFactory.TrackId1}/playback",
            JsonBody(new { })
        );
        Assert.Equal(HttpStatusCode.OK, playback.StatusCode);

        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/music/start/favorites",
            JsonBody(new { })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonElement container = JsonDocument
            .Parse(body)
            .RootElement.GetProperty("data")[0]
            .GetProperty("props");

        JsonElement items = container.GetProperty("items");
        Assert.NotEmpty(items.EnumerateArray());

        foreach (JsonElement item in items.EnumerateArray())
        {
            JsonElement data = item.GetProperty("props").GetProperty("data");
            string? link = data.GetProperty("link").GetString();

            Assert.False(string.IsNullOrWhiteSpace(link), $"A top item carried no link: {data}");
        }
    }
}
