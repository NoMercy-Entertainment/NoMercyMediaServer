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

[Trait("Category", "DashboardConfiguration")]
public class ConfigurationControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;

    public ConfigurationControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
    }

    private static StringContent JsonBody(object obj) =>
        new(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json");

    private Task<HttpResponseMessage> PatchAsync(HttpClient client, string url, object body) =>
        client.PatchAsync(url, JsonBody(body));

    [Fact]
    public async Task GetConfiguration_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync("/api/v1/dashboard/configuration");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetConfiguration_ReturnsOk_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/configuration");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetConfiguration_ReturnsEnvelopeWithDataObject()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/configuration");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("data", out JsonElement data)
            .Should()
            .BeTrue("configuration response must have a 'data' property");
        data.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task GetConfiguration_DataObject_ContainsWorkerCountFields()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/configuration");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        JsonElement data = doc.RootElement.GetProperty("data");

        data.TryGetProperty("library_workers", out _)
            .Should()
            .BeTrue("clients read 'library_workers' to display queue depth settings");
        data.TryGetProperty("import_workers", out _)
            .Should()
            .BeTrue("clients read 'import_workers'");
        data.TryGetProperty("encoder_workers", out _)
            .Should()
            .BeTrue("clients read 'encoder_workers'");
    }

    [Fact]
    public async Task GetConfiguration_DataObject_ContainsPortFields()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/configuration");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        JsonElement data = doc.RootElement.GetProperty("data");

        data.TryGetProperty("internal_port", out _)
            .Should()
            .BeTrue("clients read 'internal_port' for server connectivity display");
        data.TryGetProperty("external_port", out _).Should().BeTrue("clients read 'external_port'");
    }

    [Fact]
    public async Task PostConfiguration_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PostAsync(
            "/api/v1/dashboard/configuration",
            null
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostConfiguration_ReturnsOk_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.PostAsync(
            "/api/v1/dashboard/configuration",
            null
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchConfiguration_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await PatchAsync(
            _unauthed,
            "/api/v1/dashboard/configuration",
            new { swagger = false }
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PatchConfiguration_ReturnsOkWithStatusSuccess_WhenAuthenticated()
    {
        HttpResponseMessage response = await PatchAsync(
            _authed,
            "/api/v1/dashboard/configuration",
            new { swagger = false }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("status", out JsonElement status)
            .Should()
            .BeTrue("update response must include 'status'");
        status.GetString().Should().Be("success");
    }

    [Fact]
    public async Task PatchConfiguration_ServerName_PersistsRoundTrip()
    {
        string uniqueName = $"TestServer-{Guid.NewGuid():N}";

        HttpResponseMessage patchResponse = await PatchAsync(
            _authed,
            "/api/v1/dashboard/configuration",
            new { name = uniqueName }
        );
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage getResponse = await _authed.GetAsync("/api/v1/dashboard/configuration");
        string body = await getResponse.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        JsonElement data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("name", out JsonElement nameEl).Should().BeTrue();
        nameEl.GetString().Should().Be(uniqueName);
    }

    [Fact]
    public async Task GetLanguages_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            "/api/v1/dashboard/configuration/languages"
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetLanguages_ReturnsOkWithArray_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            "/api/v1/dashboard/configuration/languages"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.ValueKind.Should()
            .Be(JsonValueKind.Array, "languages endpoint returns a bare array");
    }

    [Fact]
    public async Task GetCountries_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            "/api/v1/dashboard/configuration/countries"
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCountries_ReturnsOkWithArray_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            "/api/v1/dashboard/configuration/countries"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.ValueKind.Should()
            .Be(JsonValueKind.Array, "countries endpoint returns a bare array");
    }
}
