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
using Serilog.Events;

namespace NoMercy.Providers.OpenSubtitles.Client;

public class OpenSubtitlesBaseClient : ExternalApiClient
{
    protected OpenSubtitlesBaseClient() { }

    internal static string? AccessToken { get; set; } = null;

    protected override string HttpClientName => HttpClientNames.OpenSubtitles;
    protected override Uri BaseUrl => new(uriString: "https://api.opensubtitles.org/xml-rpc");

    // OpenSubtitles uses XML-RPC over POST rather than the shared JSON GET flow,
    // so it provides its own request method while reusing the shared Client,
    // Queue and caching from the base.
    protected async Task<T2?> Post<T1, T2>(string url, T1 query, bool? priority = false)
        where T1 : class
        where T2 : class
    {
        string xml = query.ToXml();
        Logger.OpenSubs(message: Redact(payload: xml), level: LogEventLevel.Verbose);

        string newUrl = QueryHelpers.AddQueryString(
            uri: url,
            queryString: new Dictionary<string, string?> { { "query", xml } }
        );

        string response = await RequestQueue.Enqueue(task: () => SendAsync(url: url, xml: xml), url: newUrl, priority: priority);

        await CacheController.Write(url: newUrl, data: response);

        Logger.OpenSubs(message: Redact(payload: response), level: LogEventLevel.Verbose);

        return response.FromXml<T2>();
    }

    // Mask the session token before the payload reaches the log sink. The XML-RPC
    // body embeds OpenSubtitles.AccessToken as a bare string param, so logging it
    // verbatim leaked the token on every call.
    private static string Redact(string payload)
    {
        return string.IsNullOrEmpty(value: AccessToken) || string.IsNullOrEmpty(value: payload)
            ? payload
            : payload.Replace(oldValue: AccessToken, newValue: "***");
    }

    private async Task<string> SendAsync(string url, string xml)
    {
        using StringContent content = new(content: xml, encoding: Encoding.UTF8, mediaType: "text/xml");
        using HttpResponseMessage response = await Client.PostAsync(requestUri: url, content: content);
        // TODO(subtitle-acquisition): handle HTTP 429 — log WARN, return empty, enforce backoff window
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
