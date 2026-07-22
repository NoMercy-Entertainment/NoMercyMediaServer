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

[Trait(name: "Category", value: "DashboardUsers")]
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
        new(content: JsonSerializer.Serialize(value: obj), encoding: Encoding.UTF8, mediaType: "application/json");

    private Task<HttpResponseMessage> PatchAsync(HttpClient client, string url, object body) =>
        client.PatchAsync(requestUri: url, content: JsonBody(obj: body));

    [Fact]
    public async Task GetUsers_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(requestUri: "/api/v1/dashboard/users");

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetUsers_ReturnsOk_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/dashboard/users");

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUsers_ReturnsEnvelopeWithDataArray()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/dashboard/users");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.TryGetProperty(propertyName: "data", value: out JsonElement data)
            .Should()
            .BeTrue(because: "users response must have a 'data' property");
        data.ValueKind.Should().Be(expected: JsonValueKind.Array);
    }

    [Fact]
    public async Task GetUsers_DataArray_ContainsSeededOwner()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/dashboard/users");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        JsonElement data = doc.RootElement.GetProperty(propertyName: "data");
        data.GetArrayLength().Should().BeGreaterThan(expected: 0, because: "at least the seeded owner user exists");

        JsonElement ownerItem = data.EnumerateArray().First();
        ownerItem.TryGetProperty(propertyName: "id", value: out _).Should().BeTrue(because: "user item must expose 'id'");
        ownerItem.TryGetProperty(propertyName: "name", value: out _).Should().BeTrue(because: "user item must expose 'name'");
        ownerItem.TryGetProperty(propertyName: "email", value: out _).Should().BeTrue(because: "user item must expose 'email'");
        ownerItem.TryGetProperty(propertyName: "owner", value: out _).Should().BeTrue(because: "user item must expose 'owner'");
        ownerItem
            .TryGetProperty(propertyName: "allowed", value: out _)
            .Should()
            .BeTrue(because: "user item must expose 'allowed'");
    }

    [Fact]
    public async Task GetUserDetail_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            requestUri: $"/api/v1/dashboard/users/{TestAuthHandler.DefaultUserId}"
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetUserDetail_ReturnsNotFound_WhenUserDoesNotExist()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            requestUri: $"/api/v1/dashboard/users/{Guid.NewGuid()}"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUserDetail_ReturnsOkWithUserShape_WhenUserExists()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            requestUri: $"/api/v1/dashboard/users/{TestAuthHandler.SecondaryUserId}"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.TryGetProperty(propertyName: "data", value: out JsonElement data)
            .Should()
            .BeTrue(because: "user detail response must have a 'data' property");
        data.ValueKind.Should().Be(expected: JsonValueKind.Object);

        data.TryGetProperty(propertyName: "id", value: out JsonElement id).Should().BeTrue();
        id.GetString().Should().Be(expected: TestAuthHandler.SecondaryUserId.ToString());

        data.TryGetProperty(propertyName: "name", value: out _).Should().BeTrue(because: "user detail must expose 'name'");
        data.TryGetProperty(propertyName: "email", value: out _).Should().BeTrue(because: "user detail must expose 'email'");
        data.TryGetProperty(propertyName: "owner", value: out _).Should().BeTrue(because: "user detail must expose 'owner'");
        data.TryGetProperty(propertyName: "allowed", value: out _).Should().BeTrue(because: "user detail must expose 'allowed'");
        data.TryGetProperty(propertyName: "library_user", value: out _)
            .Should()
            .BeTrue(because: "user detail must expose 'library_user'");
        data.TryGetProperty(propertyName: "libraries", value: out _)
            .Should()
            .BeTrue(because: "user detail must expose 'libraries'");
    }

    [Fact]
    public async Task GetPermissions_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            requestUri: "/api/v1/dashboard/users/permissions"
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetPermissions_ReturnsOkWithDataArray_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            requestUri: "/api/v1/dashboard/users/permissions"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.TryGetProperty(propertyName: "data", value: out JsonElement data)
            .Should()
            .BeTrue(because: "permissions response must have a 'data' property");
        data.ValueKind.Should().Be(expected: JsonValueKind.Array);
    }

    [Fact]
    public async Task DeleteUser_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.DeleteAsync(
            requestUri: $"/api/v1/dashboard/users/{Guid.NewGuid()}"
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task DeleteUser_ReturnsNotFound_WhenUserDoesNotExist()
    {
        HttpResponseMessage response = await _authed.DeleteAsync(
            requestUri: $"/api/v1/dashboard/users/{Guid.NewGuid()}"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteUser_ReturnsUnauthorized_WhenDeletingOwner()
    {
        HttpResponseMessage response = await _authed.DeleteAsync(
            requestUri: $"/api/v1/dashboard/users/{TestAuthHandler.DefaultUserId}"
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task UpdateNotifications_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await PatchAsync(
            client: _unauthed,
            url: "/api/v1/dashboard/users/notifications",
            body: new { }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task UpdateNotifications_ReturnsOk_WhenAuthenticated()
    {
        HttpResponseMessage response = await PatchAsync(
            client: _authed,
            url: "/api/v1/dashboard/users/notifications",
            body: new { }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.TryGetProperty(propertyName: "status", value: out JsonElement status).Should().BeTrue();
        status.GetString().Should().Be(expected: "success");
    }
}
