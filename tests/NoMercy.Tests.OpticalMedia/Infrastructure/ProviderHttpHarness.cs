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

namespace NoMercy.Tests.OpticalMedia.Infrastructure;

/// <summary>
/// Request-mocking harness for the identification pipeline's provider calls
/// (TMDB via <see cref="VideoDiscIdentifier"/>, MusicBrainz + Cover Art
/// Archive via <see cref="AudioCdIdentifier"/>). Every provider client
/// resolves its <see cref="HttpClient"/> through the process-wide
/// <see cref="HttpClientProvider"/> static rather than constructor injection,
/// so the only way to intercept its traffic in a test is to stand up a real
/// <see cref="IHttpClientFactory"/> — pointed at a single scriptable handler —
/// and install it through that static for the lifetime of the test.
///
/// One harness instance per test (never shared): <see cref="HttpClientProvider"/>
/// is a process-wide static, so two tests racing a shared harness would
/// overwrite each other's scripted routes. Tests built on this harness must
/// run inside <c>[Collection("HttpClientProvider")]</c> so xUnit serializes
/// them against each other and against NoMercy.Tests.Providers if ever run in
/// the same host.
/// </summary>
public abstract class ProviderHttpHarness : IDisposable
{
    public ScriptableHttpMessageHandler Handler { get; } = new();

    private readonly ServiceProvider _serviceProvider;

    protected ProviderHttpHarness(params string[] httpClientNames)
    {
        ServiceCollection services = new();
        foreach (string name in httpClientNames)
            services.AddHttpClient(name: name).ConfigurePrimaryHttpMessageHandler(configureHandler: () => Handler);

        _serviceProvider = services.BuildServiceProvider();
        HttpClientProvider.Initialize(factory: _serviceProvider.GetRequiredService<IHttpClientFactory>());
    }

    /// <summary>
    /// A synthetic path segment unique to the calling test, so the shared
    /// on-disk response cache (uninitialized in this test process, but
    /// defensive regardless) can never answer this request with another
    /// test's response.
    /// </summary>
    protected static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    public virtual void Dispose()
    {
        HttpClientProvider.Reset();
        _serviceProvider.Dispose();
        GC.SuppressFinalize(obj: this);
    }
}
