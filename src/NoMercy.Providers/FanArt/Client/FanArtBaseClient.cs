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

using NoMercy.Providers.Abstractions;
using NoMercy.Providers.Helpers;
using NoMercy.Setup.Server;

namespace NoMercy.Providers.FanArt.Client;

public class FanArtBaseClient : ExternalApiClient
{
    protected FanArtBaseClient() { }

    protected FanArtBaseClient(Guid id)
        : base(id: id) { }

    protected override string HttpClientName => HttpClientNames.FanArt;
    protected override Uri BaseUrl => new(uriString: "https://webservice.fanart.tv/v3/");
    protected override int ConcurrentRequests => 3;
    protected override int RequestIntervalMs => 1000;

    protected override void ConfigureClient(HttpClient client)
    {
        client.DefaultRequestHeaders.Add(name: "api-key", value: ApiKeyStore.Current.FanArtApiKey);
        if (!string.IsNullOrEmpty(value: ApiKeyStore.Current.FanArtClientKey))
            client.DefaultRequestHeaders.Add(name: "client-key", value: ApiKeyStore.Current.FanArtClientKey);
    }
}
