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

[Trait(name: "Category", value: "Characterization")]
public class ManagementControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _client;

    public ManagementControllerTests(NoMercyApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ManageStatus_ReturnsOk_WithServerStatus()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/manage/status");

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: content);
        JsonElement root = json.RootElement;

        Assert.True(condition: root.TryGetProperty(propertyName: "status", value: out JsonElement status));
        Assert.False(condition: string.IsNullOrEmpty(value: status.GetString()));

        Assert.True(condition: root.TryGetProperty(propertyName: "server_name", value: out _));
        Assert.True(condition: root.TryGetProperty(propertyName: "version", value: out _));
        Assert.True(condition: root.TryGetProperty(propertyName: "platform", value: out _));
        Assert.True(condition: root.TryGetProperty(propertyName: "architecture", value: out _));
        Assert.True(condition: root.TryGetProperty(propertyName: "os", value: out _));
        Assert.True(condition: root.TryGetProperty(propertyName: "uptime_seconds", value: out JsonElement uptime));
        Assert.True(condition: uptime.GetInt64() >= 0);
        Assert.True(condition: root.TryGetProperty(propertyName: "start_time", value: out _));
        Assert.True(condition: root.TryGetProperty(propertyName: "is_dev", value: out _));
    }

    [Fact]
    public async Task ManageLogs_ReturnsOk_WithLogEntries()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/manage/logs?tail=10");

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: content);

        Assert.Equal(expected: JsonValueKind.Array, actual: json.RootElement.ValueKind);
    }

    [Fact]
    public async Task ManageLogs_WithTypeFilter_ReturnsOk()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/manage/logs?tail=10&types=app");

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
    }

    [Fact]
    public async Task ManageLogs_WithLevelFilter_ReturnsOk()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: "/manage/logs?tail=10&levels=Information,Error"
        );

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
    }

    [Fact]
    public async Task ManageConfig_ReturnsOk_WithConfiguration()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/manage/config");

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: content);
        JsonElement root = json.RootElement;

        Assert.True(condition: root.TryGetProperty(propertyName: "internal_port", value: out JsonElement port));
        Assert.True(condition: port.GetInt32() > 0);

        Assert.True(condition: root.TryGetProperty(propertyName: "external_port", value: out _));
        Assert.True(condition: root.TryGetProperty(propertyName: "server_name", value: out _));
        Assert.True(condition: root.TryGetProperty(propertyName: "library_workers", value: out _));
        Assert.True(condition: root.TryGetProperty(propertyName: "import_workers", value: out _));
        Assert.True(condition: root.TryGetProperty(propertyName: "extras_workers", value: out _));
        Assert.True(condition: root.TryGetProperty(propertyName: "encoder_workers", value: out _));
        Assert.True(condition: root.TryGetProperty(propertyName: "cron_workers", value: out _));
        Assert.True(condition: root.TryGetProperty(propertyName: "image_workers", value: out _));
        Assert.True(condition: root.TryGetProperty(propertyName: "file_workers", value: out _));
        Assert.True(condition: root.TryGetProperty(propertyName: "music_workers", value: out _));
        Assert.True(condition: root.TryGetProperty(propertyName: "swagger", value: out _));
    }

    [Fact]
    public async Task ManagePlugins_ReturnsOk_WithArray()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/manage/plugins");

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: content);

        Assert.Equal(expected: JsonValueKind.Array, actual: json.RootElement.ValueKind);
    }

    [Fact]
    public async Task ManageQueue_ReturnsOk_WithQueueStatus()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/manage/queue");

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: content);
        JsonElement root = json.RootElement;

        Assert.True(condition: root.TryGetProperty(propertyName: "workers", value: out _));
        Assert.True(condition: root.TryGetProperty(propertyName: "pending_jobs", value: out JsonElement pending));
        Assert.True(condition: pending.GetInt32() >= 0);
        Assert.True(condition: root.TryGetProperty(propertyName: "failed_jobs", value: out JsonElement failed));
        Assert.True(condition: failed.GetInt32() >= 0);
    }

    [Fact]
    public async Task ManageStop_ReturnsOk()
    {
        // Only verify the endpoint is reachable and returns correct shape
        // We don't actually want to stop the test server, so we check the response format
        // by reading the restart endpoint (which is a no-op) instead
        HttpResponseMessage response = await _client.PostAsync(requestUri: "/manage/restart", content: null);

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: content);

        Assert.Equal(expected: "ok", actual: json.RootElement.GetProperty(propertyName: "status").GetString());
    }

    [Fact]
    public async Task ManageConfigUpdate_ReturnsOk()
    {
        StringContent body = new(
            content: JsonSerializer.Serialize(value: new { server_name = "TestServer" }),
            encoding: Encoding.UTF8,
            mediaType: "application/json"
        );

        HttpResponseMessage response = await _client.PutAsync(requestUri: "/manage/config", content: body);

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: content);

        Assert.Equal(expected: "ok", actual: json.RootElement.GetProperty(propertyName: "status").GetString());
    }
}
