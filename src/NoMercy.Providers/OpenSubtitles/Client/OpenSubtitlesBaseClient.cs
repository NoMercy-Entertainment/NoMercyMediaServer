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

using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Abstractions;
using NoMercy.Providers.Helpers;

namespace NoMercy.Providers.OpenSubtitles.Client;

public class OpenSubtitlesBaseClient : ExternalApiClient
{
    protected OpenSubtitlesBaseClient() { }

    internal static string? AccessToken { get; set; } = null;

    protected override string HttpClientName => HttpClientNames.OpenSubtitles;
    protected override Uri BaseUrl => new("https://api.opensubtitles.org/xml-rpc");

    // OpenSubtitles uses XML-RPC over POST rather than the shared JSON GET flow,
    // so it provides its own request method while reusing the shared Client,
    // Queue and caching from the base.
    protected async Task<T2?> Post<T1, T2>(string url, T1 query, bool? priority = false)
        where T1 : class
        where T2 : class
    {
        string xml = query.ToXml();
        Logger.OpenSubs(xml);

        string newUrl = QueryHelpers.AddQueryString(
            url,
            new Dictionary<string, string?> { { "query", xml } }
        );

        string response = await RequestQueue.Enqueue(() => SendAsync(url, xml), newUrl, priority);

        await CacheController.Write(newUrl, response);

        Logger.OpenSubs(response);

        return response.FromXml<T2>();
    }

    private async Task<string> SendAsync(string url, string xml)
    {
        using StringContent content = new(xml, Encoding.UTF8, "text/xml");
        using HttpResponseMessage response = await Client.PostAsync(url, content);
        // TODO(subtitle-acquisition): handle HTTP 429 — log WARN, return empty, enforce backoff window
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
