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

[Trait("Category", "People")]
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
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/person?take=10&page=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(body);

        Assert.True(
            document.RootElement.TryGetProperty("data", out JsonElement data),
            "Paginated response must expose a 'data' array"
        );
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        Assert.True(document.RootElement.TryGetProperty("has_more", out _));
    }

    [Fact]
    public async Task Index_Unauthenticated_DoesNotReturnOk()
    {
        HttpResponseMessage response = await _unauthed.GetAsync("/api/v1/person?take=10&page=0");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Show_ProviderThrows_ReturnsNotFoundNotServerError()
    {
        Mock<IPersonMetadataProvider> providerMock = new();
        providerMock
            .Setup(p => p.GetPersonAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("TMDB provider unreachable"));

        HttpClient client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IPersonMetadataProvider>();
                    services.AddSingleton(providerMock.Object);
                });
            })
            .CreateClient()
            .AsAuthenticated();

        HttpResponseMessage response = await client.GetAsync("/api/v1/person/123");

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.NotFound, body);
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);

        using JsonDocument doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("title", out JsonElement title)
            .Should()
            .BeTrue("a provider failure must still return the standard ProblemDetails envelope");
        title.GetString().Should().Be("Not Found.");
    }
}
