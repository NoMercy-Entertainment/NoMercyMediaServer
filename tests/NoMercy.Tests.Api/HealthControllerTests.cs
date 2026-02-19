using System.Net;
using System.Text.Json;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait("Category", "Characterization")]
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
        HttpResponseMessage response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(content);

        Assert.Equal("healthy", json.RootElement.GetProperty("status").GetString());
        Assert.True(json.RootElement.TryGetProperty("timestamp", out _));
    }

    [Fact]
    public async Task HealthReady_ReturnsReadinessStatus_WithDatabaseCheck()
    {
        HttpResponseMessage response = await _client.GetAsync("/health/ready");

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(content);

        Assert.True(json.RootElement.TryGetProperty("status", out JsonElement statusElement));
        string? status = statusElement.GetString();
        Assert.True(status == "ready" || status == "not_ready",
            $"Expected 'ready' or 'not_ready', got '{status}'");

        Assert.True(json.RootElement.TryGetProperty("database", out JsonElement dbElement));
        string? dbStatus = dbElement.GetString();
        Assert.True(dbStatus == "ok" || dbStatus == "unavailable",
            $"Expected 'ok' or 'unavailable', got '{dbStatus}'");

        Assert.True(json.RootElement.TryGetProperty("server_started", out _));
        Assert.True(json.RootElement.TryGetProperty("timestamp", out _));
    }

    [Fact]
    public async Task HealthDetailed_ReturnsOk_WithVersionAndEnvironment()
    {
        HttpResponseMessage response = await _client.GetAsync("/health/detailed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(content);

        Assert.True(json.RootElement.TryGetProperty("status", out JsonElement statusElement));
        string? status = statusElement.GetString();
        string[] validStatuses = ["healthy", "degraded", "starting", "unhealthy"];
        Assert.Contains(status, validStatuses);

        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("version").GetString()));
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("environment").GetString()));
    }

    [Fact]
    public async Task HealthDetailed_ReturnsComponentStatus()
    {
        HttpResponseMessage response = await _client.GetAsync("/health/detailed");

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(content);

        Assert.True(json.RootElement.TryGetProperty("components", out JsonElement components));
        Assert.True(components.TryGetProperty("database", out _));
        Assert.True(components.TryGetProperty("authentication", out _));
        Assert.True(components.TryGetProperty("network", out _));
        Assert.True(components.TryGetProperty("registration", out _));
    }

    [Fact]
    public async Task HealthDetailed_ReturnsUptimeAndDegradedFlag()
    {
        HttpResponseMessage response = await _client.GetAsync("/health/detailed");

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(content);

        Assert.True(json.RootElement.TryGetProperty("uptime_seconds", out JsonElement uptimeElement));
        long uptime = uptimeElement.GetInt64();
        Assert.True(uptime >= 0, $"Uptime should be non-negative, got {uptime}");

        Assert.True(json.RootElement.TryGetProperty("is_degraded", out JsonElement degradedElement));
        Assert.True(degradedElement.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "is_degraded should be a boolean");
    }
}
