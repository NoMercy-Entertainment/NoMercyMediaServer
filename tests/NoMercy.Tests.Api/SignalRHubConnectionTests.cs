using System.Net;
using System.Text.Json;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

public class SignalRHubConnectionTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public SignalRHubConnectionTests(NoMercyApiFactory factory)
    {
        _factory = factory;
    }

    // --- Hub Endpoint Existence Tests ---

    [Theory]
    [InlineData("/videoHub")]
    [InlineData("/musicHub")]
    [InlineData("/castHub")]
    [InlineData("/dashboardHub")]
    [InlineData("/ripperHub")]
    public async Task Hub_NegotiateEndpoint_Exists(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(
            $"{hubPath}/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- Negotiate Response Shape Tests ---

    [Theory]
    [InlineData("/videoHub")]
    [InlineData("/musicHub")]
    [InlineData("/castHub")]
    [InlineData("/dashboardHub")]
    [InlineData("/ripperHub")]
    public async Task Hub_NegotiateResponse_ContainsConnectionId(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(
            $"{hubPath}/negotiate?negotiateVersion=1", null);

        string content = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(content);

        Assert.True(
            doc.RootElement.TryGetProperty("connectionId", out JsonElement connectionId),
            "Negotiate response must contain connectionId");
        Assert.False(string.IsNullOrEmpty(connectionId.GetString()));
    }

    [Theory]
    [InlineData("/videoHub")]
    [InlineData("/musicHub")]
    [InlineData("/castHub")]
    [InlineData("/dashboardHub")]
    [InlineData("/ripperHub")]
    public async Task Hub_NegotiateResponse_ContainsConnectionToken(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(
            $"{hubPath}/negotiate?negotiateVersion=1", null);

        string content = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(content);

        Assert.True(
            doc.RootElement.TryGetProperty("connectionToken", out JsonElement connectionToken),
            "Negotiate response must contain connectionToken");
        Assert.False(string.IsNullOrEmpty(connectionToken.GetString()));
    }

    [Theory]
    [InlineData("/videoHub")]
    [InlineData("/musicHub")]
    [InlineData("/castHub")]
    [InlineData("/dashboardHub")]
    [InlineData("/ripperHub")]
    public async Task Hub_NegotiateResponse_AdvertisesWebSocketsTransport(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(
            $"{hubPath}/negotiate?negotiateVersion=1", null);

        string content = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(content);

        Assert.True(
            doc.RootElement.TryGetProperty("availableTransports", out JsonElement transports),
            "Negotiate response must contain availableTransports");

        Assert.Equal(JsonValueKind.Array, transports.ValueKind);

        List<string> transportNames = [];
        foreach (JsonElement transport in transports.EnumerateArray())
        {
            if (transport.TryGetProperty("transport", out JsonElement name))
                transportNames.Add(name.GetString() ?? "");
        }

        Assert.Contains("WebSockets", transportNames);
    }

    [Theory]
    [InlineData("/videoHub")]
    [InlineData("/musicHub")]
    [InlineData("/castHub")]
    [InlineData("/dashboardHub")]
    [InlineData("/ripperHub")]
    public async Task Hub_NegotiateResponse_OnlyAdvertisesWebSockets(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(
            $"{hubPath}/negotiate?negotiateVersion=1", null);

        string content = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(content);

        JsonElement transports = doc.RootElement.GetProperty("availableTransports");

        List<string> transportNames = [];
        foreach (JsonElement transport in transports.EnumerateArray())
        {
            if (transport.TryGetProperty("transport", out JsonElement name))
                transportNames.Add(name.GetString() ?? "");
        }

        Assert.Single(transportNames);
        Assert.Equal("WebSockets", transportNames[0]);
    }

    [Theory]
    [InlineData("/videoHub")]
    [InlineData("/musicHub")]
    [InlineData("/castHub")]
    [InlineData("/dashboardHub")]
    [InlineData("/ripperHub")]
    public async Task Hub_NegotiateResponse_NegotiateVersion(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(
            $"{hubPath}/negotiate?negotiateVersion=1", null);

        string content = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(content);

        Assert.True(
            doc.RootElement.TryGetProperty("negotiateVersion", out JsonElement version),
            "Negotiate response must contain negotiateVersion");
        Assert.Equal(1, version.GetInt32());
    }

    // --- Authentication Tests ---

    [Theory]
    [InlineData("/videoHub")]
    [InlineData("/musicHub")]
    [InlineData("/castHub")]
    [InlineData("/dashboardHub")]
    [InlineData("/ripperHub")]
    public async Task Hub_NegotiateEndpoint_ReturnsUnauthorized_WhenNotAuthenticated(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsUnauthenticated();

        HttpResponseMessage response = await client.PostAsync(
            $"{hubPath}/negotiate?negotiateVersion=1", null);

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401/403 for {hubPath} but got {(int)response.StatusCode}");
    }

    // --- Multiple Negotiate Calls ---

    [Theory]
    [InlineData("/videoHub")]
    [InlineData("/musicHub")]
    [InlineData("/castHub")]
    [InlineData("/dashboardHub")]
    [InlineData("/ripperHub")]
    public async Task Hub_MultipleNegotiations_ReturnDifferentConnectionIds(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response1 = await client.PostAsync(
            $"{hubPath}/negotiate?negotiateVersion=1", null);
        HttpResponseMessage response2 = await client.PostAsync(
            $"{hubPath}/negotiate?negotiateVersion=1", null);

        string content1 = await response1.Content.ReadAsStringAsync();
        string content2 = await response2.Content.ReadAsStringAsync();

        using JsonDocument doc1 = JsonDocument.Parse(content1);
        using JsonDocument doc2 = JsonDocument.Parse(content2);

        string? id1 = doc1.RootElement.GetProperty("connectionId").GetString();
        string? id2 = doc2.RootElement.GetProperty("connectionId").GetString();

        Assert.NotNull(id1);
        Assert.NotNull(id2);
        Assert.NotEqual(id1, id2);
    }

    // --- Invalid Hub Path ---

    [Fact]
    public async Task NonExistentHub_NegotiateEndpoint_Returns404()
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(
            "/nonExistentHub/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- HTTP Method Tests ---

    [Theory]
    [InlineData("/videoHub")]
    [InlineData("/musicHub")]
    [InlineData("/castHub")]
    [InlineData("/dashboardHub")]
    [InlineData("/ripperHub")]
    public async Task Hub_NegotiateEndpoint_RejectsGetMethod(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.GetAsync(
            $"{hubPath}/negotiate?negotiateVersion=1");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // --- Negotiate Without Version ---

    [Theory]
    [InlineData("/videoHub")]
    [InlineData("/musicHub")]
    [InlineData("/castHub")]
    [InlineData("/dashboardHub")]
    [InlineData("/ripperHub")]
    public async Task Hub_NegotiateEndpoint_WorksWithoutVersionParam(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(
            $"{hubPath}/negotiate", null);

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest,
            $"Expected 200 or 400 for negotiate without version param, got {(int)response.StatusCode}");
    }

    // --- Content-Type Tests ---

    [Theory]
    [InlineData("/videoHub")]
    [InlineData("/musicHub")]
    [InlineData("/castHub")]
    [InlineData("/dashboardHub")]
    [InlineData("/ripperHub")]
    public async Task Hub_NegotiateResponse_HasJsonContentType(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(
            $"{hubPath}/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string? contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.Equal("application/json", contentType);
    }

    // --- Transfer Format Tests ---

    [Theory]
    [InlineData("/videoHub")]
    [InlineData("/musicHub")]
    [InlineData("/castHub")]
    [InlineData("/dashboardHub")]
    [InlineData("/ripperHub")]
    public async Task Hub_NegotiateResponse_WebSocketsTransferFormats(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(
            $"{hubPath}/negotiate?negotiateVersion=1", null);

        string content = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(content);

        JsonElement transports = doc.RootElement.GetProperty("availableTransports");
        JsonElement wsTransport = transports.EnumerateArray().First();

        Assert.True(
            wsTransport.TryGetProperty("transferFormats", out JsonElement formats),
            "WebSockets transport must specify transferFormats");

        List<string> formatNames = [];
        foreach (JsonElement format in formats.EnumerateArray())
            formatNames.Add(format.GetString() ?? "");

        Assert.Contains("Text", formatNames);
        Assert.Contains("Binary", formatNames);
    }
}
