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

using System.IO.Compression;
using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.Subtitles;
using NoMercy.MediaProcessing.Subtitles;

namespace NoMercy.Tests.MediaProcessing.Subtitles;

/// <summary>
/// OpenSubtitles serves SubDownloadLink as a gzip payload in the response body. These drive the
/// real provider against a stubbed transport to prove the bytes handed to callers are cue text.
/// </summary>
public class OpenSubtitlesProviderDownloadTests
{
    private const string DutchSrt = """
        1
        00:00:01,000 --> 00:00:04,000
        Wat als Captain Carter de eerste Avenger was?

        2
        00:00:05,500 --> 00:00:08,250
        Hé, dat is één van mijn vragen.
        """;

    private static OpenSubtitlesProvider CreateProvider(byte[] responseBody)
    {
        StubHttpClientFactory factory = new(responseBody);
        return new(NullLogger<OpenSubtitlesProvider>.Instance, factory);
    }

    private static byte[] Gzip(string content)
    {
        using MemoryStream destination = new();
        using (GZipStream gzip = new(destination, CompressionMode.Compress))
        {
            byte[] raw = Encoding.UTF8.GetBytes(content);
            gzip.Write(raw, 0, raw.Length);
        }

        return destination.ToArray();
    }

    [Fact]
    public async Task DownloadSubtitleAsync_UnwrapsTheGzipArchiveIntoCueText()
    {
        OpenSubtitlesProvider provider = CreateProvider(Gzip(DutchSrt));

        byte[] result = await provider.DownloadSubtitleAsync(
            "https://dl.opensubtitles.org/en/download/src-api/vrf-19a70c55/file/1955260090",
            CancellationToken.None
        );

        Encoding.UTF8.GetString(result).Should().Be(DutchSrt);
    }

    [Fact]
    public async Task DownloadSubtitleAsync_ProducesTextTheVttConverterCanRead()
    {
        OpenSubtitlesProvider provider = CreateProvider(Gzip(DutchSrt));

        byte[] result = await provider.DownloadSubtitleAsync(
            "https://dl.opensubtitles.org/x",
            CancellationToken.None
        );
        string vtt = SubtitleFormatConverter.SrtToVtt(Encoding.UTF8.GetString(result));

        vtt.Should().StartWith("WEBVTT");
        vtt.Should().Contain("00:00:01.000 --> 00:00:04.000");
        vtt.Should().Contain("Wat als Captain Carter de eerste Avenger was?");
        vtt.Should().Contain("Hé, dat is één van mijn vragen.");
    }

    [Fact]
    public async Task DownloadSubtitleAsync_PassesThroughAnUncompressedPayload()
    {
        OpenSubtitlesProvider provider = CreateProvider(Encoding.UTF8.GetBytes(DutchSrt));

        byte[] result = await provider.DownloadSubtitleAsync(
            "https://dl.opensubtitles.org/x",
            CancellationToken.None
        );

        Encoding.UTF8.GetString(result).Should().Be(DutchSrt);
    }

    [Fact]
    public async Task DownloadSubtitleAsync_ThrowsRateLimit_OnTooManyRequests()
    {
        StubHttpClientFactory factory = new([], HttpStatusCode.TooManyRequests);
        OpenSubtitlesProvider provider = new(NullLogger<OpenSubtitlesProvider>.Instance, factory);

        await Assert.ThrowsAsync<OpenSubtitlesRateLimitException>(() =>
            provider.DownloadSubtitleAsync("https://dl.opensubtitles.org/x", CancellationToken.None)
        );

        provider.IsRateLimited.Should().BeTrue();
    }

    private sealed class StubHttpClientFactory(
        byte[] body,
        HttpStatusCode status = HttpStatusCode.OK
    ) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubHandler(body, status));

        private sealed class StubHandler(byte[] body, HttpStatusCode status) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult(
                    new HttpResponseMessage(status) { Content = new ByteArrayContent(body) }
                );
            }
        }
    }
}
