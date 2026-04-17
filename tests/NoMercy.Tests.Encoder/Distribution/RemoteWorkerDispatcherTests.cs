namespace NoMercy.Tests.Encoder.Distribution;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Distribution;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Hardware;
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
    public async Task DispatchAsync_WithRemoteWorkers_StillFallsBackToLocal_UntilProtocolImplemented()
    {
        // Protocol isn't implemented yet — even when remote workers are
        // registered, results today come from the local dispatcher. The
        // test locks in that contract so future refactors don't silently
        // regress installs that happen to have a stray worker registered.
        Mock<IFfmpegExecutor> executor = MakeExecutor(succeed: true);
        LocalWorkerDispatcher local = NewLocal(executor.Object);

        FakeRegistry registry = new([
            MakeRemoteWorker("beast", slots: 8),
            MakeRemoteWorker("laptop", slots: 2),
        ]);

        RemoteWorkerDispatcher sut = NewRemote(local, registry);

        DispatchResult[] results = await sut.DispatchAsync(
            [MakeTask("t0"), MakeTask("t1")],
            CancellationToken.None
        );

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
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

    private static IRemoteWorker MakeRemoteWorker(string id, int slots = 4)
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
            Command: new FfmpegCommand("ffmpeg", ["-i", "in.mkv", "out.ts"], null),
            OutputPath: $"/out/{id}",
            Type: EncodeTaskType.QualityVariant
        );
}
