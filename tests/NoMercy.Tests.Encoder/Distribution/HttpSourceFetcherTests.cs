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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Distribution;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Distribution;

public class HttpSourceFetcherTests
{
    [Fact]
    public async Task EnsureLocalAsync_SourceExistsLocally_ReturnsPathUnchanged()
    {
        // Fast path: worker sees the source on its own filesystem
        // (shared NAS). No download, no tempdir write.
        string tempDir = CreateTempDir();
        try
        {
            string sourcePath = Path.Combine(path1: tempDir, path2: "source.mkv");
            await File.WriteAllBytesAsync(path: sourcePath, bytes: [0x00, 0x01, 0x02]);

            HttpSourceFetcher sut = MakeFetcher(tempDir: tempDir, handler: out _);
            EncodeTask task = MakeTask(inputPath: sourcePath);

            string local = await sut.EnsureLocalAsync(task: task, ct: CancellationToken.None);

            local.Should().Be(expected: sourcePath);
        }
        finally
        {
            Directory.Delete(path: tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureLocalAsync_RemoteSource_DownloadsToCache()
    {
        // Source doesn't exist locally → fetcher hits the coordinator,
        // writes the response body to the cache directory, returns the
        // cached path.
        string tempDir = CreateTempDir();
        try
        {
            byte[] expectedBytes = Encoding.UTF8.GetBytes(s: "remote-source-bytes");
            HttpSourceFetcher sut = MakeFetcher(tempDir: tempDir, handler: out FakeHandler handler);
            handler.RespondWith(status: HttpStatusCode.OK, body: expectedBytes);

            EncodeTask task = MakeTask(inputPath: "/nonexistent/source.mkv", taskId: "t-dl-1");
            string local = await sut.EnsureLocalAsync(task: task, ct: CancellationToken.None);

            local.Should().NotBe(unexpected: task.InputPath);
            File.Exists(path: local).Should().BeTrue(because: "downloaded file must land on disk");
            (await File.ReadAllBytesAsync(path: local))
                .Should()
                .BeEquivalentTo(expectation: expectedBytes, because: "downloaded bytes must match the response body");

            handler.Requests.Should().HaveCount(expected: 1);
            handler.Requests[index: 0].Query.Should().Contain(expected: "path=");
            handler.Requests[index: 0].Query.Should().Contain(expected: "sig=");
            handler.Requests[index: 0].Query.Should().Contain(expected: "ts=");
        }
        finally
        {
            Directory.Delete(path: tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureLocalAsync_Retry_ReusesCachedDownload()
    {
        // Second EnsureLocalAsync for the same task should not re-download
        // — the cached file from the first attempt is idempotency-key'd
        // by task ID.
        string tempDir = CreateTempDir();
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(s: "cached-reuse-test");
            HttpSourceFetcher sut = MakeFetcher(tempDir: tempDir, handler: out FakeHandler handler);
            handler.RespondWith(status: HttpStatusCode.OK, body: bytes);

            EncodeTask task = MakeTask(inputPath: "/nonexistent/source.mkv", taskId: "t-retry");
            await sut.EnsureLocalAsync(task: task, ct: CancellationToken.None);

            int requestsAfterFirst = handler.Requests.Count;
            await sut.EnsureLocalAsync(task: task, ct: CancellationToken.None);

            handler
                .Requests.Count.Should()
                .Be(expected: requestsAfterFirst, because: "second call must reuse the cached file");
        }
        finally
        {
            Directory.Delete(path: tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureLocalAsync_CoordinatorNonSuccess_Throws()
    {
        string tempDir = CreateTempDir();
        try
        {
            HttpSourceFetcher sut = MakeFetcher(tempDir: tempDir, handler: out FakeHandler handler);
            handler.RespondWith(status: HttpStatusCode.NotFound, body: Encoding.UTF8.GetBytes(s: "not found"));

            EncodeTask task = MakeTask(inputPath: "/nonexistent/source.mkv", taskId: "t-404");

            Func<Task> act = () => sut.EnsureLocalAsync(task: task, ct: CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            Directory.Delete(path: tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ReleaseAsync_DeletesCachedFile()
    {
        string tempDir = CreateTempDir();
        try
        {
            HttpSourceFetcher sut = MakeFetcher(tempDir: tempDir, handler: out FakeHandler handler);
            handler.RespondWith(status: HttpStatusCode.OK, body: Encoding.UTF8.GetBytes(s: "x"));

            EncodeTask task = MakeTask(inputPath: "/nonexistent/source.mkv", taskId: "t-release");
            string local = await sut.EnsureLocalAsync(task: task, ct: CancellationToken.None);
            File.Exists(path: local).Should().BeTrue();

            await sut.ReleaseAsync(task: task);

            File.Exists(path: local).Should().BeFalse(because: "ReleaseAsync must clean up the cached download");
        }
        finally
        {
            if (Directory.Exists(path: tempDir))
                Directory.Delete(path: tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task NullSourceFetcher_ReturnsInputUnchanged_NoDiskIo()
    {
        NullSourceFetcher sut = new();
        EncodeTask task = MakeTask(inputPath: "/anywhere/file.mkv");

        string local = await sut.EnsureLocalAsync(task: task, ct: CancellationToken.None);

        local.Should().Be(expected: "/anywhere/file.mkv");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static string CreateTempDir()
    {
        string dir = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "nomercy-test-" + Guid.NewGuid().ToString(format: "N")
        );
        Directory.CreateDirectory(path: dir);
        return dir;
    }

    private static EncodeTask MakeTask(string inputPath, string taskId = "task") =>
        new(
            TaskId: taskId,
            Command: new(Executable: "ffmpeg", Arguments: ["-i", inputPath, "out.ts"], WorkingDirectory: null),
            OutputPath: "/out/" + taskId,
            Type: EncodeTaskType.QualityVariant,
            InputPath: inputPath
        );

    private static HttpSourceFetcher MakeFetcher(string tempDir, out FakeHandler handler)
    {
        EncoderOptions options = new()
        {
            DistributedEncodingSigningKey = "test-fetch-key-32bytes-length-!!",
            CoordinatorUrl = "http://coordinator.test",
            LiveTranscodeCachePath = tempDir,
        };

        handler = new();
        FakeHandler capturedHandler = handler;

        ServiceCollection services = new();
        services.AddSingleton(implementationInstance: options);
        services
            .AddHttpClient()
            .ConfigureHttpClientDefaults(configure: b =>
                b.ConfigurePrimaryHttpMessageHandler(configureHandler: () => capturedHandler)
            );

        ServiceProvider provider = services.BuildServiceProvider();

        return new(
            httpClientFactory: provider.GetRequiredService<IHttpClientFactory>(),
            options: options,
            logger: NullLogger<HttpSourceFetcher>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private HttpStatusCode _status = HttpStatusCode.OK;
        private byte[] _body = [];
        private readonly List<RequestSnapshot> _requests = [];

        public IReadOnlyList<RequestSnapshot> Requests
        {
            get
            {
                lock (_requests)
                    return _requests.ToArray();
            }
        }

        public void RespondWith(HttpStatusCode status, byte[] body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_requests)
                _requests.Add(item: new(Query: request.RequestUri!.Query));

            return Task.FromResult(
                result: new HttpResponseMessage(statusCode: _status) { Content = new ByteArrayContent(content: _body) }
            );
        }
    }

    public sealed record RequestSnapshot(string Query);
}
