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
using System.Text.Json;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait(name: "Category", value: "Characterization")]
public class HealthControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _client;

    public HealthControllerTests(NoMercyApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk_WithHealthyStatus()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/health");

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: content);

        Assert.Equal(expected: "healthy", actual: json.RootElement.GetProperty(propertyName: "status").GetString());
        Assert.True(condition: json.RootElement.TryGetProperty(propertyName: "timestamp", value: out _));
    }

    [Fact]
    public async Task HealthReady_ReturnsReadinessStatus_WithDatabaseCheck()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/health/ready");

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: content);

        Assert.True(condition: json.RootElement.TryGetProperty(propertyName: "status", value: out JsonElement statusElement));
        string? status = statusElement.GetString();
        Assert.True(
            condition: status == "ready" || status == "not_ready",
            userMessage: $"Expected 'ready' or 'not_ready', got '{status}'"
        );

        Assert.True(condition: json.RootElement.TryGetProperty(propertyName: "database", value: out JsonElement dbElement));
        string? dbStatus = dbElement.GetString();
        Assert.True(
            condition: dbStatus == "ok" || dbStatus == "unavailable",
            userMessage: $"Expected 'ok' or 'unavailable', got '{dbStatus}'"
        );

        Assert.True(condition: json.RootElement.TryGetProperty(propertyName: "server_started", value: out _));
        Assert.True(condition: json.RootElement.TryGetProperty(propertyName: "timestamp", value: out _));
    }

    [Fact]
    public async Task HealthDetailed_ReturnsOk_WithVersionAndEnvironment()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/health/detailed");

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: content);

        Assert.True(condition: json.RootElement.TryGetProperty(propertyName: "status", value: out JsonElement statusElement));
        string? status = statusElement.GetString();
        string[] validStatuses = ["healthy", "degraded", "starting", "unhealthy"];
        Assert.Contains(expected: status, collection: validStatuses);

        Assert.False(condition: string.IsNullOrEmpty(value: json.RootElement.GetProperty(propertyName: "version").GetString()));
        Assert.False(condition: string.IsNullOrEmpty(value: json.RootElement.GetProperty(propertyName: "environment").GetString()));
    }

    [Fact]
    public async Task HealthDetailed_ReturnsComponentStatus()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/health/detailed");

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: content);

        Assert.True(condition: json.RootElement.TryGetProperty(propertyName: "components", value: out JsonElement components));
        Assert.True(condition: components.TryGetProperty(propertyName: "database", value: out _));
        Assert.True(condition: components.TryGetProperty(propertyName: "authentication", value: out _));
        Assert.True(condition: components.TryGetProperty(propertyName: "network", value: out _));
        Assert.True(condition: components.TryGetProperty(propertyName: "registration", value: out _));
    }

    [Fact]
    public async Task HealthDetailed_ReturnsUptimeAndDegradedFlag()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/health/detailed");

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: content);

        Assert.True(
            condition: json.RootElement.TryGetProperty(propertyName: "uptime_seconds", value: out JsonElement uptimeElement)
        );
        long uptime = uptimeElement.GetInt64();
        Assert.True(condition: uptime >= 0, userMessage: $"Uptime should be non-negative, got {uptime}");

        Assert.True(
            condition: json.RootElement.TryGetProperty(propertyName: "is_degraded", value: out JsonElement degradedElement)
        );
        Assert.True(
            condition: degradedElement.ValueKind is JsonValueKind.True or JsonValueKind.False,
            userMessage: "is_degraded should be a boolean"
        );
    }
}
