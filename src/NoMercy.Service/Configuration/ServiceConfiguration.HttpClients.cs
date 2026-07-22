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

using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Information;
using NoMercy.Providers.Helpers;

namespace NoMercy.Service.Configuration;

public static partial class ServiceConfiguration
{
    private static void ConfigureHttpClients(IServiceCollection services)
    {
        TimeSpan defaultTimeout = TimeSpan.FromMinutes(minutes: 5);

        IServerConfiguration config = services
            .BuildServiceProvider()
            .GetRequiredService<IServerConfiguration>();
        string userAgent = config.UserAgent;

        services.AddHttpClient(
            name: HttpClientNames.Tmdb,
            configureClient: client =>
            {
                client.BaseAddress = new(uriString: "https://api.themoviedb.org/3/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(item: new(mediaType: "application/json"));
                client.DefaultRequestHeaders.Add(name: "User-Agent", value: userAgent);
                client.Timeout = defaultTimeout;
            }
        );

        services.AddHttpClient(
            name: HttpClientNames.TmdbImage,
            configureClient: client =>
            {
                client.BaseAddress = new(uriString: "https://image.tmdb.org/t/p/");
                client.DefaultRequestHeaders.Add(name: "User-Agent", value: userAgent);
                client.DefaultRequestHeaders.Add(name: "Accept", value: "image/*");
                client.Timeout = defaultTimeout;
            }
        );

        services.AddHttpClient(
            name: HttpClientNames.Tvdb,
            configureClient: client =>
            {
                client.BaseAddress = new(uriString: "https://api4.thetvdb.com/v4/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(item: new(mediaType: "application/json"));
                client.DefaultRequestHeaders.Add(name: "User-Agent", value: userAgent);
                client.Timeout = defaultTimeout;
            }
        );

        services.AddHttpClient(
            name: HttpClientNames.TvdbLogin,
            configureClient: client =>
            {
                client.BaseAddress = new(uriString: "https://api4.thetvdb.com/v4/");
                client.DefaultRequestHeaders.Add(name: "Accept", value: "application/json");
                client.DefaultRequestHeaders.Add(name: "User-Agent", value: userAgent);
            }
        );

        services.AddHttpClient(
            name: HttpClientNames.MusicBrainz,
            configureClient: client =>
            {
                client.BaseAddress = new(uriString: "https://musicbrainz.org/ws/2/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(item: new(mediaType: "application/json"));
                // A blank/generic UA (including the literal string "anonymous")
                // lands in MusicBrainz's shared 50 req/s bucket — not a per-client
                // allowance, but one pool split across every anonymous client on
                // the internet, so our own request pacing can't stop it from
                // getting exhausted by other people's traffic. A properly
                // identified UA bypasses UA-based throttling entirely and only
                // has to respect the 1 req/sec-per-IP limit our Queue already
                // paces to (MusicBrainzBaseClient.RequestIntervalMs).
                client.DefaultRequestHeaders.Add(name: "User-Agent", value: userAgent);
            }
        );

        services.AddHttpClient(
            name: HttpClientNames.AcoustId,
            configureClient: client =>
            {
                client.BaseAddress = new(uriString: "https://api.acoustid.org/v2/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(item: new(mediaType: "application/json"));
                client.DefaultRequestHeaders.Add(name: "User-Agent", value: userAgent);
            }
        );

        services.AddHttpClient(
            name: HttpClientNames.OpenSubtitles,
            configureClient: client =>
            {
                client.BaseAddress = new(uriString: "https://api.opensubtitles.org/xml-rpc");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(item: new(mediaType: "text/xml"));
                client.DefaultRequestHeaders.Add(
                    name: "User-Agent",
                    value: ExternalServicesConfig.Current.OpenSubtitlesUserAgent
                );
                client.Timeout = defaultTimeout;
            }
        );

        services.AddHttpClient(
            name: HttpClientNames.OpenSubtitlesDownload,
            configureClient: client =>
            {
                client.DefaultRequestHeaders.Add(
                    name: "User-Agent",
                    value: ExternalServicesConfig.Current.OpenSubtitlesUserAgent
                );
                client.Timeout = defaultTimeout;
            }
        );

        services.AddHttpClient(
            name: HttpClientNames.FanArt,
            configureClient: client =>
            {
                client.BaseAddress = new(uriString: "https://webservice.fanart.tv/v3/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(item: new(mediaType: "application/json"));
                client.DefaultRequestHeaders.Add(name: "User-Agent", value: userAgent);
            }
        );

        services.AddHttpClient(
            name: HttpClientNames.FanArtImage,
            configureClient: client =>
            {
                client.BaseAddress = new(uriString: "https://assets.fanart.tv");
                client.DefaultRequestHeaders.Add(name: "User-Agent", value: userAgent);
                client.DefaultRequestHeaders.Add(name: "Accept", value: "image/*");
            }
        );

        services.AddHttpClient(
            name: HttpClientNames.CoverArt,
            configureClient: client =>
            {
                client.BaseAddress = new(uriString: "https://coverartarchive.org/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(item: new(mediaType: "application/json"));
                client.DefaultRequestHeaders.Add(name: "User-Agent", value: userAgent);
            }
        );

        services.AddHttpClient(
            name: HttpClientNames.CoverArtImage,
            configureClient: client =>
            {
                client.DefaultRequestHeaders.Add(name: "User-Agent", value: userAgent);
                client.DefaultRequestHeaders.Add(name: "Accept", value: "image/*");
            }
        );

        // Lyrics providers sit behind a synchronous /lyrics HTTP request from the
        // client (LyricsResolver), so a hanging call must fail fast rather than
        // inherit HttpClient's 100s default -- LyricsAggregator's own per-stage
        // WaitAsync bound gives up on the caller side well before this, but the
        // request itself still needs to actually stop so the provider's
        // rate-limited Queue slot is freed instead of held for the full 100s.
        TimeSpan lyricsProviderTimeout = TimeSpan.FromSeconds(seconds: 15);

        services.AddHttpClient(
            name: HttpClientNames.Lrclib,
            configureClient: client =>
            {
                client.BaseAddress = new(uriString: "https://lrclib.net/api/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(item: new(mediaType: "application/json"));
                client.DefaultRequestHeaders.Add(name: "User-Agent", value: userAgent);
                client.Timeout = lyricsProviderTimeout;
            }
        );

        services.AddHttpClient(
            name: HttpClientNames.MusixMatch,
            configureClient: client =>
            {
                client.BaseAddress = new(uriString: "https://apic-desktop.musixmatch.com/ws/1.1/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(item: new(mediaType: "application/json"));
                client.DefaultRequestHeaders.Add(name: "User-Agent", value: userAgent);
                client.DefaultRequestHeaders.Add(name: "authority", value: "apic-desktop.musixmatch.com");
                client.DefaultRequestHeaders.Add(name: "cookie", value: "x-mxm-token-guid=");
                client.Timeout = lyricsProviderTimeout;
            }
        );

        services.AddHttpClient(
            name: HttpClientNames.Tadb,
            configureClient: client =>
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(item: new(mediaType: "application/json"));
                client.DefaultRequestHeaders.Add(name: "User-Agent", value: userAgent);
                client.Timeout = defaultTimeout;
            }
        );

        services.AddHttpClient(
            name: HttpClientNames.NoMercyImage,
            configureClient: client =>
            {
                client.BaseAddress = new(uriString: "https://image.nomercy.tv/");
                client.DefaultRequestHeaders.Add(name: "User-Agent", value: userAgent);
                client.DefaultRequestHeaders.Add(name: "Accept", value: "image/*");
                client.Timeout = defaultTimeout;
            }
        );

        services.AddHttpClient(
            name: HttpClientNames.KitsuIo,
            configureClient: client =>
            {
                client.BaseAddress = new(uriString: "https://kitsu.io/api/edge/");
                client.DefaultRequestHeaders.UserAgent.ParseAdd(input: userAgent);
            }
        );

        services.AddHttpClient(
            name: HttpClientNames.General,
            configureClient: client =>
            {
                client.DefaultRequestHeaders.Add(name: "User-Agent", value: userAgent);
                client.Timeout = defaultTimeout;
            }
        );
    }
}
