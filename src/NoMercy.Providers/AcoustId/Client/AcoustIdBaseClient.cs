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
using Microsoft.AspNetCore.WebUtilities;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Abstractions;
using NoMercy.Providers.AcoustId.Models;
using NoMercy.Providers.Helpers;
using Serilog.Events;

namespace NoMercy.Providers.AcoustId.Client;

public class AcoustIdBaseClient : ExternalApiClient
{
    protected AcoustIdBaseClient() { }

    protected AcoustIdBaseClient(Guid id)
        : base(id: id) { }

    protected override string HttpClientName => HttpClientNames.AcoustId;
    protected override Uri BaseUrl => new(uriString: "https://api.acoustid.org/v2/");
    protected override int ConcurrentRequests => 3;

    protected override void LogRequest(string url) => Logger.AcoustId(message: url, level: LogEventLevel.Verbose);

    // AcoustId-specific fetch: only returns a fingerprint that actually carries
    // recordings (a 200 with no recordings counts as "no result"). This needs
    // the AcoustIdFingerprint constraint, so it cannot reuse the base Get.
    // Transient failures are retried by the shared Queue, not here.
    protected async Task<T?> GetFingerprint<T>(
        string url,
        Dictionary<string, string?>? query = null,
        bool? priority = false
    )
        where T : AcoustIdFingerprint
    {
        query ??= new();
        string newUrl = QueryHelpers.AddQueryString(uri: url, queryString: query);

        (bool found, T? cached) = await CacheController.ReadAsync<T>(url: newUrl);
        if (found && HasRecordings(data: cached))
            return cached;

        LogRequest(url: BaseUrl + newUrl);

        try
        {
            string response = await RequestQueue.Enqueue(
                task: () => Client.GetStringAsync(requestUri: newUrl),
                url: newUrl,
                priority: priority
            );
            await CacheController.Write(url: newUrl, data: response);
            T? data = response.FromJson<T>();
            return HasRecordings(data: data) ? data : null;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static bool HasRecordings<T>(T? data)
        where T : AcoustIdFingerprint
    {
        return data?.Results.Length > 0
            && data.Results.Any(predicate: fpResult =>
                fpResult.Recordings is not null
                && fpResult.Recordings.Any(predicate: recording => recording?.Title != null)
            );
    }
}
