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
public class SignalRHubConnectionTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public SignalRHubConnectionTests(NoMercyApiFactory factory)
    {
        _factory = factory;
    }

    // --- Hub Endpoint Existence Tests ---

    [Theory]
    [InlineData(data: "/videoHub")]
    [InlineData(data: "/musicHub")]
    [InlineData(data: "/castHub")]
    [InlineData(data: "/dashboardHub")]
    [InlineData(data: "/ripperHub")]
    public async Task Hub_NegotiateEndpoint_Exists(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(
            requestUri: $"{hubPath}/negotiate?negotiateVersion=1",
            content: null
        );

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
    }

    // --- Negotiate Response Shape Tests ---

    [Theory]
    [InlineData(data: "/videoHub")]
    [InlineData(data: "/musicHub")]
    [InlineData(data: "/castHub")]
    [InlineData(data: "/dashboardHub")]
    [InlineData(data: "/ripperHub")]
    public async Task Hub_NegotiateResponse_ContainsConnectionId(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(
            requestUri: $"{hubPath}/negotiate?negotiateVersion=1",
            content: null
        );

        string content = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: content);

        Assert.True(
            condition: doc.RootElement.TryGetProperty(propertyName: "connectionId", value: out JsonElement connectionId),
            userMessage: "Negotiate response must contain connectionId"
        );
        Assert.False(condition: string.IsNullOrEmpty(value: connectionId.GetString()));
    }

    [Theory]
    [InlineData(data: "/videoHub")]
    [InlineData(data: "/musicHub")]
    [InlineData(data: "/castHub")]
    [InlineData(data: "/dashboardHub")]
    [InlineData(data: "/ripperHub")]
    public async Task Hub_NegotiateResponse_ContainsConnectionToken(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(
            requestUri: $"{hubPath}/negotiate?negotiateVersion=1",
            content: null
        );

        string content = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: content);

        Assert.True(
            condition: doc.RootElement.TryGetProperty(propertyName: "connectionToken", value: out JsonElement connectionToken),
            userMessage: "Negotiate response must contain connectionToken"
        );
        Assert.False(condition: string.IsNullOrEmpty(value: connectionToken.GetString()));
    }

    [Theory]
    [InlineData(data: "/videoHub")]
    [InlineData(data: "/musicHub")]
    [InlineData(data: "/castHub")]
    [InlineData(data: "/dashboardHub")]
    [InlineData(data: "/ripperHub")]
    public async Task Hub_NegotiateResponse_AdvertisesWebSocketsTransport(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(
            requestUri: $"{hubPath}/negotiate?negotiateVersion=1",
            content: null
        );

        string content = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: content);

        Assert.True(
            condition: doc.RootElement.TryGetProperty(propertyName: "availableTransports", value: out JsonElement transports),
            userMessage: "Negotiate response must contain availableTransports"
        );

        Assert.Equal(expected: JsonValueKind.Array, actual: transports.ValueKind);

        List<string> transportNames = [];
        foreach (JsonElement transport in transports.EnumerateArray())
        {
            if (transport.TryGetProperty(propertyName: "transport", value: out JsonElement name))
                transportNames.Add(item: name.GetString() ?? "");
        }

        Assert.Contains(expected: "WebSockets", collection: transportNames);
    }

    [Theory]
    [InlineData(data: "/videoHub")]
    [InlineData(data: "/musicHub")]
    [InlineData(data: "/castHub")]
    [InlineData(data: "/dashboardHub")]
    [InlineData(data: "/ripperHub")]
    public async Task Hub_NegotiateResponse_OnlyAdvertisesWebSockets(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(
            requestUri: $"{hubPath}/negotiate?negotiateVersion=1",
            content: null
        );

        string content = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: content);

        JsonElement transports = doc.RootElement.GetProperty(propertyName: "availableTransports");

        List<string> transportNames = [];
        foreach (JsonElement transport in transports.EnumerateArray())
        {
            if (transport.TryGetProperty(propertyName: "transport", value: out JsonElement name))
                transportNames.Add(item: name.GetString() ?? "");
        }

        Assert.Single(collection: transportNames);
        Assert.Equal(expected: "WebSockets", actual: transportNames[index: 0]);
    }

    [Theory]
    [InlineData(data: "/videoHub")]
    [InlineData(data: "/musicHub")]
    [InlineData(data: "/castHub")]
    [InlineData(data: "/dashboardHub")]
    [InlineData(data: "/ripperHub")]
    public async Task Hub_NegotiateResponse_NegotiateVersion(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(
            requestUri: $"{hubPath}/negotiate?negotiateVersion=1",
            content: null
        );

        string content = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: content);

        Assert.True(
            condition: doc.RootElement.TryGetProperty(propertyName: "negotiateVersion", value: out JsonElement version),
            userMessage: "Negotiate response must contain negotiateVersion"
        );
        Assert.Equal(expected: 1, actual: version.GetInt32());
    }

    // --- Authentication Tests ---

    [Theory]
    [InlineData(data: "/videoHub")]
    [InlineData(data: "/musicHub")]
    [InlineData(data: "/castHub")]
    [InlineData(data: "/dashboardHub")]
    [InlineData(data: "/ripperHub")]
    public async Task Hub_NegotiateEndpoint_ReturnsUnauthorized_WhenNotAuthenticated(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsUnauthenticated();

        HttpResponseMessage response = await client.PostAsync(
            requestUri: $"{hubPath}/negotiate?negotiateVersion=1",
            content: null
        );

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            userMessage: $"Expected 401/403 for {hubPath} but got {(int)response.StatusCode}"
        );
    }

    // --- Multiple Negotiate Calls ---

    [Theory]
    [InlineData(data: "/videoHub")]
    [InlineData(data: "/musicHub")]
    [InlineData(data: "/castHub")]
    [InlineData(data: "/dashboardHub")]
    [InlineData(data: "/ripperHub")]
    public async Task Hub_MultipleNegotiations_ReturnDifferentConnectionIds(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response1 = await client.PostAsync(
            requestUri: $"{hubPath}/negotiate?negotiateVersion=1",
            content: null
        );
        HttpResponseMessage response2 = await client.PostAsync(
            requestUri: $"{hubPath}/negotiate?negotiateVersion=1",
            content: null
        );

        string content1 = await response1.Content.ReadAsStringAsync();
        string content2 = await response2.Content.ReadAsStringAsync();

        using JsonDocument doc1 = JsonDocument.Parse(json: content1);
        using JsonDocument doc2 = JsonDocument.Parse(json: content2);

        string? id1 = doc1.RootElement.GetProperty(propertyName: "connectionId").GetString();
        string? id2 = doc2.RootElement.GetProperty(propertyName: "connectionId").GetString();

        Assert.NotNull(@object: id1);
        Assert.NotNull(@object: id2);
        Assert.NotEqual(expected: id1, actual: id2);
    }

    // --- Invalid Hub Path ---

    [Fact]
    public async Task NonExistentHub_NegotiateEndpoint_Returns404()
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(
            requestUri: "/nonExistentHub/negotiate?negotiateVersion=1",
            content: null
        );

        Assert.Equal(expected: HttpStatusCode.NotFound, actual: response.StatusCode);
    }

    // --- HTTP Method Tests ---

    [Theory]
    [InlineData(data: "/videoHub")]
    [InlineData(data: "/musicHub")]
    [InlineData(data: "/castHub")]
    [InlineData(data: "/dashboardHub")]
    [InlineData(data: "/ripperHub")]
    public async Task Hub_NegotiateEndpoint_RejectsGetMethod(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.GetAsync(
            requestUri: $"{hubPath}/negotiate?negotiateVersion=1"
        );

        Assert.NotEqual(expected: HttpStatusCode.OK, actual: response.StatusCode);
    }

    // --- Negotiate Without Version ---

    [Theory]
    [InlineData(data: "/videoHub")]
    [InlineData(data: "/musicHub")]
    [InlineData(data: "/castHub")]
    [InlineData(data: "/dashboardHub")]
    [InlineData(data: "/ripperHub")]
    public async Task Hub_NegotiateEndpoint_WorksWithoutVersionParam(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(requestUri: $"{hubPath}/negotiate", content: null);

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest,
            userMessage: $"Expected 200 or 400 for negotiate without version param, got {(int)response.StatusCode}"
        );
    }

    // --- Content-Type Tests ---

    [Theory]
    [InlineData(data: "/videoHub")]
    [InlineData(data: "/musicHub")]
    [InlineData(data: "/castHub")]
    [InlineData(data: "/dashboardHub")]
    [InlineData(data: "/ripperHub")]
    public async Task Hub_NegotiateResponse_HasJsonContentType(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(
            requestUri: $"{hubPath}/negotiate?negotiateVersion=1",
            content: null
        );

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        string? contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.Equal(expected: "application/json", actual: contentType);
    }

    // --- Transfer Format Tests ---

    [Theory]
    [InlineData(data: "/videoHub")]
    [InlineData(data: "/musicHub")]
    [InlineData(data: "/castHub")]
    [InlineData(data: "/dashboardHub")]
    [InlineData(data: "/ripperHub")]
    public async Task Hub_NegotiateResponse_WebSocketsTransferFormats(string hubPath)
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(
            requestUri: $"{hubPath}/negotiate?negotiateVersion=1",
            content: null
        );

        string content = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: content);

        JsonElement transports = doc.RootElement.GetProperty(propertyName: "availableTransports");
        JsonElement wsTransport = transports.EnumerateArray().First();

        Assert.True(
            condition: wsTransport.TryGetProperty(propertyName: "transferFormats", value: out JsonElement formats),
            userMessage: "WebSockets transport must specify transferFormats"
        );

        List<string> formatNames = [];
        foreach (JsonElement format in formats.EnumerateArray())
            formatNames.Add(item: format.GetString() ?? "");

        Assert.Contains(expected: "Text", collection: formatNames);
        Assert.Contains(expected: "Binary", collection: formatNames);
    }
}
