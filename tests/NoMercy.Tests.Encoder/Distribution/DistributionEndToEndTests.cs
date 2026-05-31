using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Distribution;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Progress;

namespace NoMercy.Tests.Encoder.Distribution;

/// <summary>
/// End-to-end protocol test. Runs a simulated coordinator + worker in
/// one test process:
///
///   coordinator.RemoteWorkerDispatcher
///     → HttpRemoteWorker (fake HttpClient bound to worker endpoint)
///     → worker.ExecuteTask controller logic
///     → worker.LocalWorkerDispatcher (mock IFfmpegExecutor)
///     → DispatchResult signed and returned
///     → coordinator verifies + unwraps
///
/// The fake HttpMessageHandler routes /execute-task calls directly into
/// an in-memory worker pipeline — no real sockets, but every real
/// serialization / HMAC / dispatcher code path executes. A regression
/// anywhere in the chain (signing, envelope shape, response status
/// codes) breaks this test, which is the value: the unit tests cover
/// each layer, this one proves they compose.
/// </summary>
public class DistributionEndToEndTests
{
    private readonly byte[] _sharedKey = Encoding.UTF8.GetBytes("end-to-end-shared-signing-key-32");
    private readonly TaskSerializer _serializer = new();

    [Fact]
    public async Task CoordinatorDispatches_WorkerReceivesAndRuns_ResultFlowsBack()
    {
        // Worker-side executor — pretends every ffmpeg invocation succeeds
        // in 1s. DispatchResult gets built by LocalWorkerDispatcher from
        // this executor's output.
        Mock<IFfmpegExecutor> executor = MakeSuccessExecutor();
        LocalWorkerDispatcher workerLocalDispatcher = new(
            executor.Object,
            NullLogger<LocalWorkerDispatcher>.Instance
        );

        // Fake HTTP handler routes /execute-task straight into the worker's
        // controller logic.
        WorkerHandler handler = new(workerLocalDispatcher, _serializer, _sharedKey);
        HttpClient httpToWorker = new(handler) { BaseAddress = new("http://worker.test/") };

        HttpRemoteWorker remoteWorker = new(
            workerId: "end2end-worker",
            http: httpToWorker,
            serializer: _serializer,
            signingKey: _sharedKey,
            initialCapabilities: new HardwareCapabilities([], 4),
            initialBudget: new(0, 4, 0),
            logger: NullLogger<HttpRemoteWorker>.Instance
        );

        InMemoryRemoteWorkerRegistry registry = new();
        registry.Register(remoteWorker);

        // Coordinator-side local fallback — should NOT be called in the
        // happy path, but needs to be wired for the dispatcher.
        LocalWorkerDispatcher coordinatorFallback = new(
            MakeSuccessExecutor().Object,
            NullLogger<LocalWorkerDispatcher>.Instance
        );
        RemoteWorkerDispatcher coordinatorDispatcher = new(
            registry,
            new WorkerAssigner(),
            coordinatorFallback,
            NullLogger<RemoteWorkerDispatcher>.Instance
        );

        EncodeTask[] tasks = [MakeTask("e2e-1"), MakeTask("e2e-2"), MakeTask("e2e-3")];

        DispatchResult[] results = await coordinatorDispatcher.DispatchAsync(
            tasks,
            CancellationToken.None
        );

        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
        results.Should().AllSatisfy(r => r.WorkerId.Should().Be("end2end-worker"));
        handler.ReceivedTaskCount.Should().Be(3, "worker should have received all three tasks");
    }

    [Fact]
    public async Task WorkerSignsWithWrongKey_CoordinatorRejectsResponse()
    {
        // Worker uses a different signing key than the coordinator. The
        // coordinator will reject the response as "HMAC verification
        // failure" and the result falls back to local dispatch.
        byte[] wrongKey = Encoding.UTF8.GetBytes("wrong-key-32-bytes-padded-here!!");

        Mock<IFfmpegExecutor> workerExec = MakeSuccessExecutor();
        LocalWorkerDispatcher workerLocal = new(
            workerExec.Object,
            NullLogger<LocalWorkerDispatcher>.Instance
        );

        WorkerHandler handler = new(workerLocal, _serializer, wrongKey);
        HttpClient httpToWorker = new(handler) { BaseAddress = new("http://worker.test/") };

        HttpRemoteWorker remoteWorker = new(
            workerId: "mismatched-key",
            http: httpToWorker,
            serializer: _serializer,
            signingKey: _sharedKey, // coordinator's key — doesn't match worker's
            initialCapabilities: new HardwareCapabilities([], 4),
            initialBudget: new(0, 4, 0),
            logger: NullLogger<HttpRemoteWorker>.Instance
        );

        InMemoryRemoteWorkerRegistry registry = new();
        registry.Register(remoteWorker);

        LocalWorkerDispatcher coordinatorFallback = new(
            MakeSuccessExecutor().Object,
            NullLogger<LocalWorkerDispatcher>.Instance
        );
        RemoteWorkerDispatcher coordinatorDispatcher = new(
            registry,
            new WorkerAssigner(),
            coordinatorFallback,
            NullLogger<RemoteWorkerDispatcher>.Instance
        );

        DispatchResult[] results = await coordinatorDispatcher.DispatchAsync(
            [MakeTask("mismatch-1")],
            CancellationToken.None
        );

        // The task must still complete via local fallback — a signing-key
        // mismatch must NOT fail the user's encode.
        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static Mock<IFfmpegExecutor> MakeSuccessExecutor()
    {
        Mock<IFfmpegExecutor> mock = new();
        mock.Setup(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ExecutionResult(
                    Success: true,
                    ExitCode: 0,
                    StdErr: string.Empty,
                    Duration: TimeSpan.FromSeconds(1),
                    Error: null
                )
            );
        return mock;
    }

    private static EncodeTask MakeTask(string id) =>
        new(
            TaskId: id,
            Command: new("ffmpeg", ["-i", "in.mkv", "out.ts"], null),
            OutputPath: $"/out/{id}",
            Type: EncodeTaskType.QualityVariant
        );

    /// <summary>
    /// Simulates the worker-side /api/v1/worker/execute-task endpoint. Runs
    /// the same verify → local-dispatch → sign flow that WorkerExecutionController
    /// does, but directly against an injected LocalWorkerDispatcher.
    /// </summary>
    private sealed class WorkerHandler(
        LocalWorkerDispatcher localDispatcher,
        ITaskSerializer serializer,
        byte[] workerSigningKey
    ) : HttpMessageHandler
    {
        public int ReceivedTaskCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            string absolutePath = request.RequestUri!.AbsolutePath;
            if (
                !absolutePath.Contains("/worker/tasks")
                && !absolutePath.Contains("/worker/execute-task")
            )
                return new(HttpStatusCode.NotFound);

            string body = await request.Content!.ReadAsStringAsync(cancellationToken);
            EncodeTask? task = serializer.Deserialize(body, workerSigningKey);
            if (task is null)
                return new(HttpStatusCode.Unauthorized);

            ReceivedTaskCount++;
            DispatchResult[] results = await localDispatcher.DispatchAsync(
                [task],
                cancellationToken
            );
            DispatchResult result = results[0];

            string signed = serializer.SerializeResult(result, workerSigningKey);
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(signed, Encoding.UTF8, "application/json"),
            };
        }
    }
}
