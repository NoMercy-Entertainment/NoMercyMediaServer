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

using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.MediaProcessing.Subtitles;

namespace NoMercy.Tests.MediaProcessing.Subtitles;

/// <summary>
/// Downloads hit a different host to the searches and so never went through the client's rate
/// limit queue: a backlog sweep issued them as fast as its loop ran. These pin that they are
/// paced, and that a request someone is waiting on is not stuck behind the sweep.
/// </summary>
public class OpenSubtitlesDownloadQueueTests
{
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
    public async Task DownloadSubtitleAsync_SerialisesConcurrentDownloads()
    {
        RecordingHandler handler = new(body: Gzip(content: "1\n00:00:01,000 --> 00:00:02,000\ncue"));
        OpenSubtitlesProvider provider = new(
            logger: NullLogger<OpenSubtitlesProvider>.Instance,
            httpClientFactory: new StubFactory(handler: handler)
        );

        await Task.WhenAll(
            tasks: Enumerable
                .Range(start: 0, count: 5)
                .Select(selector: i =>
                    provider.DownloadSubtitleAsync(
                        downloadUrl: $"https://dl.opensubtitles.org/{i}",
                        ct: CancellationToken.None
                    )
                )
        );

        handler.Requests.Should().HaveCount(expected: 5);
        handler.MaxConcurrent.Should().Be(expected: 1, because: "the queue admits one download at a time");
    }

    [Fact]
    public async Task DownloadSubtitleAsync_StillReturnsCueBytesThroughTheQueue()
    {
        const string srt = "1\n00:00:01,000 --> 00:00:04,000\nWat als...?";
        RecordingHandler handler = new(body: Gzip(content: srt));
        OpenSubtitlesProvider provider = new(
            logger: NullLogger<OpenSubtitlesProvider>.Instance,
            httpClientFactory: new StubFactory(handler: handler)
        );

        byte[] result = await provider.DownloadSubtitleAsync(
            downloadUrl: "https://dl.opensubtitles.org/x",
            ct: CancellationToken.None,
            priority: true
        );

        Encoding.UTF8.GetString(bytes: result).Should().Be(expected: srt);
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler: handler, disposeHandler: false);
    }

    private sealed class RecordingHandler(byte[] body) : HttpMessageHandler
    {
        private int _concurrent;

        public ConcurrentBag<string> Requests { get; } = [];
        public int MaxConcurrent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            int running = Interlocked.Increment(location: ref _concurrent);
            lock (Requests)
                MaxConcurrent = Math.Max(val1: MaxConcurrent, val2: running);

            Requests.Add(item: request.RequestUri?.ToString() ?? string.Empty);

            try
            {
                await Task.Delay(millisecondsDelay: 40, cancellationToken: cancellationToken);
                return new(statusCode: System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(content: body) };
            }
            finally
            {
                Interlocked.Decrement(location: ref _concurrent);
            }
        }
    }
}
