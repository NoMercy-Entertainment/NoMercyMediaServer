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
using NoMercy.Networking.Http;
using NoMercy.Networking.Messaging;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

[Trait(name: "Category", value: "DashboardNotifications")]
public class NotificationsControllerTests : IClassFixture<NoMercyApiFactory>
{
    private const string BroadcastUrl = "/api/v1/dashboard/notifications/broadcast";
    private const string SendUrl = "/api/v1/dashboard/notifications/send";

    private readonly NoMercyApiFactory _factory;
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;
    private readonly HttpClient _secondaryUser;

    public NotificationsControllerTests(NoMercyApiFactory factory)
    {
        _factory = factory;
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
        _secondaryUser = factory.CreateClient().AsSecondaryUser();
    }

    private static StringContent JsonBody(object obj) =>
        new(content: JsonSerializer.Serialize(value: obj), encoding: Encoding.UTF8, mediaType: "application/json");

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, object body) =>
        client.PostAsync(requestUri: BroadcastUrl, content: JsonBody(obj: body));

    private static Task<HttpResponseMessage> PostSendAsync(HttpClient client, object body) =>
        client.PostAsync(requestUri: SendUrl, content: JsonBody(obj: body));

    [Fact]
    public async Task Broadcast_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await PostAsync(
            client: _unauthed,
            body: new { title = "Maintenance", body = "Server restarting soon" }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Broadcast_ReturnsForbidden_WhenAuthenticatedButNotModerator()
    {
        // TestAuthHandler.SecondaryUserId is seeded Allowed=true, Owner=false,
        // Manage=false — a real, allowed-but-non-moderator identity.
        HttpResponseMessage response = await PostAsync(
            client: _secondaryUser,
            body: new { title = "Maintenance", body = "Server restarting soon" }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Broadcast_ReturnsOk_WhenOwner()
    {
        HttpResponseMessage response = await PostAsync(
            client: _authed,
            body: new { title = "Maintenance", body = "Server restarting soon" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);
    }

    [Fact]
    public async Task Broadcast_ReturnsEnvelopeWithNotifiedUserCountAndEchoedFields()
    {
        HttpResponseMessage response = await PostAsync(
            client: _authed,
            body: new
            {
                title = "Maintenance",
                body = "Server restarting soon",
                type = "warning",
            }
        );

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.TryGetProperty(propertyName: "data", value: out JsonElement data)
            .Should()
            .BeTrue(because: "broadcast response must have a 'data' property");

        data.TryGetProperty(propertyName: "notified_users", value: out JsonElement notifiedUsers)
            .Should()
            .BeTrue(because: "broadcast response must expose 'notified_users'");
        // Two allowed users are seeded (owner + secondary) — both count as
        // targets of the broadcast regardless of live connection state.
        notifiedUsers.GetInt32().Should().BeGreaterThanOrEqualTo(expected: 2);

        data.TryGetProperty(propertyName: "title", value: out JsonElement title).Should().BeTrue();
        title.GetString().Should().Be(expected: "Maintenance");

        data.TryGetProperty(propertyName: "body", value: out JsonElement bodyField).Should().BeTrue();
        bodyField.GetString().Should().Be(expected: "Server restarting soon");

        data.TryGetProperty(propertyName: "type", value: out JsonElement type).Should().BeTrue();
        type.GetString().Should().Be(expected: "warning");
    }

    [Fact]
    public async Task Broadcast_DefaultsTypeToInfo_WhenTypeOmitted()
    {
        HttpResponseMessage response = await PostAsync(
            client: _authed,
            body: new { title = "Maintenance", body = "Server restarting soon" }
        );

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        JsonElement data = doc.RootElement.GetProperty(propertyName: "data");
        data.GetProperty(propertyName: "type").GetString().Should().Be(expected: "info");
    }

    [Fact]
    public async Task Broadcast_ReturnsBadRequest_WhenTitleMissing()
    {
        HttpResponseMessage response = await PostAsync(
            client: _authed,
            body: new { title = "", body = "Server restarting soon" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Broadcast_ReturnsBadRequest_WhenBodyMissing()
    {
        HttpResponseMessage response = await PostAsync(
            client: _authed,
            body: new { title = "Maintenance", body = "" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    // =========================================================================
    // POST /notifications/send — single-user targeting
    // =========================================================================

    [Fact]
    public async Task Send_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await PostSendAsync(
            client: _unauthed,
            body: new
            {
                user_id = TestAuthHandler.SecondaryUserId,
                title = "Heads up",
                body = "Your download finished",
            }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Send_ReturnsForbidden_WhenAuthenticatedButNotModerator()
    {
        // TestAuthHandler.SecondaryUserId is seeded Allowed=true, Owner=false,
        // Manage=false — a real, allowed-but-non-moderator identity.
        HttpResponseMessage response = await PostSendAsync(
            client: _secondaryUser,
            body: new
            {
                user_id = TestAuthHandler.DefaultUserId,
                title = "Heads up",
                body = "Your download finished",
            }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Send_ReturnsBadRequest_WhenUserIdMissing()
    {
        HttpResponseMessage response = await PostSendAsync(
            client: _authed,
            body: new { title = "Heads up", body = "Your download finished" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Send_ReturnsBadRequest_WhenTitleMissing()
    {
        HttpResponseMessage response = await PostSendAsync(
            client: _authed,
            body: new
            {
                user_id = TestAuthHandler.SecondaryUserId,
                title = "",
                body = "Your download finished",
            }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Send_ReturnsBadRequest_WhenBodyMissing()
    {
        HttpResponseMessage response = await PostSendAsync(
            client: _authed,
            body: new
            {
                user_id = TestAuthHandler.SecondaryUserId,
                title = "Heads up",
                body = "",
            }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Send_ReturnsNotFound_WhenUserDoesNotExist()
    {
        HttpResponseMessage response = await PostSendAsync(
            client: _authed,
            body: new
            {
                user_id = Guid.NewGuid(),
                title = "Heads up",
                body = "Your download finished",
            }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Send_ReturnsEnvelopeWithTargetUserAndEchoedFields()
    {
        HttpResponseMessage response = await PostSendAsync(
            client: _authed,
            body: new
            {
                user_id = TestAuthHandler.SecondaryUserId,
                title = "Heads up",
                body = "Your download finished",
                type = "success",
            }
        );

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK, because: body);

        doc.RootElement.TryGetProperty(propertyName: "data", value: out JsonElement data)
            .Should()
            .BeTrue(because: "send response must have a 'data' property");

        data.GetProperty(propertyName: "user_id").GetGuid().Should().Be(expected: TestAuthHandler.SecondaryUserId);
        data.GetProperty(propertyName: "title").GetString().Should().Be(expected: "Heads up");
        data.GetProperty(propertyName: "body").GetString().Should().Be(expected: "Your download finished");
        data.GetProperty(propertyName: "type").GetString().Should().Be(expected: "success");
    }

    [Fact]
    public async Task Send_DefaultsTypeToInfo_WhenTypeOmitted()
    {
        HttpResponseMessage response = await PostSendAsync(
            client: _authed,
            body: new
            {
                user_id = TestAuthHandler.SecondaryUserId,
                title = "Heads up",
                body = "Your download finished",
            }
        );

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        JsonElement data = doc.RootElement.GetProperty(propertyName: "data");
        data.GetProperty(propertyName: "type").GetString().Should().Be(expected: "info");
    }

    [Fact]
    public async Task Send_ReportsConnectedFalse_WhenTargetHasNoLiveConnection()
    {
        HttpResponseMessage response = await PostSendAsync(
            client: _authed,
            body: new
            {
                user_id = TestAuthHandler.SecondaryUserId,
                title = "Heads up",
                body = "Your download finished",
            }
        );

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        JsonElement data = doc.RootElement.GetProperty(propertyName: "data");
        data.GetProperty(propertyName: "connected").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Send_ReportsConnectedTrue_WhenTargetHasLiveConnectionOnVideoHub()
    {
        // Seeds the same ConnectedClients registry ClientMessenger.SendTo reads
        // from — this is the live-connection mechanism the controller's
        // "connected" flag reports on, not a fake stand-in for it.
        ConnectedClients connectedClients = _factory.GetConnectedClients();
        string connectionKey = $"test-live-{Guid.NewGuid()}";
        connectedClients.Clients[key: connectionKey] = new()
        {
            Sub = TestAuthHandler.SecondaryUserId,
            Endpoint = "/videoHub",
        };

        try
        {
            HttpResponseMessage response = await PostSendAsync(
                client: _authed,
                body: new
                {
                    user_id = TestAuthHandler.SecondaryUserId,
                    title = "Heads up",
                    body = "Your download finished",
                }
            );

            string body = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(json: body);

            JsonElement data = doc.RootElement.GetProperty(propertyName: "data");
            data.GetProperty(propertyName: "connected").GetBoolean().Should().BeTrue(because: body);
        }
        finally
        {
            connectedClients.Clients.TryRemove(key: connectionKey, value: out Client? _);
        }
    }
}
