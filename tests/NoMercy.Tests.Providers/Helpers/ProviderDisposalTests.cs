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
using NoMercy.Providers.OpenSubtitles.Client;

namespace NoMercy.Tests.Providers.Helpers;

[Collection(name: "HttpClientProvider")]
public class ProviderDisposalTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public ProviderDisposalTests()
    {
        ServiceCollection services = new();
        services.AddHttpClient(name: HttpClientNames.OpenSubtitles);
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
    public void OpenSubtitlesBaseClient_Dispose_DoesNotThrow()
    {
        TestableOpenSubtitlesClient client = new();

        Action action = () => client.Dispose();

        action.Should().NotThrow<NotImplementedException>();
    }

    private class TestableOpenSubtitlesClient : OpenSubtitlesBaseClient { }
}
