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

using Microsoft.Extensions.DependencyInjection;
using NoMercy.Providers.Helpers;

namespace NoMercy.Tests.Providers.Helpers;

[Collection(name: "HttpClientProvider")]
public class HttpClientProviderTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public HttpClientProviderTests()
    {
        ServiceCollection services = new();
        services.AddHttpClient(
            name: HttpClientNames.Tmdb,
            configureClient: client =>
            {
                client.BaseAddress = new(uriString: "https://api.themoviedb.org/3/");
            }
        );
        services.AddHttpClient(
            name: HttpClientNames.MusicBrainz,
            configureClient: client =>
            {
                client.BaseAddress = new(uriString: "https://musicbrainz.org/ws/2/");
                client.DefaultRequestHeaders.Add(name: "User-Agent", value: "Anonymous");
            }
        );
        services.AddHttpClient(
            name: HttpClientNames.FanArt,
            configureClient: client =>
            {
                client.BaseAddress = new(uriString: "http://webservice.fanart.tv/v3/");
            }
        );
        services.AddHttpClient(name: HttpClientNames.General);

        _serviceProvider = services.BuildServiceProvider();
        HttpClientProvider.Initialize(factory: _serviceProvider.GetRequiredService<IHttpClientFactory>());
    }

    public void Dispose()
    {
        HttpClientProvider.Reset();
        _serviceProvider.Dispose();
    }

    [Fact]
    public void CreateClient_WithNamedClient_ReturnsConfiguredHttpClient()
    {
        HttpClient client = HttpClientProvider.CreateClient(name: HttpClientNames.Tmdb);

        client.Should().NotBeNull();
        client.BaseAddress.Should().Be(expected: new Uri(uriString: "https://api.themoviedb.org/3/"));
    }

    [Fact]
    public void CreateClient_DifferentNames_ReturnDifferentConfigurations()
    {
        HttpClient tmdbClient = HttpClientProvider.CreateClient(name: HttpClientNames.Tmdb);
        HttpClient musicBrainzClient = HttpClientProvider.CreateClient(name: HttpClientNames.MusicBrainz);

        tmdbClient.BaseAddress.Should().Be(expected: new Uri(uriString: "https://api.themoviedb.org/3/"));
        musicBrainzClient.BaseAddress.Should().Be(expected: new Uri(uriString: "https://musicbrainz.org/ws/2/"));
    }

    [Fact]
    public void CreateClient_CalledMultipleTimes_DoesNotThrow()
    {
        List<HttpClient> clients = [];
        Action action = () =>
        {
            for (int i = 0; i < 100; i++)
            {
                clients.Add(item: HttpClientProvider.CreateClient(name: HttpClientNames.Tmdb));
            }
        };

        action.Should().NotThrow();
        clients.Should().HaveCount(expected: 100);
    }

    [Fact]
    public void CreateClient_WithGeneralName_ReturnsClient()
    {
        HttpClient client = HttpClientProvider.CreateClient(name: HttpClientNames.General);

        client.Should().NotBeNull();
    }

    [Fact]
    public void CreateClient_WithUnknownName_ReturnsFallbackClient()
    {
        HttpClient client = HttpClientProvider.CreateClient(name: "UnknownProvider");

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
            HttpClientNames.General,
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
            HttpClientNames.General,
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
                HttpClient client = HttpClientProvider.CreateClient(name: HttpClientNames.Tmdb);
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
            tasks.Add(
                item: Task.Run(action: () =>
                {
                    HttpClient client = HttpClientProvider.CreateClient(name: HttpClientNames.FanArt);
                    client.Should().NotBeNull();
                    client.BaseAddress.Should().Be(expected: new Uri(uriString: "http://webservice.fanart.tv/v3/"));
                })
            );
        }

        Action action = () => Task.WaitAll(tasks: tasks.ToArray());
        action.Should().NotThrow();
    }
}
