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

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NoMercy.Tests.Common;

namespace NoMercy.Tests.MediaProcessing;

/// <summary>
/// Serves canned TMDB responses so MovieManager's real TMDB lookups never touch
/// the network during tests. Registered via HttpClientProvider so the churn suite
/// is deterministic regardless of TMDB availability or credentials. Only the URLs
/// MovieManager.Add (movie/1771) and StoreCompanies (company/420, company/7505)
/// request are mocked; anything else returns 404.
/// </summary>
internal sealed class TmdbMockHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) =>
        new(new TmdbMockHandler()) { BaseAddress = new Uri("https://api.themoviedb.org/3/") };

    private sealed class TmdbMockHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            string path = request.RequestUri?.AbsolutePath ?? string.Empty;

            string? body = true switch
            {
                _ when path.Contains("/movie/1771") =>
                    MovieResponseMocks.MovieAppendsResponseJson(),
                _ when path.Contains("/company/420") => Company(420, "Marvel Studios"),
                _ when path.Contains("/company/7505") => Company(7505, "Marvel Entertainment"),
                _ => null,
            };

            HttpResponseMessage response = body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                };

            return Task.FromResult(response);
        }

        private static string Company(int id, string name) =>
            $$"""
                {"id":{{id}},"name":"{{name}}","logo_path":"","origin_country":"US","headquarters":"","homepage":""}
                """;
    }
}
