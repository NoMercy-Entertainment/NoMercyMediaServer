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
using FluentAssertions;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

[Trait("Category", "DashboardNotifications")]
public class NotificationsControllerTests : IClassFixture<NoMercyApiFactory>
{
    private const string BroadcastUrl = "/api/v1/dashboard/notifications/broadcast";

    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;
    private readonly HttpClient _secondaryUser;

    public NotificationsControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
        _secondaryUser = factory.CreateClient().AsSecondaryUser();
    }

    private static StringContent JsonBody(object obj) =>
        new(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json");

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, object body) =>
        client.PostAsync(BroadcastUrl, JsonBody(body));

    [Fact]
    public async Task Broadcast_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await PostAsync(
            _unauthed,
            new { title = "Maintenance", body = "Server restarting soon" }
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Broadcast_ReturnsForbidden_WhenAuthenticatedButNotModerator()
    {
        // TestAuthHandler.SecondaryUserId is seeded Allowed=true, Owner=false,
        // Manage=false — a real, allowed-but-non-moderator identity.
        HttpResponseMessage response = await PostAsync(
            _secondaryUser,
            new { title = "Maintenance", body = "Server restarting soon" }
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Broadcast_ReturnsOk_WhenOwner()
    {
        HttpResponseMessage response = await PostAsync(
            _authed,
            new { title = "Maintenance", body = "Server restarting soon" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Broadcast_ReturnsEnvelopeWithNotifiedUserCountAndEchoedFields()
    {
        HttpResponseMessage response = await PostAsync(
            _authed,
            new
            {
                title = "Maintenance",
                body = "Server restarting soon",
                type = "warning",
            }
        );

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("data", out JsonElement data)
            .Should()
            .BeTrue("broadcast response must have a 'data' property");

        data.TryGetProperty("notified_users", out JsonElement notifiedUsers)
            .Should()
            .BeTrue("broadcast response must expose 'notified_users'");
        // Two allowed users are seeded (owner + secondary) — both count as
        // targets of the broadcast regardless of live connection state.
        notifiedUsers.GetInt32().Should().BeGreaterThanOrEqualTo(2);

        data.TryGetProperty("title", out JsonElement title).Should().BeTrue();
        title.GetString().Should().Be("Maintenance");

        data.TryGetProperty("body", out JsonElement bodyField).Should().BeTrue();
        bodyField.GetString().Should().Be("Server restarting soon");

        data.TryGetProperty("type", out JsonElement type).Should().BeTrue();
        type.GetString().Should().Be("warning");
    }

    [Fact]
    public async Task Broadcast_DefaultsTypeToInfo_WhenTypeOmitted()
    {
        HttpResponseMessage response = await PostAsync(
            _authed,
            new { title = "Maintenance", body = "Server restarting soon" }
        );

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        JsonElement data = doc.RootElement.GetProperty("data");
        data.GetProperty("type").GetString().Should().Be("info");
    }

    [Fact]
    public async Task Broadcast_ReturnsBadRequest_WhenTitleMissing()
    {
        HttpResponseMessage response = await PostAsync(
            _authed,
            new { title = "", body = "Server restarting soon" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Broadcast_ReturnsBadRequest_WhenBodyMissing()
    {
        HttpResponseMessage response = await PostAsync(
            _authed,
            new { title = "Maintenance", body = "" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
