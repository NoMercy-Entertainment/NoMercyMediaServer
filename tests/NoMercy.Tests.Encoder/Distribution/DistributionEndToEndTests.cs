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
    private readonly byte[] _sharedKey = Encoding.UTF8.GetBytes(s: "end-to-end-shared-signing-key-32");
    private readonly TaskSerializer _serializer = new();

    [Fact]
    public async Task CoordinatorDispatches_WorkerReceivesAndRuns_ResultFlowsBack()
    {
        // Worker-side executor — pretends every ffmpeg invocation succeeds
        // in 1s. DispatchResult gets built by LocalWorkerDispatcher from
        // this executor's output.
        Mock<IFfmpegExecutor> executor = MakeSuccessExecutor();
        LocalWorkerDispatcher workerLocalDispatcher = new(
            executor: executor.Object,
            logger: NullLogger<LocalWorkerDispatcher>.Instance
        );

        // Fake HTTP handler routes /execute-task straight into the worker's
        // controller logic.
        WorkerHandler handler = new(localDispatcher: workerLocalDispatcher, serializer: _serializer, workerSigningKey: _sharedKey);
        HttpClient httpToWorker = new(handler: handler) { BaseAddress = new(uriString: "http://worker.test/") };

        HttpRemoteWorker remoteWorker = new(
            workerId: "end2end-worker",
            http: httpToWorker,
            serializer: _serializer,
            signingKey: _sharedKey,
            initialCapabilities: new HardwareCapabilities(Gpus: [], CpuCores: 4),
            initialBudget: new(AvailableGpuSlots: 0, AvailableCpuThreads: 4, GpuUtilization: 0),
            logger: NullLogger<HttpRemoteWorker>.Instance
        );

        InMemoryRemoteWorkerRegistry registry = new();
        registry.Register(worker: remoteWorker);

        // Coordinator-side local fallback — should NOT be called in the
        // happy path, but needs to be wired for the dispatcher.
        LocalWorkerDispatcher coordinatorFallback = new(
            executor: MakeSuccessExecutor().Object,
            logger: NullLogger<LocalWorkerDispatcher>.Instance
        );
        RemoteWorkerDispatcher coordinatorDispatcher = new(
            registry: registry,
            assigner: new WorkerAssigner(),
            localFallback: coordinatorFallback,
            logger: NullLogger<RemoteWorkerDispatcher>.Instance
        );

        EncodeTask[] tasks = [MakeTask(id: "e2e-1"), MakeTask(id: "e2e-2"), MakeTask(id: "e2e-3")];

        DispatchResult[] results = await coordinatorDispatcher.DispatchAsync(
            tasks: tasks,
            ct: CancellationToken.None
        );

        results.Should().HaveCount(expected: 3);
        results.Should().AllSatisfy(expected: r => r.Success.Should().BeTrue());
        results.Should().AllSatisfy(expected: r => r.WorkerId.Should().Be(expected: "end2end-worker"));
        handler.ReceivedTaskCount.Should().Be(expected: 3, because: "worker should have received all three tasks");
    }

    [Fact]
    public async Task WorkerSignsWithWrongKey_CoordinatorRejectsResponse()
    {
        // Worker uses a different signing key than the coordinator. The
        // coordinator will reject the response as "HMAC verification
        // failure" and the result falls back to local dispatch.
        byte[] wrongKey = Encoding.UTF8.GetBytes(s: "wrong-key-32-bytes-padded-here!!");

        Mock<IFfmpegExecutor> workerExec = MakeSuccessExecutor();
        LocalWorkerDispatcher workerLocal = new(
            executor: workerExec.Object,
            logger: NullLogger<LocalWorkerDispatcher>.Instance
        );

        WorkerHandler handler = new(localDispatcher: workerLocal, serializer: _serializer, workerSigningKey: wrongKey);
        HttpClient httpToWorker = new(handler: handler) { BaseAddress = new(uriString: "http://worker.test/") };

        HttpRemoteWorker remoteWorker = new(
            workerId: "mismatched-key",
            http: httpToWorker,
            serializer: _serializer,
            signingKey: _sharedKey, // coordinator's key — doesn't match worker's
            initialCapabilities: new HardwareCapabilities(Gpus: [], CpuCores: 4),
            initialBudget: new(AvailableGpuSlots: 0, AvailableCpuThreads: 4, GpuUtilization: 0),
            logger: NullLogger<HttpRemoteWorker>.Instance
        );

        InMemoryRemoteWorkerRegistry registry = new();
        registry.Register(worker: remoteWorker);

        LocalWorkerDispatcher coordinatorFallback = new(
            executor: MakeSuccessExecutor().Object,
            logger: NullLogger<LocalWorkerDispatcher>.Instance
        );
        RemoteWorkerDispatcher coordinatorDispatcher = new(
            registry: registry,
            assigner: new WorkerAssigner(),
            localFallback: coordinatorFallback,
            logger: NullLogger<RemoteWorkerDispatcher>.Instance
        );

        DispatchResult[] results = await coordinatorDispatcher.DispatchAsync(
            tasks: [MakeTask(id: "mismatch-1")],
            ct: CancellationToken.None
        );

        // The task must still complete via local fallback — a signing-key
        // mismatch must NOT fail the user's encode.
        results.Should().HaveCount(expected: 1);
        results[0].Success.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static Mock<IFfmpegExecutor> MakeSuccessExecutor()
    {
        Mock<IFfmpegExecutor> mock = new();
        mock.Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new ExecutionResult(
                    Success: true,
                    ExitCode: 0,
                    StdErr: string.Empty,
                    Duration: TimeSpan.FromSeconds(seconds: 1),
                    Error: null
                )
            );
        return mock;
    }

    private static EncodeTask MakeTask(string id) =>
        new(
            TaskId: id,
            Command: new(Executable: "ffmpeg", Arguments: ["-i", "in.mkv", "out.ts"], WorkingDirectory: null),
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
                !absolutePath.Contains(value: "/worker/tasks")
                && !absolutePath.Contains(value: "/worker/execute-task")
            )
                return new(statusCode: HttpStatusCode.NotFound);

            string body = await request.Content!.ReadAsStringAsync(cancellationToken: cancellationToken);
            EncodeTask? task = serializer.Deserialize(payload: body, signingKey: workerSigningKey);
            if (task is null)
                return new(statusCode: HttpStatusCode.Unauthorized);

            ReceivedTaskCount++;
            DispatchResult[] results = await localDispatcher.DispatchAsync(
                tasks: [task],
                ct: cancellationToken
            );
            DispatchResult result = results[0];

            string signed = serializer.SerializeResult(result: result, signingKey: workerSigningKey);
            return new(statusCode: HttpStatusCode.OK)
            {
                Content = new StringContent(content: signed, encoding: Encoding.UTF8, mediaType: "application/json"),
            };
        }
    }
}
