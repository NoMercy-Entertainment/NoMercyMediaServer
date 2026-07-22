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
using System.Text;
using FluentAssertions;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Media.Subtitles;

/// <summary>
/// POST api/v1/subtitles/download fetches <c>download_url</c> server-side. That
/// field is fully client-controlled, so it must never be allowed to reach an
/// internal host (loopback / LAN / cloud link-local metadata) or a non-http(s)
/// scheme — the Jellyfin CVE-2026-35032 SSRF shape. These lock the guard that
/// rejects such URLs before any fetch.
/// </summary>
[Trait(name: "Category", value: "Subtitles")]
public class SubtitleDownloadSsrfTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _authed;

    public SubtitleDownloadSsrfTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
    }

    [Theory]
    [InlineData(data: "http://169.254.169.254/latest/meta-data/")] // cloud metadata
    [InlineData(data: "http://127.0.0.1:7626/api/v1/dashboard/server")] // loopback
    [InlineData(data: "https://10.0.0.1/subs.srt")] // private LAN
    [InlineData(data: "https://192.168.1.10/subs.srt")] // private LAN
    [InlineData(data: "file:///etc/passwd")] // non-http scheme
    public async Task Download_RejectsServerSideRequestToInternalOrNonHttpUrl(string downloadUrl)
    {
        string payload =
            "{\"type\":\"movie\",\"id\":1,\"download_url\":\""
            + downloadUrl
            + "\",\"language\":\"en\"}";
        StringContent body = new(content: payload, encoding: Encoding.UTF8, mediaType: "application/json");

        HttpResponseMessage response = await _authed.PostAsync(requestUri: "/api/v1/subtitles/download", content: body);

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }
}
