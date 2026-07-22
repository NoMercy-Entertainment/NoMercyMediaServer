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
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using NoMercy.Api.Middleware;
using NoMercy.Encoder.Errors;
using Xunit;

namespace NoMercy.Tests.Api.Middleware;

[Trait(name: "Category", value: "Unit")]
public class EncoderRuntimeExceptionMiddlewareTests
{
    /// <summary>
    /// Builds a minimal test pipeline: EncoderRuntimeExceptionMiddleware → stub terminal
    /// that throws the supplied exception (or calls next for pass-through tests).
    /// </summary>
    private static HttpClient BuildClient(RequestDelegate terminal)
    {
        IHost host = new HostBuilder()
            .ConfigureWebHost(configure: web =>
            {
                web.UseTestServer();
                web.Configure(configureApp: app =>
                {
                    app.UseMiddleware<EncoderRuntimeExceptionMiddleware>();
                    app.Run(handler: terminal);
                });
            })
            .Build();

        host.Start();
        return host.GetTestClient();
    }

    [Fact]
    public async Task GpuCapacityExhausted_returns_409_with_correct_id()
    {
        HttpClient client = BuildClient(terminal: _ =>
            throw RuntimeErrors.GpuCapacityExhausted(gpu: "RTX 4090", sessions: 3)
        );

        HttpResponseMessage response = await client.GetAsync(requestUri: "/");

        Assert.Equal(expected: HttpStatusCode.Conflict, actual: response.StatusCode);
        Assert.Equal(expected: "application/json", actual: response.Content.Headers.ContentType?.MediaType);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: content);

        Assert.Equal(expected: "gpu_capacity_exhausted", actual: json.RootElement.GetProperty(propertyName: "id").GetString());
    }

    [Fact]
    public async Task SourceNotAccessible_returns_404_with_correct_id()
    {
        HttpClient client = BuildClient(terminal: _ => throw RuntimeErrors.SourceNotAccessible(path: "/x.mkv"));

        HttpResponseMessage response = await client.GetAsync(requestUri: "/");

        Assert.Equal(expected: HttpStatusCode.NotFound, actual: response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: content);

        Assert.Equal(expected: "source.not_accessible", actual: json.RootElement.GetProperty(propertyName: "id").GetString());
    }

    [Fact]
    public async Task Unrelated_exception_is_not_caught_by_middleware()
    {
        // The middleware must NOT catch arbitrary exceptions — they propagate
        // upward. TestServer re-throws them at the HttpClient boundary, so
        // we assert the exception escapes (rather than being swallowed and
        // serialised as an EncoderErrorShape).
        HttpClient client = BuildClient(terminal: _ =>
            throw new InvalidOperationException(message: "something unrelated")
        );

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(testCode: () =>
            client.GetAsync(requestUri: "/")
        );

        Assert.Equal(expected: "something unrelated", actual: thrown.Message);
    }
}
