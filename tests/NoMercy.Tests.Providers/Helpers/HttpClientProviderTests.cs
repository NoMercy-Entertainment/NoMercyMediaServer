using Microsoft.Extensions.DependencyInjection;
using NoMercy.Providers.Helpers;

namespace NoMercy.Tests.Providers.Helpers;

[Collection("HttpClientProvider")]
public class HttpClientProviderTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public HttpClientProviderTests()
    {
        ServiceCollection services = new();
        services.AddHttpClient(HttpClientNames.Tmdb, client =>
        {
            client.BaseAddress = new("https://api.themoviedb.org/3/");
            client.DefaultRequestHeaders.Add("User-Agent", "test-agent");
        });
        services.AddHttpClient(HttpClientNames.MusicBrainz, client =>
        {
            client.BaseAddress = new("https://musicbrainz.org/ws/2/");
        });
        services.AddHttpClient(HttpClientNames.FanArt, client =>
        {
            client.BaseAddress = new("http://webservice.fanart.tv/v3/");
        });
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
    public void CreateClient_WithNamedClient_ReturnsConfiguredHttpClient()
    {
        HttpClient client = HttpClientProvider.CreateClient(HttpClientNames.Tmdb);

        client.Should().NotBeNull();
        client.BaseAddress.Should().Be(new Uri("https://api.themoviedb.org/3/"));
    }

    [Fact]
    public void CreateClient_DifferentNames_ReturnDifferentConfigurations()
    {
        HttpClient tmdbClient = HttpClientProvider.CreateClient(HttpClientNames.Tmdb);
        HttpClient musicBrainzClient = HttpClientProvider.CreateClient(HttpClientNames.MusicBrainz);

        tmdbClient.BaseAddress.Should().Be(new Uri("https://api.themoviedb.org/3/"));
        musicBrainzClient.BaseAddress.Should().Be(new Uri("https://musicbrainz.org/ws/2/"));
    }

    [Fact]
    public void CreateClient_CalledMultipleTimes_DoesNotThrow()
    {
        List<HttpClient> clients = [];
        Action action = () =>
        {
            for (int i = 0; i < 100; i++)
            {
                clients.Add(HttpClientProvider.CreateClient(HttpClientNames.Tmdb));
            }
        };

        action.Should().NotThrow();
        clients.Should().HaveCount(100);
    }

    [Fact]
    public void CreateClient_WithGeneralName_ReturnsClient()
    {
        HttpClient client = HttpClientProvider.CreateClient(HttpClientNames.General);

        client.Should().NotBeNull();
    }

    [Fact]
    public void CreateClient_WithUnknownName_ReturnsFallbackClient()
    {
        HttpClient client = HttpClientProvider.CreateClient("UnknownProvider");

        client.Should().NotBeNull();
    }

    [Fact]
    public void HttpClientNames_AllConstantsAreUnique()
    {
        string[] names =
        [
            HttpClientNames.Tmdb,
            HttpClientNames.TmdbImage,
            HttpClientNames.Tvdb,
            HttpClientNames.TvdbLogin,
            HttpClientNames.MusicBrainz,
            HttpClientNames.AcoustId,
            HttpClientNames.OpenSubtitles,
            HttpClientNames.FanArt,
            HttpClientNames.FanArtImage,
            HttpClientNames.CoverArt,
            HttpClientNames.CoverArtImage,
            HttpClientNames.Lrclib,
            HttpClientNames.MusixMatch,
            HttpClientNames.Tadb,
            HttpClientNames.NoMercyImage,
            HttpClientNames.KitsuIo,
            HttpClientNames.General
        ];

        names.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void HttpClientNames_AllConstantsAreNotEmpty()
    {
        string[] names =
        [
            HttpClientNames.Tmdb,
            HttpClientNames.TmdbImage,
            HttpClientNames.Tvdb,
            HttpClientNames.TvdbLogin,
            HttpClientNames.MusicBrainz,
            HttpClientNames.AcoustId,
            HttpClientNames.OpenSubtitles,
            HttpClientNames.FanArt,
            HttpClientNames.FanArtImage,
            HttpClientNames.CoverArt,
            HttpClientNames.CoverArtImage,
            HttpClientNames.Lrclib,
            HttpClientNames.MusixMatch,
            HttpClientNames.Tadb,
            HttpClientNames.NoMercyImage,
            HttpClientNames.KitsuIo,
            HttpClientNames.General
        ];

        foreach (string name in names)
        {
            name.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void CreateClient_SequentialCalls_DoNotExhaustSockets()
    {
        Action action = () =>
        {
            for (int i = 0; i < 200; i++)
            {
                HttpClient client = HttpClientProvider.CreateClient(HttpClientNames.Tmdb);
                client.Should().NotBeNull();
            }
        };

        action.Should().NotThrow();
    }

    [Fact]
    public void CreateClient_ConcurrentCalls_AreThreadSafe()
    {
        List<Task> tasks = [];

        for (int i = 0; i < 50; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                HttpClient client = HttpClientProvider.CreateClient(HttpClientNames.FanArt);
                client.Should().NotBeNull();
                client.BaseAddress.Should().Be(new Uri("http://webservice.fanart.tv/v3/"));
            }));
        }

        Action action = () => Task.WaitAll(tasks.ToArray());
        action.Should().NotThrow();
    }
}
