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
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Media;

[Trait(name: "Category", value: "People")]
public class PeopleControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;

    public PeopleControllerTests(NoMercyApiFactory factory)
    {
        _factory = factory;
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
    }

    [Fact]
    public async Task Index_Authenticated_ReturnsPaginatedEnvelope()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/person?take=10&page=0");

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(json: body);

        Assert.True(
            condition: document.RootElement.TryGetProperty(propertyName: "data", value: out JsonElement data),
            userMessage: "Paginated response must expose a 'data' array"
        );
        Assert.Equal(expected: JsonValueKind.Array, actual: data.ValueKind);
        Assert.True(condition: document.RootElement.TryGetProperty(propertyName: "has_more", value: out _));
    }

    [Fact]
    public async Task Index_Unauthenticated_DoesNotReturnOk()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(requestUri: "/api/v1/person?take=10&page=0");

        Assert.NotEqual(expected: HttpStatusCode.OK, actual: response.StatusCode);
    }

    [Fact]
    public async Task Show_ProviderThrows_ReturnsNotFoundNotServerError()
    {
        Mock<IPersonMetadataProvider> providerMock = new();
        providerMock
            .Setup(expression: p => p.GetPersonAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception: new InvalidOperationException(message: "TMDB provider unreachable"));

        HttpClient client = _factory
            .WithWebHostBuilder(configuration: builder =>
            {
                builder.ConfigureTestServices(servicesConfiguration: services =>
                {
                    services.RemoveAll<IPersonMetadataProvider>();
                    services.AddSingleton(implementationInstance: providerMock.Object);
                });
            })
            .CreateClient()
            .AsAuthenticated();

        HttpResponseMessage response = await client.GetAsync(requestUri: "/api/v1/person/123");

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound, because: body);
        response.StatusCode.Should().NotBe(unexpected: HttpStatusCode.InternalServerError);

        using JsonDocument doc = JsonDocument.Parse(json: body);
        doc.RootElement.TryGetProperty(propertyName: "title", value: out JsonElement title)
            .Should()
            .BeTrue(because: "a provider failure must still return the standard ProblemDetails envelope");
        title.GetString().Should().Be(expected: "Not Found.");
    }
}
