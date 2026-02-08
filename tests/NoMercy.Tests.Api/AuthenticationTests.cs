using System.Net;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait("Category", "Characterization")]
public class AuthenticationTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public AuthenticationTests(NoMercyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousEndpoint_ReturnsOk_WithoutAuth()
    {
        HttpClient client = _factory.CreateClient().AsUnauthenticated();

        HttpResponseMessage response = await client.GetAsync("/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedEndpoint_ReturnsOk_WhenAuthenticated()
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.GetAsync("/api/v1/setup/permissions");

        string content = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {content}");
        Assert.Contains("owner", content);
        Assert.Contains("allowed", content);
    }

    [Fact]
    public async Task AuthenticatedEndpoint_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        HttpClient client = _factory.CreateClient().AsUnauthenticated();

        HttpResponseMessage response = await client.GetAsync("/api/v1/setup/permissions");

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401 or 403, got {(int)response.StatusCode}");
    }
}
