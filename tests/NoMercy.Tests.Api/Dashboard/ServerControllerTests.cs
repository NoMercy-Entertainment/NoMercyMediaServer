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

[Trait("Category", "DashboardServer")]
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
        new(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json");

    private Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string url, object body) =>
        client.PostAsync(url, JsonBody(body));

    [Fact]
    public async Task GetServer_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync("/api/v1/dashboard/server");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetServer_ReturnsOk_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/server");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetServerInfo_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync("/api/v1/dashboard/server/info");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetServerInfo_ReturnsOk_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/server/info");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetServerInfo_ReturnsEnvelopeWithStatusAndData()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/server/info");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("status", out JsonElement status)
            .Should()
            .BeTrue("server info response must have 'status'");
        status.GetString().Should().Be("ok");

        doc.RootElement.TryGetProperty("data", out JsonElement data)
            .Should()
            .BeTrue("server info response must have 'data'");
        data.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task GetServerInfo_DataObject_ContainsRequiredClientFields()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/server/info");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        JsonElement data = doc.RootElement.GetProperty("data");

        data.TryGetProperty("server", out _).Should().BeTrue("clients read 'server' for display");
        data.TryGetProperty("version", out _)
            .Should()
            .BeTrue("clients read 'version' for update checks");
        data.TryGetProperty("os", out _).Should().BeTrue("clients read 'os'");
        data.TryGetProperty("arch", out _).Should().BeTrue("clients read 'arch'");
        data.TryGetProperty("setup_complete", out JsonElement setupComplete)
            .Should()
            .BeTrue("clients read 'setup_complete' to decide whether to run setup wizard");
        setupComplete.ValueKind.Should().BeOneOf(JsonValueKind.True, JsonValueKind.False);
    }

    [Fact]
    public async Task GetServerSetup_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync("/api/v1/dashboard/server/setup");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetServerSetup_ReturnsOkWithSetupCompleteFlag_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/server/setup");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("data", out JsonElement data)
            .Should()
            .BeTrue("setup response must have 'data'");
        data.TryGetProperty("setup_complete", out JsonElement setupComplete)
            .Should()
            .BeTrue("data must have 'setup_complete' boolean for setup-wizard gate");
        setupComplete.ValueKind.Should().BeOneOf(JsonValueKind.True, JsonValueKind.False);
    }

    [Fact]
    public async Task GetResources_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            "/api/v1/dashboard/server/resources"
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetResources_DoesNotReturn500_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/server/resources");

        ((int)response.StatusCode)
            .Should()
            .NotBe(
                500,
                "resource monitor must not panic; allowed to return 422 if monitor is unavailable"
            );
    }

    [Fact]
    public async Task GetServerPaths_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync("/api/v1/dashboard/server/paths");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetServerPaths_ReturnsOkWithArray_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/server/paths");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array, "paths returns a bare array");
        doc.RootElement.GetArrayLength()
            .Should()
            .BeGreaterThan(0, "always returns at least Cache, Logs, Transcodes, Configs");
    }

    [Fact]
    public async Task GetServerPaths_ArrayItems_ContainKeyAndValueFields()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/server/paths");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        JsonElement first = doc.RootElement.EnumerateArray().First();
        first.TryGetProperty("key", out _).Should().BeTrue("paths items must have 'key'");
        first.TryGetProperty("value", out _).Should().BeTrue("paths items must have 'value'");
    }

    [Fact]
    public async Task CheckForUpdate_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            "/api/v1/dashboard/server/update/check"
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CheckForUpdate_ReturnsOkWithUpdateAvailableField_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            "/api/v1/dashboard/server/update/check"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("updateAvailable", out JsonElement updateAvailable)
            .Should()
            .BeTrue("clients poll 'updateAvailable' to show the update banner");
        updateAvailable.ValueKind.Should().BeOneOf(JsonValueKind.True, JsonValueKind.False);
    }

    [Fact]
    public async Task StartServer_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PostAsync(
            "/api/v1/dashboard/server/start",
            null
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StartServer_ReturnsNotImplemented_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.PostAsync(
            "/api/v1/dashboard/server/start",
            null
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task UpdateWorkers_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PatchAsync(
            "/api/v1/dashboard/server/workers/library/2",
            null
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateWorkers_ReturnsBadRequest_WhenCountIsNegative()
    {
        HttpResponseMessage response = await _authed.PatchAsync(
            "/api/v1/dashboard/server/workers/library/-1",
            null
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ChangeIp_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await PostJsonAsync(
            _unauthed,
            "/api/v1/dashboard/server/changeIp",
            new { ip = "10.0.0.1" }
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangeIp_ReturnsBadRequest_WhenIpIsEmpty()
    {
        HttpResponseMessage response = await PostJsonAsync(
            _authed,
            "/api/v1/dashboard/server/changeIp",
            new { ip = "" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangeIp_ReturnsOkWithStatusOk_WhenValidIpProvided()
    {
        HttpResponseMessage response = await PostJsonAsync(
            _authed,
            "/api/v1/dashboard/server/changeIp",
            new { ip = "192.168.1.100" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("status", out JsonElement status).Should().BeTrue();
        status.GetString().Should().Be("ok");
    }
}
