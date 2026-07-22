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

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Distribution;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Progress;

namespace NoMercy.Tests.Encoder.Distribution;

public class RemoteWorkerDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_NoRemoteWorkers_FallsBackToLocal()
    {
        Mock<IFfmpegExecutor> executor = MakeExecutor(succeed: true);
        LocalWorkerDispatcher local = NewLocal(executor: executor.Object);
        RemoteWorkerDispatcher sut = NewRemote(local: local, registry: new EmptyRemoteWorkerRegistry());

        DispatchResult[] results = await sut.DispatchAsync(
            tasks: [MakeTask(id: "t0")],
            ct: CancellationToken.None
        );

        results.Should().HaveCount(expected: 1);
        results[0].Success.Should().BeTrue();
    }

    [Fact]
    public async Task DispatchAsync_WorkerSucceeds_UsesWorkerResult()
    {
        // Worker returns a successful DispatchResult — dispatcher must
        // return that directly, not the local fallback. WorkerId on the
        // result proves the worker was the one that executed.
        Mock<IFfmpegExecutor> executor = MakeExecutor(succeed: true);
        LocalWorkerDispatcher local = NewLocal(executor: executor.Object);

        IRemoteWorker beast = MakeRemoteWorkerWithResult(
            id: "beast",
            slots: 8,
            result: new(
                TaskId: "t0",
                Success: true,
                OutputPath: "/remote/out/t0",
                Duration: TimeSpan.FromSeconds(seconds: 5),
                WorkerId: "beast"
            )
        );

        FakeRegistry registry = new(workers: [beast]);
        RemoteWorkerDispatcher sut = NewRemote(local: local, registry: registry);

        DispatchResult[] results = await sut.DispatchAsync(
            tasks: [MakeTask(id: "t0")],
            ct: CancellationToken.None
        );

        results.Should().HaveCount(expected: 1);
        results[0].Success.Should().BeTrue();
        results[0].WorkerId.Should().Be(expected: "beast");
        results[0].OutputPath.Should().Be(expected: "/remote/out/t0");
    }

    [Fact]
    public async Task DispatchAsync_WorkerReturnsFailure_FallsBackToLocalForThatTask()
    {
        // A worker that reports failure (not throws) must trigger local
        // retry for that specific task. The whole job shouldn't fail
        // because one worker went sideways.
        Mock<IFfmpegExecutor> executor = MakeExecutor(succeed: true);
        LocalWorkerDispatcher local = NewLocal(executor: executor.Object);

        IRemoteWorker brokenWorker = MakeRemoteWorkerWithResult(
            id: "broken",
            slots: 4,
            result: new(
                TaskId: "t0",
                Success: false,
                OutputPath: "",
                Duration: TimeSpan.Zero,
                Error: "Worker OOM"
            )
        );

        FakeRegistry registry = new(workers: [brokenWorker]);
        RemoteWorkerDispatcher sut = NewRemote(local: local, registry: registry);

        DispatchResult[] results = await sut.DispatchAsync(
            tasks: [MakeTask(id: "t0")],
            ct: CancellationToken.None
        );

        // Local fallback succeeded, so the final result should be success.
        results[0].Success.Should().BeTrue();
    }

    [Fact]
    public async Task DispatchAsync_WorkerThrows_FallsBackToLocalForThatTask()
    {
        // Network failures / timeouts surface as thrown exceptions.
        // Dispatcher must swallow, log, and fall back — not fail the job.
        Mock<IFfmpegExecutor> executor = MakeExecutor(succeed: true);
        LocalWorkerDispatcher local = NewLocal(executor: executor.Object);

        Mock<IRemoteWorker> flaky = new();
        flaky.SetupGet(expression: w => w.WorkerId).Returns(value: "flaky");
        flaky.Setup(expression: w => w.GetAvailableBudget()).Returns(value: new ResourceBudgetSnapshot(AvailableGpuSlots: 0, AvailableCpuThreads: 4, GpuUtilization: 0));
        flaky
            .Setup(expression: w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception: new HttpRequestException(message: "connection refused"));

        FakeRegistry registry = new(workers: [flaky.Object]);
        RemoteWorkerDispatcher sut = NewRemote(local: local, registry: registry);

        DispatchResult[] results = await sut.DispatchAsync(
            tasks: [MakeTask(id: "t0")],
            ct: CancellationToken.None
        );

        results[0].Success.Should().BeTrue();
    }

    [Fact]
    public async Task DispatchAsync_FirstWorkerFails_RetriesOnAnotherWorker_NotLocal()
    {
        // Worker A fails, Worker B succeeds. Dispatcher must reach B
        // BEFORE giving up to local fallback. Local must not be touched.
        Mock<IFfmpegExecutor> localExec = MakeExecutor(succeed: true);
        int localCalls = 0;
        localExec
            .Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(action: () => Interlocked.Increment(location: ref localCalls))
            .ReturnsAsync(
                value: new ExecutionResult(
                    Success: true,
                    ExitCode: 0,
                    StdErr: "",
                    Duration: TimeSpan.FromSeconds(seconds: 1),
                    Error: null
                )
            );
        LocalWorkerDispatcher local = NewLocal(executor: localExec.Object);

        IRemoteWorker failing = MakeRemoteWorkerWithResult(
            id: "a-broken",
            slots: 8,
            result: new(TaskId: "t0", Success: false, OutputPath: "", Duration: TimeSpan.Zero, Error: "boom")
        );
        IRemoteWorker rescue = MakeRemoteWorkerWithResult(
            id: "b-healthy",
            slots: 2,
            result: new(TaskId: "t0", Success: true, OutputPath: "/b/t0", Duration: TimeSpan.FromSeconds(seconds: 2), WorkerId: "b-healthy")
        );

        FakeRegistry registry = new(workers: [failing, rescue]);
        RemoteWorkerDispatcher sut = NewRemote(local: local, registry: registry);

        DispatchResult[] results = await sut.DispatchAsync(
            tasks: [MakeTask(id: "t0")],
            ct: CancellationToken.None
        );

        results.Should().HaveCount(expected: 1);
        results[0].Success.Should().BeTrue();
        results[0].WorkerId.Should().Be(expected: "b-healthy", because: "retry must land on the alternate worker");
        localCalls.Should().Be(expected: 0, because: "local fallback must not run when a remote retry succeeds");
    }

    [Fact]
    public async Task DispatchAsync_MultipleTasks_DistributesAcrossWorkers()
    {
        // Two workers with different weights, four tasks. The assigner
        // load-balances; every task must complete, every worker that
        // got work must have received its ExecuteTaskAsync call.
        Mock<IFfmpegExecutor> executor = MakeExecutor(succeed: true);
        LocalWorkerDispatcher local = NewLocal(executor: executor.Object);

        int beastCalls = 0;
        int laptopCalls = 0;

        IRemoteWorker beast = MakeDynamicWorker(
            id: "beast",
            slots: 8,
            producer: t =>
            {
                Interlocked.Increment(location: ref beastCalls);
                return new(
                    TaskId: t.TaskId,
                    Success: true,
                    OutputPath: $"/beast/{t.TaskId}",
                    Duration: TimeSpan.FromSeconds(seconds: 1),
                    WorkerId: "beast"
                );
            }
        );
        IRemoteWorker laptop = MakeDynamicWorker(
            id: "laptop",
            slots: 2,
            producer: t =>
            {
                Interlocked.Increment(location: ref laptopCalls);
                return new(
                    TaskId: t.TaskId,
                    Success: true,
                    OutputPath: $"/laptop/{t.TaskId}",
                    Duration: TimeSpan.FromSeconds(seconds: 1),
                    WorkerId: "laptop"
                );
            }
        );

        FakeRegistry registry = new(workers: [beast, laptop]);
        RemoteWorkerDispatcher sut = NewRemote(local: local, registry: registry);

        EncodeTask[] tasks = Enumerable.Range(start: 0, count: 4).Select(selector: i => MakeTask(id: $"t{i}")).ToArray();

        DispatchResult[] results = await sut.DispatchAsync(tasks: tasks, ct: CancellationToken.None);

        results.Should().HaveCount(expected: 4);
        results.Should().AllSatisfy(expected: r => r.Success.Should().BeTrue());
        (beastCalls + laptopCalls).Should().Be(expected: 4);
        beastCalls.Should().BeGreaterThan(expected: 0, because: "higher-weight worker should see more tasks");
    }

    [Fact]
    public async Task DispatchAsync_CancellationPropagates()
    {
        Mock<IFfmpegExecutor> executor = MakeExecutor(succeed: true);
        LocalWorkerDispatcher local = NewLocal(executor: executor.Object);

        Mock<IRemoteWorker> slow = new();
        slow.SetupGet(expression: w => w.WorkerId).Returns(value: "slow");
        slow.Setup(expression: w => w.GetAvailableBudget()).Returns(value: new ResourceBudgetSnapshot(AvailableGpuSlots: 0, AvailableCpuThreads: 4, GpuUtilization: 0));
        slow.Setup(expression: w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .Returns(
                valueFunction: async (EncodeTask t, CancellationToken ct) =>
                {
                    await Task.Delay(millisecondsDelay: Timeout.Infinite, cancellationToken: ct);
                    return new(TaskId: t.TaskId, Success: true, OutputPath: "", Duration: TimeSpan.Zero);
                }
            );

        FakeRegistry registry = new(workers: [slow.Object]);
        RemoteWorkerDispatcher sut = NewRemote(local: local, registry: registry);

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        Func<Task> act = () => sut.DispatchAsync(tasks: [MakeTask(id: "t0")], ct: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void AvailableWorkerCount_ReflectsRegistryOrLocal()
    {
        Mock<IFfmpegExecutor> executor = MakeExecutor(succeed: true);
        LocalWorkerDispatcher local = NewLocal(executor: executor.Object);

        FakeRegistry registry = new(workers:
        [
            MakeRemoteWorker(id: "a"),
            MakeRemoteWorker(id: "b"),
            MakeRemoteWorker(id: "c"),
        ]);

        RemoteWorkerDispatcher sut = NewRemote(local: local, registry: registry);

        sut.AvailableWorkerCount.Should().BeGreaterThanOrEqualTo(expected: 3);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private sealed class FakeRegistry(IReadOnlyList<IRemoteWorker> workers) : IRemoteWorkerRegistry
    {
        public IReadOnlyList<IRemoteWorker> GetActiveWorkers() => workers;
    }

    private static LocalWorkerDispatcher NewLocal(IFfmpegExecutor executor) =>
        new(executor: executor, logger: NullLogger<LocalWorkerDispatcher>.Instance);

    private static RemoteWorkerDispatcher NewRemote(
        LocalWorkerDispatcher local,
        IRemoteWorkerRegistry registry
    ) => new(registry: registry, assigner: new WorkerAssigner(), localFallback: local, logger: NullLogger<RemoteWorkerDispatcher>.Instance);

    private static IRemoteWorker MakeRemoteWorker(string id, int slots = 4) =>
        MakeRemoteWorkerWithResult(
            id: id,
            slots: slots,
            result: new(
                TaskId: "placeholder",
                Success: true,
                OutputPath: $"/remote/{id}/placeholder",
                Duration: TimeSpan.FromSeconds(seconds: 1),
                WorkerId: id
            )
        );

    private static IRemoteWorker MakeRemoteWorkerWithResult(
        string id,
        int slots,
        DispatchResult result
    )
    {
        Mock<IRemoteWorker> mock = new();
        mock.SetupGet(expression: w => w.WorkerId).Returns(value: id);
        mock.Setup(expression: w => w.GetAvailableBudget())
            .Returns(
                value: new ResourceBudgetSnapshot(
                    AvailableGpuSlots: 0,
                    AvailableCpuThreads: slots,
                    GpuUtilization: 0
                )
            );
        mock.Setup(expression: w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: result);
        return mock.Object;
    }

    private static IRemoteWorker MakeDynamicWorker(
        string id,
        int slots,
        Func<EncodeTask, DispatchResult> producer
    )
    {
        Mock<IRemoteWorker> mock = new();
        mock.SetupGet(expression: w => w.WorkerId).Returns(value: id);
        mock.Setup(expression: w => w.GetAvailableBudget()).Returns(value: new ResourceBudgetSnapshot(AvailableGpuSlots: 0, AvailableCpuThreads: slots, GpuUtilization: 0));
        mock.Setup(expression: w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .Returns(valueFunction: (EncodeTask t, CancellationToken _) => Task.FromResult(result: producer(arg: t)));
        return mock.Object;
    }

    private static Mock<IFfmpegExecutor> MakeExecutor(bool succeed)
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
                    Success: succeed,
                    ExitCode: succeed ? 0 : 1,
                    StdErr: "",
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
}
