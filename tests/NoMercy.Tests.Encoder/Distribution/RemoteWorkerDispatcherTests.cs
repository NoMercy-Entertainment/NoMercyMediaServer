namespace NoMercy.Tests.Encoder.Distribution;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Distribution;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Jobs;

public class RemoteWorkerDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_NoRemoteWorkers_FallsBackToLocal()
    {
        Mock<IFfmpegExecutor> executor = MakeExecutor(succeed: true);
        LocalWorkerDispatcher local = NewLocal(executor.Object);
        RemoteWorkerDispatcher sut = NewRemote(local, new EmptyRemoteWorkerRegistry());

        DispatchResult[] results = await sut.DispatchAsync(
            [MakeTask("t0")],
            CancellationToken.None
        );

        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
    }

    [Fact]
    public async Task DispatchAsync_WorkerSucceeds_UsesWorkerResult()
    {
        // Worker returns a successful DispatchResult — dispatcher must
        // return that directly, not the local fallback. WorkerId on the
        // result proves the worker was the one that executed.
        Mock<IFfmpegExecutor> executor = MakeExecutor(succeed: true);
        LocalWorkerDispatcher local = NewLocal(executor.Object);

        IRemoteWorker beast = MakeRemoteWorkerWithResult(
            "beast",
            slots: 8,
            result: new(
                TaskId: "t0",
                Success: true,
                OutputPath: "/remote/out/t0",
                Duration: TimeSpan.FromSeconds(5),
                WorkerId: "beast"
            )
        );

        FakeRegistry registry = new([beast]);
        RemoteWorkerDispatcher sut = NewRemote(local, registry);

        DispatchResult[] results = await sut.DispatchAsync(
            [MakeTask("t0")],
            CancellationToken.None
        );

        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        results[0].WorkerId.Should().Be("beast");
        results[0].OutputPath.Should().Be("/remote/out/t0");
    }

    [Fact]
    public async Task DispatchAsync_WorkerReturnsFailure_FallsBackToLocalForThatTask()
    {
        // A worker that reports failure (not throws) must trigger local
        // retry for that specific task. The whole job shouldn't fail
        // because one worker went sideways.
        Mock<IFfmpegExecutor> executor = MakeExecutor(succeed: true);
        LocalWorkerDispatcher local = NewLocal(executor.Object);

        IRemoteWorker brokenWorker = MakeRemoteWorkerWithResult(
            "broken",
            slots: 4,
            result: new(
                TaskId: "t0",
                Success: false,
                OutputPath: "",
                Duration: TimeSpan.Zero,
                Error: "Worker OOM"
            )
        );

        FakeRegistry registry = new([brokenWorker]);
        RemoteWorkerDispatcher sut = NewRemote(local, registry);

        DispatchResult[] results = await sut.DispatchAsync(
            [MakeTask("t0")],
            CancellationToken.None
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
        LocalWorkerDispatcher local = NewLocal(executor.Object);

        Mock<IRemoteWorker> flaky = new();
        flaky.SetupGet(w => w.WorkerId).Returns("flaky");
        flaky.Setup(w => w.GetAvailableBudget()).Returns(new ResourceBudgetSnapshot(0, 4, 0));
        flaky
            .Setup(w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        FakeRegistry registry = new([flaky.Object]);
        RemoteWorkerDispatcher sut = NewRemote(local, registry);

        DispatchResult[] results = await sut.DispatchAsync(
            [MakeTask("t0")],
            CancellationToken.None
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
            .Setup(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<NoMercy.Encoder.Progress.EncodingProgress>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => Interlocked.Increment(ref localCalls))
            .ReturnsAsync(
                new ExecutionResult(
                    Success: true,
                    ExitCode: 0,
                    StdErr: "",
                    Duration: TimeSpan.FromSeconds(1),
                    Error: null
                )
            );
        LocalWorkerDispatcher local = NewLocal(localExec.Object);

        IRemoteWorker failing = MakeRemoteWorkerWithResult(
            "a-broken",
            slots: 8,
            result: new("t0", false, "", TimeSpan.Zero, "boom")
        );
        IRemoteWorker rescue = MakeRemoteWorkerWithResult(
            "b-healthy",
            slots: 2,
            result: new(
                "t0",
                true,
                "/b/t0",
                TimeSpan.FromSeconds(2),
                WorkerId: "b-healthy"
            )
        );

        FakeRegistry registry = new([failing, rescue]);
        RemoteWorkerDispatcher sut = NewRemote(local, registry);

        DispatchResult[] results = await sut.DispatchAsync(
            [MakeTask("t0")],
            CancellationToken.None
        );

        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        results[0].WorkerId.Should().Be("b-healthy", "retry must land on the alternate worker");
        localCalls.Should().Be(0, "local fallback must not run when a remote retry succeeds");
    }

    [Fact]
    public async Task DispatchAsync_MultipleTasks_DistributesAcrossWorkers()
    {
        // Two workers with different weights, four tasks. The assigner
        // load-balances; every task must complete, every worker that
        // got work must have received its ExecuteTaskAsync call.
        Mock<IFfmpegExecutor> executor = MakeExecutor(succeed: true);
        LocalWorkerDispatcher local = NewLocal(executor.Object);

        int beastCalls = 0;
        int laptopCalls = 0;

        IRemoteWorker beast = MakeDynamicWorker(
            "beast",
            8,
            t =>
            {
                Interlocked.Increment(ref beastCalls);
                return new(
                    t.TaskId,
                    true,
                    $"/beast/{t.TaskId}",
                    TimeSpan.FromSeconds(1),
                    WorkerId: "beast"
                );
            }
        );
        IRemoteWorker laptop = MakeDynamicWorker(
            "laptop",
            2,
            t =>
            {
                Interlocked.Increment(ref laptopCalls);
                return new(
                    t.TaskId,
                    true,
                    $"/laptop/{t.TaskId}",
                    TimeSpan.FromSeconds(1),
                    WorkerId: "laptop"
                );
            }
        );

        FakeRegistry registry = new([beast, laptop]);
        RemoteWorkerDispatcher sut = NewRemote(local, registry);

        EncodeTask[] tasks = Enumerable.Range(0, 4).Select(i => MakeTask($"t{i}")).ToArray();

        DispatchResult[] results = await sut.DispatchAsync(tasks, CancellationToken.None);

        results.Should().HaveCount(4);
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
        (beastCalls + laptopCalls).Should().Be(4);
        beastCalls.Should().BeGreaterThan(0, "higher-weight worker should see more tasks");
    }

    [Fact]
    public async Task DispatchAsync_CancellationPropagates()
    {
        Mock<IFfmpegExecutor> executor = MakeExecutor(succeed: true);
        LocalWorkerDispatcher local = NewLocal(executor.Object);

        Mock<IRemoteWorker> slow = new();
        slow.SetupGet(w => w.WorkerId).Returns("slow");
        slow.Setup(w => w.GetAvailableBudget()).Returns(new ResourceBudgetSnapshot(0, 4, 0));
        slow.Setup(w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .Returns(
                async (EncodeTask t, CancellationToken ct) =>
                {
                    await Task.Delay(Timeout.Infinite, ct);
                    return new(t.TaskId, true, "", TimeSpan.Zero);
                }
            );

        FakeRegistry registry = new([slow.Object]);
        RemoteWorkerDispatcher sut = NewRemote(local, registry);

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        Func<Task> act = () => sut.DispatchAsync([MakeTask("t0")], cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void AvailableWorkerCount_ReflectsRegistryOrLocal()
    {
        Mock<IFfmpegExecutor> executor = MakeExecutor(succeed: true);
        LocalWorkerDispatcher local = NewLocal(executor.Object);

        FakeRegistry registry = new([
            MakeRemoteWorker("a"),
            MakeRemoteWorker("b"),
            MakeRemoteWorker("c"),
        ]);

        RemoteWorkerDispatcher sut = NewRemote(local, registry);

        sut.AvailableWorkerCount.Should().BeGreaterThanOrEqualTo(3);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private sealed class FakeRegistry(IReadOnlyList<IRemoteWorker> workers) : IRemoteWorkerRegistry
    {
        public IReadOnlyList<IRemoteWorker> GetActiveWorkers() => workers;
    }

    private static LocalWorkerDispatcher NewLocal(IFfmpegExecutor executor) =>
        new(executor, NullLogger<LocalWorkerDispatcher>.Instance);

    private static RemoteWorkerDispatcher NewRemote(
        LocalWorkerDispatcher local,
        IRemoteWorkerRegistry registry
    ) => new(registry, new WorkerAssigner(), local, NullLogger<RemoteWorkerDispatcher>.Instance);

    private static IRemoteWorker MakeRemoteWorker(string id, int slots = 4) =>
        MakeRemoteWorkerWithResult(
            id,
            slots,
            new(
                TaskId: "placeholder",
                Success: true,
                OutputPath: $"/remote/{id}/placeholder",
                Duration: TimeSpan.FromSeconds(1),
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
        mock.SetupGet(w => w.WorkerId).Returns(id);
        mock.Setup(w => w.GetAvailableBudget())
            .Returns(
                new ResourceBudgetSnapshot(
                    AvailableGpuSlots: 0,
                    AvailableCpuThreads: slots,
                    GpuUtilization: 0
                )
            );
        mock.Setup(w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return mock.Object;
    }

    private static IRemoteWorker MakeDynamicWorker(
        string id,
        int slots,
        Func<EncodeTask, DispatchResult> producer
    )
    {
        Mock<IRemoteWorker> mock = new();
        mock.SetupGet(w => w.WorkerId).Returns(id);
        mock.Setup(w => w.GetAvailableBudget()).Returns(new ResourceBudgetSnapshot(0, slots, 0));
        mock.Setup(w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .Returns((EncodeTask t, CancellationToken _) => Task.FromResult(producer(t)));
        return mock.Object;
    }

    private static Mock<IFfmpegExecutor> MakeExecutor(bool succeed)
    {
        Mock<IFfmpegExecutor> mock = new();
        mock.Setup(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<NoMercy.Encoder.Progress.EncodingProgress>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ExecutionResult(
                    Success: succeed,
                    ExitCode: succeed ? 0 : 1,
                    StdErr: "",
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
}
