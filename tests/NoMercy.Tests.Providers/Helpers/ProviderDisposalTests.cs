using Microsoft.Extensions.DependencyInjection;
using NoMercy.Providers.Helpers;
using NoMercy.Providers.OpenSubtitles.Client;

namespace NoMercy.Tests.Providers.Helpers;

[Collection("HttpClientProvider")]
public class ProviderDisposalTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public ProviderDisposalTests()
    {
        ServiceCollection services = new();
        services.AddHttpClient(HttpClientNames.OpenSubtitles);
        services.AddHttpClient(HttpClientNames.General);

        _serviceProvider = services.BuildServiceProvider();
        HttpClientProvider.Initialize(_serviceProvider.GetRequiredService<IHttpClientFactory>());
    }

    public void Dispose()
    {
        HttpClientProvider.Reset();
        _serviceProvider.Dispose();
    }

    [Fact]
    public void OpenSubtitlesBaseClient_Dispose_DoesNotThrow()
    {
        TestableOpenSubtitlesClient client = new();

        Action action = () => client.Dispose();

        action.Should().NotThrow<NotImplementedException>();
    }

    private class TestableOpenSubtitlesClient : OpenSubtitlesBaseClient
    {
    }
}
