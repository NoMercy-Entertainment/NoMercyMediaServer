using System.Net;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Media;

[Trait("Category", "Specials")]
public class SpecialControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;

    public SpecialControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
    }

    [Fact]
    public async Task Available_UnknownSpecial_ReturnsNotFound()
    {
        string id = Ulid.NewUlid().ToString();
        HttpResponseMessage response = await _authed.GetAsync($"/api/v1/specials/{id}/available");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Available_Unauthenticated_DoesNotReturnOk()
    {
        string id = Ulid.NewUlid().ToString();
        HttpResponseMessage response = await _unauthed.GetAsync($"/api/v1/specials/{id}/available");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
