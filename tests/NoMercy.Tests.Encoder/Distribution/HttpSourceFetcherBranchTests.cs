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
            HttpSourceFetcher sut = MakeFetcher(tempDir, out FakeHandler handler);
            EncodeTask task = MakeTask(string.Empty, taskId: "no-input");

            string local = await sut.EnsureLocalAsync(task, CancellationToken.None);

            local.Should().BeEmpty();
            handler.Requests.Should().BeEmpty("no coordinator request for empty input");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
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
            services.AddSingleton(options);
            services.AddHttpClient();
            ServiceProvider provider = services.BuildServiceProvider();

            HttpSourceFetcher sut = new(
                provider.GetRequiredService<IHttpClientFactory>(),
                options,
                NullLogger<HttpSourceFetcher>.Instance,
                TestStorageFactory.CreateLocal()
            );
            EncodeTask task = MakeTask("/nonexistent/source.mkv", taskId: "no-coord");

            string local = await sut.EnsureLocalAsync(task, CancellationToken.None);

            local.Should().Be("/nonexistent/source.mkv");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
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
            HttpSourceFetcher sut = MakeFetcher(tempDir, out FakeHandler handler);
            handler.RespondWith(HttpStatusCode.OK, [0xA]);

            EncodeTask task = MakeTask("/nonexistent/source.mkv", taskId: "t-hmac");
            await sut.EnsureLocalAsync(task, CancellationToken.None);

            handler.Requests.Should().ContainSingle();
            RequestSnapshot snapshot = handler.Requests[0];
            snapshot.Headers.Should().ContainKey("X-NoMercy-Timestamp");
            snapshot.Headers.Should().ContainKey("X-NoMercy-Signature");
            // The signature is base64 over HMAC-SHA256, length 44 with
            // trailing '='.
            snapshot.Headers["X-NoMercy-Signature"].Should().HaveLength(44);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
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
            HttpSourceFetcher sut = MakeFetcher(tempDir, out FakeHandler handler);
            handler.RespondWith(HttpStatusCode.OK, [0xB]);

            EncodeTask task = MakeTask("/share/raw-bytes", taskId: "t-noext");
            string local = await sut.EnsureLocalAsync(task, CancellationToken.None);

            local.Should().EndWith(".src");
            Path.GetFileNameWithoutExtension(local).Should().Be("t-noext");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
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
            HttpSourceFetcher sut = MakeFetcher(tempDir, out FakeHandler handler);
            handler.RespondWith(HttpStatusCode.OK, [0xC]);

            EncodeTask task = MakeTask("/share/My Movies/raw bytes.mkv", taskId: "t-encoded");
            await sut.EnsureLocalAsync(task, CancellationToken.None);

            handler.Requests[0].Query.Should().Contain("%20", "spaces must be percent-encoded");
            handler.Requests[0].Query.Should().NotContain(" ");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
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
            HttpSourceFetcher sut = MakeFetcher(tempDir, out _);
            EncodeTask task = MakeTask("/nonexistent/source.mkv", taskId: "t-no-cache");

            Func<Task> act = () => sut.ReleaseAsync(task);

            await act.Should().NotThrowAsync();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
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
            HttpSourceFetcher sut = MakeFetcher(tempDir, out FakeHandler handler);
            handler.RespondWith(HttpStatusCode.InternalServerError, Encoding.UTF8.GetBytes("boom"));

            EncodeTask task = MakeTask("/nonexistent/source.mkv", taskId: "t-500");

            Func<Task> act = () => sut.EnsureLocalAsync(task, CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────

    private static string CreateTempDir()
    {
        string dir = Path.Combine(
            Path.GetTempPath(),
            "nomercy-test-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static EncodeTask MakeTask(string inputPath, string taskId = "task") =>
        new(
            TaskId: taskId,
            Command: new("ffmpeg", ["-i", inputPath, "out.ts"], null),
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
        services.AddSingleton(options);
        services
            .AddHttpClient()
            .ConfigureHttpClientDefaults(b =>
                b.ConfigurePrimaryHttpMessageHandler(() => capturedHandler)
            );
        ServiceProvider provider = services.BuildServiceProvider();

        return new(
            provider.GetRequiredService<IHttpClientFactory>(),
            options,
            NullLogger<HttpSourceFetcher>.Instance,
            TestStorageFactory.CreateLocal()
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
                headers[h.Key] = string.Join(",", h.Value);
            }

            lock (_requests)
                _requests.Add(new(request.RequestUri!.Query, headers));

            return Task.FromResult(
                new HttpResponseMessage(_status) { Content = new ByteArrayContent(_body) }
            );
        }
    }

    public sealed record RequestSnapshot(string Query, IReadOnlyDictionary<string, string> Headers);
}
