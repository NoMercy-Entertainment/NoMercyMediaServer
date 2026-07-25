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

namespace NoMercy.Tests.Api.Dashboard;

[Trait("Category", "DashboardUsers")]
public class UsersControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;

    public UsersControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
    }

    private static StringContent JsonBody(object obj) =>
        new(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json");

    private Task<HttpResponseMessage> PatchAsync(HttpClient client, string url, object body) =>
        client.PatchAsync(url, JsonBody(body));

    [Fact]
    public async Task GetUsers_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync("/api/v1/dashboard/users");

        response.StatusCode.Should().BeOneOf([HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetUsers_ReturnsOk_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUsers_ReturnsEnvelopeWithDataArray()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/users");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("data", out JsonElement data)
            .Should()
            .BeTrue("users response must have a 'data' property");
        data.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetUsers_DataArray_ContainsSeededOwner()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/users");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        JsonElement data = doc.RootElement.GetProperty("data");
        data.GetArrayLength().Should().BeGreaterThan(0, "at least the seeded owner user exists");

        JsonElement ownerItem = data.EnumerateArray().First();
        ownerItem.TryGetProperty("id", out _).Should().BeTrue("user item must expose 'id'");
        ownerItem.TryGetProperty("name", out _).Should().BeTrue("user item must expose 'name'");
        ownerItem.TryGetProperty("email", out _).Should().BeTrue("user item must expose 'email'");
        ownerItem.TryGetProperty("owner", out _).Should().BeTrue("user item must expose 'owner'");
        ownerItem
            .TryGetProperty("allowed", out _)
            .Should()
            .BeTrue("user item must expose 'allowed'");
    }

    [Fact]
    public async Task GetUserDetail_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            $"/api/v1/dashboard/users/{TestAuthHandler.DefaultUserId}"
        );

        response.StatusCode.Should().BeOneOf([HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetUserDetail_ReturnsNotFound_WhenUserDoesNotExist()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            $"/api/v1/dashboard/users/{Guid.NewGuid()}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUserDetail_ReturnsOkWithUserShape_WhenUserExists()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            $"/api/v1/dashboard/users/{TestAuthHandler.SecondaryUserId}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("data", out JsonElement data)
            .Should()
            .BeTrue("user detail response must have a 'data' property");
        data.ValueKind.Should().Be(JsonValueKind.Object);

        data.TryGetProperty("id", out JsonElement id).Should().BeTrue();
        id.GetString().Should().Be(TestAuthHandler.SecondaryUserId.ToString());

        data.TryGetProperty("name", out _).Should().BeTrue("user detail must expose 'name'");
        data.TryGetProperty("email", out _).Should().BeTrue("user detail must expose 'email'");
        data.TryGetProperty("owner", out _).Should().BeTrue("user detail must expose 'owner'");
        data.TryGetProperty("allowed", out _).Should().BeTrue("user detail must expose 'allowed'");
        data.TryGetProperty("library_user", out _)
            .Should()
            .BeTrue("user detail must expose 'library_user'");
        data.TryGetProperty("libraries", out _)
            .Should()
            .BeTrue("user detail must expose 'libraries'");
    }

    [Fact]
    public async Task GetPermissions_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            "/api/v1/dashboard/users/permissions"
        );

        response.StatusCode.Should().BeOneOf([HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetPermissions_ReturnsOkWithDataArray_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            "/api/v1/dashboard/users/permissions"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("data", out JsonElement data)
            .Should()
            .BeTrue("permissions response must have a 'data' property");
        data.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task DeleteUser_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.DeleteAsync(
            $"/api/v1/dashboard/users/{Guid.NewGuid()}"
        );

        response.StatusCode.Should().BeOneOf([HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task DeleteUser_ReturnsNotFound_WhenUserDoesNotExist()
    {
        HttpResponseMessage response = await _authed.DeleteAsync(
            $"/api/v1/dashboard/users/{Guid.NewGuid()}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteUser_ReturnsUnauthorized_WhenDeletingOwner()
    {
        HttpResponseMessage response = await _authed.DeleteAsync(
            $"/api/v1/dashboard/users/{TestAuthHandler.DefaultUserId}"
        );

        response.StatusCode.Should().BeOneOf([HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task UpdateNotifications_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await PatchAsync(
            _unauthed,
            "/api/v1/dashboard/users/notifications",
            new { }
        );

        response.StatusCode.Should().BeOneOf([HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task UpdateNotifications_ReturnsOk_WhenAuthenticated()
    {
        HttpResponseMessage response = await PatchAsync(
            _authed,
            "/api/v1/dashboard/users/notifications",
            new { }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("status", out JsonElement status).Should().BeTrue();
        status.GetString().Should().Be("success");
    }
}
