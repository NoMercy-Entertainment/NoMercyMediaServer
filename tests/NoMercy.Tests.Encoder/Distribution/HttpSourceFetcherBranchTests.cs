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

/// <summary>
/// Branch coverage for HttpSourceFetcher edge cases the happy-path
/// tests don't exercise: empty input path, missing coordinator URL,
/// distributed-encoding signing headers, extensionless source files,
/// and ReleaseAsync swallowing delete failures.
/// </summary>
public class HttpSourceFetcherBranchTests
{
    [Fact]
    public async Task EnsureLocalAsync_NullInputPath_ReturnsEmpty()
    {
        // A task with no input path is malformed — fetcher must return
        // early without hitting the coordinator or touching disk.
        string tempDir = CreateTempDir();
        try
        {
            HttpSourceFetcher sut = MakeFetcher(tempDir: tempDir, handler: out FakeHandler handler);
            EncodeTask task = MakeTask(inputPath: string.Empty, taskId: "no-input");

            string local = await sut.EnsureLocalAsync(task: task, ct: CancellationToken.None);

            local.Should().BeEmpty();
            handler.Requests.Should().BeEmpty(because: "no coordinator request for empty input");
        }
        finally
        {
            Directory.Delete(path: tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureLocalAsync_NoCoordinatorUrl_ReturnsInputPathUnchanged()
    {
        // Standalone worker has no coordinator to fetch from. The contract
        // is: log a warning and return the requested path so ffmpeg fails
        // naturally with the path it was asked to encode — not silently
        // substituted for an empty string the caller can't trace.
        string tempDir = CreateTempDir();
        try
        {
            EncoderOptions options = new()
            {
                DistributedEncodingSigningKey = "test-fetch-key-32bytes-length-!!",
                CoordinatorUrl = null,
                LiveTranscodeCachePath = tempDir,
            };

            ServiceCollection services = new();
            services.AddSingleton(implementationInstance: options);
            services.AddHttpClient();
            ServiceProvider provider = services.BuildServiceProvider();

            HttpSourceFetcher sut = new(
                httpClientFactory: provider.GetRequiredService<IHttpClientFactory>(),
                options: options,
                logger: NullLogger<HttpSourceFetcher>.Instance,
                storage: TestStorageFactory.CreateLocal()
            );
            EncodeTask task = MakeTask(inputPath: "/nonexistent/source.mkv", taskId: "no-coord");

            string local = await sut.EnsureLocalAsync(task: task, ct: CancellationToken.None);

            local.Should().Be(expected: "/nonexistent/source.mkv");
        }
        finally
        {
            Directory.Delete(path: tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureLocalAsync_DistributedEncodingEnabled_SendsHmacHeaders()
    {
        // With distributed encoding turned on, every coordinator request
        // must carry the timestamp + signature headers. Without them the
        // coordinator's auth middleware drops the request as anonymous.
        string tempDir = CreateTempDir();
        try
        {
            HttpSourceFetcher sut = MakeFetcher(tempDir: tempDir, handler: out FakeHandler handler);
            handler.RespondWith(status: HttpStatusCode.OK, body: [0xA]);

            EncodeTask task = MakeTask(inputPath: "/nonexistent/source.mkv", taskId: "t-hmac");
            await sut.EnsureLocalAsync(task: task, ct: CancellationToken.None);

            handler.Requests.Should().ContainSingle();
            RequestSnapshot snapshot = handler.Requests[index: 0];
            snapshot.Headers.Should().ContainKey(expected: "X-NoMercy-Timestamp");
            snapshot.Headers.Should().ContainKey(expected: "X-NoMercy-Signature");
            // The signature is base64 over HMAC-SHA256, length 44 with
            // trailing '='.
            snapshot.Headers[key: "X-NoMercy-Signature"].Should().HaveLength(expected: 44);
        }
        finally
        {
            Directory.Delete(path: tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureLocalAsync_ExtensionlessInput_UsesSrcSuffix()
    {
        // Source file with no extension — fetcher defaults to .src so
        // the cached file still has a stable, parseable suffix.
        string tempDir = CreateTempDir();
        try
        {
            HttpSourceFetcher sut = MakeFetcher(tempDir: tempDir, handler: out FakeHandler handler);
            handler.RespondWith(status: HttpStatusCode.OK, body: [0xB]);

            EncodeTask task = MakeTask(inputPath: "/share/raw-bytes", taskId: "t-noext");
            string local = await sut.EnsureLocalAsync(task: task, ct: CancellationToken.None);

            local.Should().EndWith(expected: ".src");
            Path.GetFileNameWithoutExtension(path: local).Should().Be(expected: "t-noext");
        }
        finally
        {
            Directory.Delete(path: tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureLocalAsync_SignedQueryHasUrlEncodedPath()
    {
        // The signed query is the contract with the coordinator: the path
        // appears URL-encoded so a Windows-style backslash or a space in
        // a share name doesn't break the request line.
        string tempDir = CreateTempDir();
        try
        {
            HttpSourceFetcher sut = MakeFetcher(tempDir: tempDir, handler: out FakeHandler handler);
            handler.RespondWith(status: HttpStatusCode.OK, body: [0xC]);

            EncodeTask task = MakeTask(inputPath: "/share/My Movies/raw bytes.mkv", taskId: "t-encoded");
            await sut.EnsureLocalAsync(task: task, ct: CancellationToken.None);

            handler.Requests[index: 0].Query.Should().Contain(expected: "%20", because: "spaces must be percent-encoded");
            handler.Requests[index: 0].Query.Should().NotContain(unexpected: " ");
        }
        finally
        {
            Directory.Delete(path: tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ReleaseAsync_NoCachedFile_DoesNotThrow()
    {
        // Release after a failed fetch (nothing in cache) must be a no-op.
        // The catch in ReleaseAsync covers this; verify nothing escapes.
        string tempDir = CreateTempDir();
        try
        {
            HttpSourceFetcher sut = MakeFetcher(tempDir: tempDir, handler: out _);
            EncodeTask task = MakeTask(inputPath: "/nonexistent/source.mkv", taskId: "t-no-cache");

            Func<Task> act = () => sut.ReleaseAsync(task: task);

            await act.Should().NotThrowAsync();
        }
        finally
        {
            Directory.Delete(path: tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureLocalAsync_5xxResponse_Throws()
    {
        // 500 from the coordinator is just as fatal as 404 — the test
        // for 404 exists; pin 500 to prove the branch isn't 404-specific.
        string tempDir = CreateTempDir();
        try
        {
            HttpSourceFetcher sut = MakeFetcher(tempDir: tempDir, handler: out FakeHandler handler);
            handler.RespondWith(status: HttpStatusCode.InternalServerError, body: Encoding.UTF8.GetBytes(s: "boom"));

            EncodeTask task = MakeTask(inputPath: "/nonexistent/source.mkv", taskId: "t-500");

            Func<Task> act = () => sut.EnsureLocalAsync(task: task, ct: CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            Directory.Delete(path: tempDir, recursive: true);
        }
    }

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
            Dictionary<string, string> headers = new();
            foreach (KeyValuePair<string, IEnumerable<string>> h in request.Headers)
            {
                headers[key: h.Key] = string.Join(separator: ",", values: h.Value);
            }

            lock (_requests)
                _requests.Add(item: new(Query: request.RequestUri!.Query, Headers: headers));

            return Task.FromResult(
                result: new HttpResponseMessage(statusCode: _status) { Content = new ByteArrayContent(content: _body) }
            );
        }
    }

    public sealed record RequestSnapshot(string Query, IReadOnlyDictionary<string, string> Headers);
}
