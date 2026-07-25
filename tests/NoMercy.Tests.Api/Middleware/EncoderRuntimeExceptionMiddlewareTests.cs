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

[Trait("Category", "Unit")]
public class EncoderRuntimeExceptionMiddlewareTests
{
    /// <summary>
    /// Builds a minimal test pipeline: EncoderRuntimeExceptionMiddleware → stub terminal
    /// that throws the supplied exception (or calls next for pass-through tests).
    /// </summary>
    private static HttpClient BuildClient(RequestDelegate terminal)
    {
        IHost host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.Configure(app =>
                {
                    app.UseMiddleware<EncoderRuntimeExceptionMiddleware>();
                    app.Run(terminal);
                });
            })
            .Build();

        host.Start();
        return host.GetTestClient();
    }

    [Fact]
    public async Task GpuCapacityExhausted_returns_409_with_correct_id()
    {
        HttpClient client = BuildClient(_ =>
            throw RuntimeErrors.GpuCapacityExhausted("RTX 4090", 3)
        );

        HttpResponseMessage response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(content);

        Assert.Equal("gpu_capacity_exhausted", json.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public async Task SourceNotAccessible_returns_404_with_correct_id()
    {
        HttpClient client = BuildClient(_ => throw RuntimeErrors.SourceNotAccessible("/x.mkv"));

        HttpResponseMessage response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(content);

        Assert.Equal("source.not_accessible", json.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Unrelated_exception_is_not_caught_by_middleware()
    {
        // The middleware must NOT catch arbitrary exceptions — they propagate
        // upward. TestServer re-throws them at the HttpClient boundary, so
        // we assert the exception escapes (rather than being swallowed and
        // serialised as an EncoderErrorShape).
        HttpClient client = BuildClient(_ =>
            throw new InvalidOperationException("something unrelated")
        );

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetAsync("/")
        );

        Assert.Equal("something unrelated", thrown.Message);
    }
}
