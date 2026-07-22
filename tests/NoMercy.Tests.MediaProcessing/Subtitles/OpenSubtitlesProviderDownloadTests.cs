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
using NoMercy.NmSystem.Extensions;

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
        StubHttpClientFactory factory = new(body: responseBody);
        return new(logger: NullLogger<OpenSubtitlesProvider>.Instance, httpClientFactory: factory);
    }

    private static byte[] Gzip(string content)
    {
        using MemoryStream destination = new();
        using (GZipStream gzip = new(stream: destination, mode: CompressionMode.Compress))
        {
            byte[] raw = Encoding.UTF8.GetBytes(s: content);
            gzip.Write(buffer: raw, offset: 0, count: raw.Length);
        }

        return destination.ToArray();
    }

    [Fact]
    public async Task DownloadSubtitleAsync_UnwrapsTheGzipArchiveIntoCueText()
    {
        OpenSubtitlesProvider provider = CreateProvider(responseBody: Gzip(content: DutchSrt));

        byte[] result = await provider.DownloadSubtitleAsync(
            downloadUrl: "https://dl.opensubtitles.org/en/download/src-api/vrf-19a70c55/file/1955260090",
            ct: CancellationToken.None
        );

        Encoding.UTF8.GetString(bytes: result).Should().Be(expected: DutchSrt);
    }

    [Fact]
    public async Task DownloadSubtitleAsync_ProducesTextTheVttConverterCanRead()
    {
        OpenSubtitlesProvider provider = CreateProvider(responseBody: Gzip(content: DutchSrt));

        byte[] result = await provider.DownloadSubtitleAsync(
            downloadUrl: "https://dl.opensubtitles.org/x",
            ct: CancellationToken.None
        );
        string vtt = SubtitleFormatConverter.SrtToVtt(srtContent: Encoding.UTF8.GetString(bytes: result));

        vtt.Should().StartWith(expected: "WEBVTT");
        vtt.Should().Contain(expected: "00:00:01.000 --> 00:00:04.000");
        vtt.Should().Contain(expected: "Wat als Captain Carter de eerste Avenger was?");
        vtt.Should().Contain(expected: "Hé, dat is één van mijn vragen.");
    }

    [Fact]
    public async Task DownloadSubtitleAsync_PassesThroughAnUncompressedPayload()
    {
        OpenSubtitlesProvider provider = CreateProvider(responseBody: Encoding.UTF8.GetBytes(s: DutchSrt));

        byte[] result = await provider.DownloadSubtitleAsync(
            downloadUrl: "https://dl.opensubtitles.org/x",
            ct: CancellationToken.None
        );

        Encoding.UTF8.GetString(bytes: result).Should().Be(expected: DutchSrt);
    }

    [Fact]
    public async Task DownloadSubtitleAsync_ThrowsRateLimit_OnTooManyRequests()
    {
        StubHttpClientFactory factory = new(body: [], status: HttpStatusCode.TooManyRequests);
        OpenSubtitlesProvider provider = new(logger: NullLogger<OpenSubtitlesProvider>.Instance, httpClientFactory: factory);

        await Assert.ThrowsAsync<OpenSubtitlesRateLimitException>(testCode: () =>
            provider.DownloadSubtitleAsync(downloadUrl: "https://dl.opensubtitles.org/x", ct: CancellationToken.None)
        );

        provider.IsRateLimited.Should().BeTrue();
    }

    private sealed class StubHttpClientFactory(
        byte[] body,
        HttpStatusCode status = HttpStatusCode.OK
    ) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler: new StubHandler(body: body, status: status));

        private sealed class StubHandler(byte[] body, HttpStatusCode status) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult(
                    result: new HttpResponseMessage(statusCode: status) { Content = new ByteArrayContent(content: body) }
                );
            }
        }
    }
}

/// <summary>
/// OpenSubtitles silently drops a sublanguageid it does not recognise and answers with a fulltext
/// match across every language, so a 2-letter code returns everything-but-the-language-asked-for.
/// These pin the codes the provider puts on the wire.
/// </summary>
public class OpenSubtitlesLanguageCodeTests
{
    [Theory]
    [InlineData(data: ["nl", "dut"])]
    [InlineData(data: ["de", "ger"])]
    [InlineData(data: ["en", "eng"])]
    public void WatchRequestLanguage_BecomesTheCodeTheApiAccepts(string watch, string wire)
    {
        Culture.BibliographicLanguageCode(code: watch).Should().Be(expected: wire);
    }

    [Fact]
    public void TheCodesInTheLiveSearchResponse_RoundTripUnchanged()
    {
        // SubLanguageID values from a real What If...? S01E01 response.
        string[] fromResponse = ["jpn", "hun", "pob", "slv", "spa"];

        foreach (string code in fromResponse)
            Culture.BibliographicLanguageCode(code: code).Should().Be(expected: code);
    }
}
