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

namespace NoMercy.Tests.Providers.Infrastructure;

/// <summary>
/// Reusable request-mocking harness for the provider clients under
/// <c>NoMercy.Providers</c> (TMDB, TVDB, MusicBrainz, ...). Every
/// <c>ExternalApiClient</c> subclass resolves its <see cref="HttpClient"/>
/// through the process-wide <see cref="HttpClientProvider"/> static rather than
/// constructor injection, so the only way to intercept its traffic in a test is
/// to stand up a real <see cref="IHttpClientFactory"/> — pointed at a single
/// scriptable handler — and install it through that static for the lifetime of
/// the test.
///
/// One harness instance per test (never a shared fixture): the underlying
/// <see cref="HttpClientProvider"/> and provider-family <c>Queue</c> instances
/// are both process-wide statics, so two tests racing a shared harness would
/// either overwrite each other's scripted routes or interleave requests onto
/// the same rate limiter. Test classes built on this harness must therefore
/// stay inside the ad-hoc <c>[Collection("HttpClientProvider")]</c> xUnit
/// collection so they run serially against each other.
///
/// Response caching (<c>CacheController</c>) is keyed by URL and shared
/// per-process on disk, so every scripted request must target a unique path —
/// see <see cref="Unique"/> — or an earlier test's cached response silently
/// answers a later one.
/// </summary>
public abstract class ProviderHttpHarness : IDisposable
{
    public ScriptableHttpMessageHandler Handler { get; } = new();

    private readonly ServiceProvider _serviceProvider;

    protected ProviderHttpHarness(params string[] httpClientNames)
    {
        ServiceCollection services = new();
        foreach (string name in httpClientNames)
            services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => Handler);

        _serviceProvider = services.BuildServiceProvider();
        HttpClientProvider.Initialize(_serviceProvider.GetRequiredService<IHttpClientFactory>());
    }

    /// <summary>
    /// A synthetic path segment unique to the calling test, so the shared
    /// on-disk <c>CacheController</c> cache can never answer this request with
    /// another test's cached body.
    /// </summary>
    protected static string Unique(string prefix) => $"{prefix}/{Guid.NewGuid():N}";

    public virtual void Dispose()
    {
        HttpClientProvider.Reset();
        _serviceProvider.Dispose();
        GC.SuppressFinalize(this);
    }
}
