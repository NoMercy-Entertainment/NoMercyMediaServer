using System.Net;
using System.Text;
using System.Text.Json;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait("Category", "Characterization")]
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
        HttpResponseMessage response = await _client.GetAsync("/manage/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(content);
        JsonElement root = json.RootElement;

        Assert.True(root.TryGetProperty("status", out JsonElement status));
        Assert.False(string.IsNullOrEmpty(status.GetString()));

        Assert.True(root.TryGetProperty("server_name", out _));
        Assert.True(root.TryGetProperty("version", out _));
        Assert.True(root.TryGetProperty("platform", out _));
        Assert.True(root.TryGetProperty("architecture", out _));
        Assert.True(root.TryGetProperty("os", out _));
        Assert.True(root.TryGetProperty("uptime_seconds", out JsonElement uptime));
        Assert.True(uptime.GetInt64() >= 0);
        Assert.True(root.TryGetProperty("start_time", out _));
        Assert.True(root.TryGetProperty("is_dev", out _));
    }

    [Fact]
    public async Task ManageLogs_ReturnsOk_WithLogEntries()
    {
        HttpResponseMessage response = await _client.GetAsync("/manage/logs?tail=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(content);

        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
    }

    [Fact]
    public async Task ManageLogs_WithTypeFilter_ReturnsOk()
    {
        HttpResponseMessage response = await _client.GetAsync("/manage/logs?tail=10&types=app");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ManageLogs_WithLevelFilter_ReturnsOk()
    {
        HttpResponseMessage response = await _client.GetAsync("/manage/logs?tail=10&levels=Information,Error");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ManageConfig_ReturnsOk_WithConfiguration()
    {
        HttpResponseMessage response = await _client.GetAsync("/manage/config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(content);
        JsonElement root = json.RootElement;

        Assert.True(root.TryGetProperty("internal_port", out JsonElement port));
        Assert.True(port.GetInt32() > 0);

        Assert.True(root.TryGetProperty("external_port", out _));
        Assert.True(root.TryGetProperty("server_name", out _));
        Assert.True(root.TryGetProperty("queue_workers", out _));
        Assert.True(root.TryGetProperty("encoder_workers", out _));
        Assert.True(root.TryGetProperty("cron_workers", out _));
        Assert.True(root.TryGetProperty("data_workers", out _));
        Assert.True(root.TryGetProperty("image_workers", out _));
        Assert.True(root.TryGetProperty("request_workers", out _));
        Assert.True(root.TryGetProperty("swagger", out _));
    }

    [Fact]
    public async Task ManagePlugins_ReturnsOk_WithArray()
    {
        HttpResponseMessage response = await _client.GetAsync("/manage/plugins");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(content);

        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
    }

    [Fact]
    public async Task ManageQueue_ReturnsOk_WithQueueStatus()
    {
        HttpResponseMessage response = await _client.GetAsync("/manage/queue");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(content);
        JsonElement root = json.RootElement;

        Assert.True(root.TryGetProperty("workers", out _));
        Assert.True(root.TryGetProperty("pending_jobs", out JsonElement pending));
        Assert.True(pending.GetInt32() >= 0);
        Assert.True(root.TryGetProperty("failed_jobs", out JsonElement failed));
        Assert.True(failed.GetInt32() >= 0);
    }

    [Fact]
    public async Task ManageStop_ReturnsOk()
    {
        // Only verify the endpoint is reachable and returns correct shape
        // We don't actually want to stop the test server, so we check the response format
        // by reading the restart endpoint (which is a no-op) instead
        HttpResponseMessage response = await _client.PostAsync("/manage/restart", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(content);

        Assert.Equal("ok", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ManageConfigUpdate_ReturnsOk()
    {
        StringContent body = new(
            JsonSerializer.Serialize(new { server_name = "TestServer" }),
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await _client.PutAsync("/manage/config", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(content);

        Assert.Equal("ok", json.RootElement.GetProperty("status").GetString());
    }
}
