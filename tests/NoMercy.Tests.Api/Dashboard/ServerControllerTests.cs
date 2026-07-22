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

[Trait(name: "Category", value: "DashboardServer")]
public class ServerControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;

    public ServerControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
    }

    private static StringContent JsonBody(object obj) =>
        new(content: JsonSerializer.Serialize(value: obj), encoding: Encoding.UTF8, mediaType: "application/json");

    private Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string url, object body) =>
        client.PostAsync(requestUri: url, content: JsonBody(obj: body));

    [Fact]
    public async Task GetServer_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(requestUri: "/api/v1/dashboard/server");

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetServer_ReturnsOk_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/dashboard/server");

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetServerInfo_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(requestUri: "/api/v1/dashboard/server/info");

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetServerInfo_ReturnsOk_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/dashboard/server/info");

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetServerInfo_ReturnsEnvelopeWithStatusAndData()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/dashboard/server/info");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.TryGetProperty(propertyName: "status", value: out JsonElement status)
            .Should()
            .BeTrue(because: "server info response must have 'status'");
        status.GetString().Should().Be(expected: "ok");

        doc.RootElement.TryGetProperty(propertyName: "data", value: out JsonElement data)
            .Should()
            .BeTrue(because: "server info response must have 'data'");
        data.ValueKind.Should().Be(expected: JsonValueKind.Object);
    }

    [Fact]
    public async Task GetServerInfo_DataObject_ContainsRequiredClientFields()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/dashboard/server/info");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        JsonElement data = doc.RootElement.GetProperty(propertyName: "data");

        data.TryGetProperty(propertyName: "server", value: out _).Should().BeTrue(because: "clients read 'server' for display");
        data.TryGetProperty(propertyName: "version", value: out _)
            .Should()
            .BeTrue(because: "clients read 'version' for update checks");
        data.TryGetProperty(propertyName: "os", value: out _).Should().BeTrue(because: "clients read 'os'");
        data.TryGetProperty(propertyName: "arch", value: out _).Should().BeTrue(because: "clients read 'arch'");
        data.TryGetProperty(propertyName: "setup_complete", value: out JsonElement setupComplete)
            .Should()
            .BeTrue(because: "clients read 'setup_complete' to decide whether to run setup wizard");
        setupComplete.ValueKind.Should().BeOneOf(validValues: [JsonValueKind.True, JsonValueKind.False]);
    }

    [Fact]
    public async Task GetServerSetup_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(requestUri: "/api/v1/dashboard/server/setup");

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetServerSetup_ReturnsOkWithSetupCompleteFlag_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/dashboard/server/setup");

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.TryGetProperty(propertyName: "data", value: out JsonElement data)
            .Should()
            .BeTrue(because: "setup response must have 'data'");
        data.TryGetProperty(propertyName: "setup_complete", value: out JsonElement setupComplete)
            .Should()
            .BeTrue(because: "data must have 'setup_complete' boolean for setup-wizard gate");
        setupComplete.ValueKind.Should().BeOneOf(validValues: [JsonValueKind.True, JsonValueKind.False]);
    }

    [Fact]
    public async Task GetResources_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            requestUri: "/api/v1/dashboard/server/resources"
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetResources_DoesNotReturn500_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/dashboard/server/resources");

        ((int)response.StatusCode)
            .Should()
            .NotBe(
                unexpected: 500,
                because: "resource monitor must not panic; allowed to return 422 if monitor is unavailable"
            );
    }

    [Fact]
    public async Task GetServerPaths_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(requestUri: "/api/v1/dashboard/server/paths");

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetServerPaths_ReturnsOkWithArray_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/dashboard/server/paths");

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.ValueKind.Should().Be(expected: JsonValueKind.Array, because: "paths returns a bare array");
        doc.RootElement.GetArrayLength()
            .Should()
            .BeGreaterThan(expected: 0, because: "always returns at least Cache, Logs, Transcodes, Configs");
    }

    [Fact]
    public async Task GetServerPaths_ArrayItems_ContainKeyAndValueFields()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/dashboard/server/paths");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        JsonElement first = doc.RootElement.EnumerateArray().First();
        first.TryGetProperty(propertyName: "key", value: out _).Should().BeTrue(because: "paths items must have 'key'");
        first.TryGetProperty(propertyName: "value", value: out _).Should().BeTrue(because: "paths items must have 'value'");
    }

    [Fact]
    public async Task CheckForUpdate_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            requestUri: "/api/v1/dashboard/server/update/check"
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task CheckForUpdate_ReturnsOkWithUpdateAvailableField_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            requestUri: "/api/v1/dashboard/server/update/check"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.TryGetProperty(propertyName: "updateAvailable", value: out JsonElement updateAvailable)
            .Should()
            .BeTrue(because: "clients poll 'updateAvailable' to show the update banner");
        updateAvailable.ValueKind.Should().BeOneOf(validValues: [JsonValueKind.True, JsonValueKind.False]);
    }

    [Fact]
    public async Task StartServer_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PostAsync(
            requestUri: "/api/v1/dashboard/server/start",
            content: null
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task StartServer_ReturnsNotImplemented_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.PostAsync(
            requestUri: "/api/v1/dashboard/server/start",
            content: null
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task UpdateWorkers_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PatchAsync(
            requestUri: "/api/v1/dashboard/server/workers/library/2",
            content: null
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task UpdateWorkers_ReturnsBadRequest_WhenCountIsNegative()
    {
        HttpResponseMessage response = await _authed.PatchAsync(
            requestUri: "/api/v1/dashboard/server/workers/library/-1",
            content: null
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.BadRequest, HttpStatusCode.NotFound]);
    }

    [Fact]
    public async Task ChangeIp_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await PostJsonAsync(
            client: _unauthed,
            url: "/api/v1/dashboard/server/changeIp",
            body: new { ip = "10.0.0.1" }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task ChangeIp_ReturnsBadRequest_WhenIpIsEmpty()
    {
        HttpResponseMessage response = await PostJsonAsync(
            client: _authed,
            url: "/api/v1/dashboard/server/changeIp",
            body: new { ip = "" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangeIp_ReturnsOkWithStatusOk_WhenValidIpProvided()
    {
        HttpResponseMessage response = await PostJsonAsync(
            client: _authed,
            url: "/api/v1/dashboard/server/changeIp",
            body: new { ip = "192.168.1.100" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.TryGetProperty(propertyName: "status", value: out JsonElement status).Should().BeTrue();
        status.GetString().Should().Be(expected: "ok");
    }
}
