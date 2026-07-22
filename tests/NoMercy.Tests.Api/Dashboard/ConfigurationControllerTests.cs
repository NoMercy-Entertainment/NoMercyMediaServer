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

[Trait(name: "Category", value: "DashboardConfiguration")]
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
        new(content: JsonSerializer.Serialize(value: obj), encoding: Encoding.UTF8, mediaType: "application/json");

    private Task<HttpResponseMessage> PatchAsync(HttpClient client, string url, object body) =>
        client.PatchAsync(requestUri: url, content: JsonBody(obj: body));

    [Fact]
    public async Task GetConfiguration_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(requestUri: "/api/v1/dashboard/configuration");

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetConfiguration_ReturnsOk_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/dashboard/configuration");

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetConfiguration_ReturnsEnvelopeWithDataObject()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/dashboard/configuration");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.TryGetProperty(propertyName: "data", value: out JsonElement data)
            .Should()
            .BeTrue(because: "configuration response must have a 'data' property");
        data.ValueKind.Should().Be(expected: JsonValueKind.Object);
    }

    [Fact]
    public async Task GetConfiguration_DataObject_ContainsWorkerCountFields()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/dashboard/configuration");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        JsonElement data = doc.RootElement.GetProperty(propertyName: "data");

        data.TryGetProperty(propertyName: "library_workers", value: out _)
            .Should()
            .BeTrue(because: "clients read 'library_workers' to display queue depth settings");
        data.TryGetProperty(propertyName: "import_workers", value: out _)
            .Should()
            .BeTrue(because: "clients read 'import_workers'");
        data.TryGetProperty(propertyName: "encoder_workers", value: out _)
            .Should()
            .BeTrue(because: "clients read 'encoder_workers'");
    }

    [Fact]
    public async Task GetConfiguration_DataObject_ContainsPortFields()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/dashboard/configuration");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        JsonElement data = doc.RootElement.GetProperty(propertyName: "data");

        data.TryGetProperty(propertyName: "internal_port", value: out _)
            .Should()
            .BeTrue(because: "clients read 'internal_port' for server connectivity display");
        data.TryGetProperty(propertyName: "external_port", value: out _).Should().BeTrue(because: "clients read 'external_port'");
    }

    [Fact]
    public async Task PostConfiguration_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PostAsync(
            requestUri: "/api/v1/dashboard/configuration",
            content: null
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task PostConfiguration_ReturnsOk_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.PostAsync(
            requestUri: "/api/v1/dashboard/configuration",
            content: null
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchConfiguration_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await PatchAsync(
            client: _unauthed,
            url: "/api/v1/dashboard/configuration",
            body: new { swagger = false }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task PatchConfiguration_ReturnsOkWithStatusSuccess_WhenAuthenticated()
    {
        HttpResponseMessage response = await PatchAsync(
            client: _authed,
            url: "/api/v1/dashboard/configuration",
            body: new { swagger = false }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.TryGetProperty(propertyName: "status", value: out JsonElement status)
            .Should()
            .BeTrue(because: "update response must include 'status'");
        status.GetString().Should().Be(expected: "success");
    }

    [Fact]
    public async Task PatchConfiguration_ServerName_PersistsRoundTrip()
    {
        string uniqueName = $"TestServer-{Guid.NewGuid():N}";

        HttpResponseMessage patchResponse = await PatchAsync(
            client: _authed,
            url: "/api/v1/dashboard/configuration",
            body: new { name = uniqueName }
        );
        patchResponse.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        HttpResponseMessage getResponse = await _authed.GetAsync(requestUri: "/api/v1/dashboard/configuration");
        string body = await getResponse.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        JsonElement data = doc.RootElement.GetProperty(propertyName: "data");
        data.TryGetProperty(propertyName: "name", value: out JsonElement nameEl).Should().BeTrue();
        nameEl.GetString().Should().Be(expected: uniqueName);
    }

    [Fact]
    public async Task GetLanguages_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            requestUri: "/api/v1/dashboard/configuration/languages"
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetLanguages_ReturnsOkWithArray_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            requestUri: "/api/v1/dashboard/configuration/languages"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.ValueKind.Should()
            .Be(expected: JsonValueKind.Array, because: "languages endpoint returns a bare array");
    }

    [Fact]
    public async Task GetCountries_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            requestUri: "/api/v1/dashboard/configuration/countries"
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetCountries_ReturnsOkWithArray_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            requestUri: "/api/v1/dashboard/configuration/countries"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.ValueKind.Should()
            .Be(expected: JsonValueKind.Array, because: "countries endpoint returns a bare array");
    }
}
