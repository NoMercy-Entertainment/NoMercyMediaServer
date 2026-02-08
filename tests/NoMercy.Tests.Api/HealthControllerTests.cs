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
    public async Task HealthDetailed_ReturnsOk_WithVersionAndEnvironment()
    {
        HttpResponseMessage response = await _client.GetAsync("/health/detailed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(content);

        Assert.Equal("healthy", json.RootElement.GetProperty("status").GetString());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("version").GetString()));
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("environment").GetString()));
    }
}
