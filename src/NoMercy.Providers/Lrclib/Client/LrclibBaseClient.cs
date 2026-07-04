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
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Abstractions;
using NoMercy.Providers.Helpers;
using Serilog.Events;

namespace NoMercy.Providers.Lrclib.Client;

public class LrclibBaseClient : ExternalApiClient
{
    protected LrclibBaseClient() { }

    protected LrclibBaseClient(Guid id)
        : base(id) { }

    protected override string HttpClientName => HttpClientNames.Lrclib;
    protected override Uri BaseUrl => new("https://lrclib.net/api/");

    protected override void LogRequest(string url) => Logger.Lrclib(url, LogEventLevel.Verbose);

    // Lrclib returns 404 for tracks with no lyrics — treat only that as "no
    // result". Transient failures are retried by the shared Queue, not here.
    protected override bool ShouldSoftFail(HttpStatusCode? status) =>
        status is HttpStatusCode.NotFound;
}
