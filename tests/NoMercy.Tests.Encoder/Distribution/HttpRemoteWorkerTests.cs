using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.Distribution;
using NoMercy.Encoder.Hardware;

namespace NoMercy.Tests.Encoder.Distribution;

public class HttpRemoteWorkerTests
{
    private readonly byte[] _signingKey = Encoding.UTF8.GetBytes(
        "http-worker-signing-key-32bytes!"
    );
    private readonly TaskSerializer _serializer = new();

    [Fact]
    public async Task ExecuteTaskAsync_SuccessResponse_ReturnsSignedResult()
    {
        DispatchResult workerResult = new(
            TaskId: "t1",
            Success: true,
            OutputPath: "/remote/out/t1",
            Duration: TimeSpan.FromSeconds(7)
        );
        string signedResponse = _serializer.SerializeResult(workerResult, _signingKey);

        HttpClient http = MakeClientReturning(HttpStatusCode.OK, signedResponse);
        HttpRemoteWorker sut = MakeWorker("beast", http);

        EncodeTask task = MakeTask("t1");
        DispatchResult result = await sut.ExecuteTaskAsync(task, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.OutputPath.Should().Be("/remote/out/t1");
        result.WorkerId.Should().Be("beast", "dispatcher always stamps the worker id");
    }

    [Fact]
    public async Task ExecuteTaskAsync_WorkerReturns500_ReturnsFailureDispatchResult()
    {
        HttpClient http = MakeClientReturning(
            HttpStatusCode.InternalServerError,
            "worker exploded"
        );
        HttpRemoteWorker sut = MakeWorker("broken", http);

        DispatchResult result = await sut.ExecuteTaskAsync(MakeTask("t2"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("500");
        result.WorkerId.Should().Be("broken");
    }

    [Fact]
    public async Task ExecuteTaskAsync_HttpRequestException_ReturnsFailureDispatchResult()
    {
        HttpClient http = MakeClientThrowing(new HttpRequestException("connection refused"));
        HttpRemoteWorker sut = MakeWorker("offline", http);

        DispatchResult result = await sut.ExecuteTaskAsync(MakeTask("t3"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("connection refused");
        result.WorkerId.Should().Be("offline");
    }

    [Fact]
    public async Task ExecuteTaskAsync_UnsignedResponse_ReturnsVerificationFailure()
    {
        // Worker returns 200 but with a body that doesn't pass HMAC —
        // treated as tampered / compromised worker, refuse the result.
        HttpClient http = MakeClientReturning(HttpStatusCode.OK, "{\"Success\":true}");
        HttpRemoteWorker sut = MakeWorker("evil", http);

        DispatchResult result = await sut.ExecuteTaskAsync(MakeTask("t4"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("HMAC");
    }

    [Fact]
    public async Task ExecuteTaskAsync_Cancellation_Throws()
    {
        HttpClient http = MakeClientReturning(HttpStatusCode.OK, "ignored");
        HttpRemoteWorker sut = MakeWorker("w", http);

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        Func<Task> act = () => sut.ExecuteTaskAsync(MakeTask("t5"), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void UpdateSnapshot_RefreshesBudget()
    {
        HttpClient http = new();
        HttpRemoteWorker sut = MakeWorker("w", http);

        sut.GetAvailableBudget().AvailableCpuThreads.Should().Be(4);

        sut.UpdateSnapshot(
            MakeCapabilities(),
            new(AvailableGpuSlots: 2, AvailableCpuThreads: 16, GpuUtilization: 0.1)
        );

        sut.GetAvailableBudget().AvailableCpuThreads.Should().Be(16);
        sut.GetAvailableBudget().AvailableGpuSlots.Should().Be(2);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private HttpRemoteWorker MakeWorker(string id, HttpClient http) =>
        new(
            workerId: id,
            http: http,
            serializer: _serializer,
            signingKey: _signingKey,
            initialCapabilities: MakeCapabilities(),
            initialBudget: new(0, 4, 0),
            logger: NullLogger<HttpRemoteWorker>.Instance
        );

    private static IHardwareCapabilities MakeCapabilities() => new HardwareCapabilities([], 4);

    private static EncodeTask MakeTask(string id) =>
        new(
            TaskId: id,
            Command: new("ffmpeg", [], null),
            OutputPath: $"/out/{id}",
            Type: EncodeTaskType.QualityVariant
        );

    private static HttpClient MakeClientReturning(HttpStatusCode status, string body) =>
        new(
            new FakeHandler(
                (req, ct) =>
                    Task.FromResult(
                        new HttpResponseMessage(status) { Content = new StringContent(body) }
                    )
            )
        )
        {
            BaseAddress = new("http://worker.test/"),
        };

    private static HttpClient MakeClientThrowing(Exception ex) =>
        new(new FakeHandler((req, ct) => Task.FromException<HttpResponseMessage>(ex)))
        {
            BaseAddress = new("http://worker.test/"),
        };

    private sealed class FakeHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond
    ) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return respond(request, cancellationToken);
        }
    }
}
