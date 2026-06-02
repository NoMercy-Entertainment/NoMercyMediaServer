using System.Net;
using System.Text.Json;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Media;

[Trait("Category", "People")]
public class PeopleControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;

    public PeopleControllerTests(NoMercyApiFactory factory)
    {
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
}
