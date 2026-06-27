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

using System.Net;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Abstractions;
using NoMercy.Providers.Helpers;
using Serilog.Events;

namespace NoMercy.Providers.MusicBrainz.Client;

public class MusicBrainzBaseClient : ExternalApiClient
{
    protected MusicBrainzBaseClient() { }

    protected MusicBrainzBaseClient(Guid id)
        : base(id) { }

    protected override string HttpClientName => HttpClientNames.MusicBrainz;
    protected override Uri BaseUrl => new("https://musicbrainz.org/ws/2/");

    // MusicBrainz asks clients to stay at roughly one request per second.
    protected override int RequestIntervalMs => 1100;

    // MusicBrainz rejects anonymous user-agents with a 403. The factory-
    // registered UA only takes effect once HttpClientProvider is initialized;
    // early seed callers fall back to a bare client, so set it defensively.
    protected override void ConfigureClient(HttpClient client)
    {
        if (client.DefaultRequestHeaders.UserAgent.Count == 0)
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                ExternalServicesConfig.Current.UserAgent
            );
    }

    protected override void LogRequest(string url) =>
        Logger.MusicBrainz(url, LogEventLevel.Verbose);

    // MusicBrainz returns 404 for unknown MBIDs — treat as "no result".
    // Transient 429/503 are retried by the shared Queue, not here.
    protected override bool ShouldSoftFail(HttpStatusCode? status) =>
        status is HttpStatusCode.NotFound;
}
