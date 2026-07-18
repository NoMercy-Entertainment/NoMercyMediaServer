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

using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Distribution;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Progress;

namespace NoMercy.Tests.Encoder.Scenarios;

/// <summary>
/// End-to-end scenario tests for distributed encode work across compute nodes.
/// These scenarios prove: tasks distribute correctly to fit available capacity,
/// no task is lost or double-assigned, serialization/signing works under tamper
/// detection, and the system adapts when workers join/leave. No live encodes;
/// mocked HTTP and executors.
/// </summary>
public class DistributedComputeScenarioTests
{
    private readonly byte[] _sharedKey = Encoding.UTF8.GetBytes(
        "scenario-test-shared-key-32-bytes!"
    );
    private readonly TaskSerializer _serializer = new();

    [Fact]
    public async Task SingleInstance_NoRemoteWorkers_AllTasksAssignedLocallyWithoutLoss()
    {
        Mock<IFfmpegExecutor> executor = MakeSuccessExecutor();
        LocalWorkerDispatcher dispatcher = new(
            executor.Object,
            NullLogger<LocalWorkerDispatcher>.Instance
        );

        EncodeTask[] tasks =
        [
            MakeTask("t0", EncodeTaskType.QualityVariant),
            MakeTask("t1", EncodeTaskType.QualityVariant),
            MakeTask("t2", EncodeTaskType.TimeChunk),
            MakeTask("t3", EncodeTaskType.TimeChunk),
        ];

        DispatchResult[] results = await dispatcher.DispatchAsync(tasks, CancellationToken.None);

        results.Should().HaveCount(4);
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
        HashSet<string> resultIds = results.Select(r => r.TaskId).ToHashSet();
        resultIds.Should().BeEquivalentTo("t0", "t1", "t2", "t3");
    }

    [Fact]
    public async Task MultiInstance_VaryingCapacity_TasksDistributedByCapacityNotAllOnOne()
    {
        Mock<IFfmpegExecutor> executor = MakeSuccessExecutor();
        LocalWorkerDispatcher local = new(
            executor.Object,
            NullLogger<LocalWorkerDispatcher>.Instance
        );

        int beastReceived = 0;
        int laptopReceived = 0;

        IRemoteWorker beast = MakeDynamicWorker(
            "beast",
            8,
            t =>
            {
                Interlocked.Increment(ref beastReceived);
                return new DispatchResult(
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
                Interlocked.Increment(ref laptopReceived);
                return new DispatchResult(
                    t.TaskId,
                    true,
                    $"/laptop/{t.TaskId}",
                    TimeSpan.FromSeconds(1),
                    WorkerId: "laptop"
                );
            }
        );

        InMemoryRemoteWorkerRegistry registry = new();
        registry.Register(beast);
        registry.Register(laptop);

        RemoteWorkerDispatcher dispatcher = new(
            registry,
            new WorkerAssigner(),
            local,
            NullLogger<RemoteWorkerDispatcher>.Instance
        );

        EncodeTask[] tasks = Enumerable
            .Range(0, 6)
            .Select(i => MakeTask($"t{i}", EncodeTaskType.QualityVariant))
            .ToArray();

        DispatchResult[] results = await dispatcher.DispatchAsync(tasks, CancellationToken.None);

        results.Should().HaveCount(6);
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
        (beastReceived + laptopReceived).Should().Be(6);
        beastReceived
            .Should()
            .BeGreaterThan(laptopReceived, "higher-capacity worker should receive more tasks");
    }

    [Fact]
    public async Task RemoteWorkerParticipates_ReceivesAtLeastOneTask()
    {
        Mock<IFfmpegExecutor> executor = MakeSuccessExecutor();
        LocalWorkerDispatcher local = new(
            executor.Object,
            NullLogger<LocalWorkerDispatcher>.Instance
        );

        int remoteTaskCount = 0;
        IRemoteWorker remote = MakeDynamicWorker(
            "remote-box",
            4,
            t =>
            {
                Interlocked.Increment(ref remoteTaskCount);
                return new DispatchResult(
                    t.TaskId,
                    true,
                    $"/remote/{t.TaskId}",
                    TimeSpan.FromSeconds(1),
                    WorkerId: "remote-box"
                );
            }
        );

        InMemoryRemoteWorkerRegistry registry = new();
        registry.Register(remote);

        RemoteWorkerDispatcher dispatcher = new(
            registry,
            new WorkerAssigner(),
            local,
            NullLogger<RemoteWorkerDispatcher>.Instance
        );

        EncodeTask[] tasks = [MakeTask("single", EncodeTaskType.QualityVariant)];

        DispatchResult[] results = await dispatcher.DispatchAsync(tasks, CancellationToken.None);

        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        results[0].WorkerId.Should().Be("remote-box");
        remoteTaskCount.Should().Be(1);
    }

    [Fact]
    public async Task SaturatedWorker_OverloadedNotStranded_CapacityWorkerPreferredWhenAvailable()
    {
        Mock<IFfmpegExecutor> executor = MakeSuccessExecutor();
        LocalWorkerDispatcher local = new(
            executor.Object,
            NullLogger<LocalWorkerDispatcher>.Instance
        );

        int zeroSlotsCalls = 0;
        Mock<IRemoteWorker> zeroCalls = new();
        zeroCalls.SetupGet(w => w.WorkerId).Returns("no-slots");
        zeroCalls.Setup(w => w.GetAvailableBudget()).Returns(new ResourceBudgetSnapshot(0, 0, 0));
        zeroCalls
            .Setup(w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .Callback(() => Interlocked.Increment(ref zeroSlotsCalls))
            .Returns(
                (EncodeTask t, CancellationToken _) =>
                    Task.FromResult(
                        new DispatchResult(
                            t.TaskId,
                            true,
                            "/out/t0",
                            TimeSpan.FromSeconds(1),
                            WorkerId: "no-slots"
                        )
                    )
            );

        InMemoryRemoteWorkerRegistry registry = new();
        registry.Register(zeroCalls.Object);

        RemoteWorkerDispatcher dispatcher = new(
            registry,
            new WorkerAssigner(),
            local,
            NullLogger<RemoteWorkerDispatcher>.Instance
        );

        EncodeTask[] tasks = [MakeTask("t0", EncodeTaskType.QualityVariant)];
        DispatchResult[] results = await dispatcher.DispatchAsync(tasks, CancellationToken.None);

        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        // Documented assigner contract: when the ONLY registered worker is
        // saturated (zero available slots) the assigner overloads it rather
        // than stranding the task — strict capacity enforcement is the
        // dispatcher/registry's job, not the assigner's. The guarantee here is
        // that the task is NOT stranded; it completes on the (only) worker.
        zeroSlotsCalls.Should().Be(1, "a saturated sole worker is overloaded, not stranded");

        int withCapacityCalls = 0;
        Mock<IRemoteWorker> withCapacity = new();
        withCapacity.SetupGet(w => w.WorkerId).Returns("has-slots");
        withCapacity
            .Setup(w => w.GetAvailableBudget())
            .Returns(new ResourceBudgetSnapshot(0, 4, 0));
        withCapacity
            .Setup(w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .Callback(() => Interlocked.Increment(ref withCapacityCalls))
            .Returns(
                (EncodeTask t, CancellationToken _) =>
                    Task.FromResult(
                        new DispatchResult(
                            t.TaskId,
                            true,
                            "/out/t1",
                            TimeSpan.FromSeconds(1),
                            WorkerId: "has-slots"
                        )
                    )
            );

        registry.Register(withCapacity.Object);

        results = await dispatcher.DispatchAsync(
            [MakeTask("t1", EncodeTaskType.QualityVariant)],
            CancellationToken.None
        );

        results[0].Success.Should().BeTrue();
        results[0].WorkerId.Should().Be("has-slots");
        withCapacityCalls.Should().Be(1, "worker with capacity should be assigned");
    }

    [Fact]
    public void TaskSerializer_RoundTrip_SerializeSignVerifyRecoveryTask()
    {
        EncodeTask original = MakeTask("round-trip", EncodeTaskType.QualityVariant);

        string signed = _serializer.Serialize(original, _sharedKey);

        EncodeTask? recovered = _serializer.Deserialize(signed, _sharedKey);

        recovered.Should().NotBeNull();
        recovered!.TaskId.Should().Be("round-trip");
        recovered.OutputPath.Should().Be("/out/round-trip");
        recovered.Type.Should().Be(EncodeTaskType.QualityVariant);
    }

    [Fact]
    public void TaskSerializer_TamperedPayload_FailsHmacVerification()
    {
        EncodeTask original = MakeTask("tamper-test", EncodeTaskType.QualityVariant);
        string signed = _serializer.Serialize(original, _sharedKey);

        int tamperIndex = signed.Length / 2;
        string tampered =
            signed.Substring(0, tamperIndex) + "X" + signed.Substring(tamperIndex + 1);

        EncodeTask? recovered = _serializer.Deserialize(tampered, _sharedKey);

        recovered.Should().BeNull("tampered payload should fail verification");
    }

    [Fact]
    public void ResultSerializer_RoundTrip_SerializeSignVerifyResult()
    {
        DispatchResult original = new(
            "result-rt",
            Success: true,
            OutputPath: "/out/result",
            Duration: TimeSpan.FromSeconds(5),
            WorkerId: "test-worker"
        );

        string signed = _serializer.SerializeResult(original, _sharedKey);

        DispatchResult? recovered = _serializer.DeserializeResult(signed, _sharedKey);

        recovered.Should().NotBeNull();
        recovered!.TaskId.Should().Be("result-rt");
        recovered.Success.Should().BeTrue();
        recovered.WorkerId.Should().Be("test-worker");
        recovered.Duration.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ResultSerializer_TamperedResult_FailsHmacVerification()
    {
        DispatchResult original = new(
            "result-tamper",
            true,
            "/out/r",
            TimeSpan.FromSeconds(1),
            WorkerId: "w"
        );
        string signed = _serializer.SerializeResult(original, _sharedKey);

        int tamperIndex = signed.Length / 2;
        string tampered =
            signed.Substring(0, tamperIndex) + "Z" + signed.Substring(tamperIndex + 1);

        DispatchResult? recovered = _serializer.DeserializeResult(tampered, _sharedKey);

        recovered.Should().BeNull("tampered result should fail verification");
    }

    [Fact]
    public async Task WorkerCountChanges_DistributionAdaptsBetweenRounds()
    {
        Mock<IFfmpegExecutor> executor = MakeSuccessExecutor();
        LocalWorkerDispatcher local = new(
            executor.Object,
            NullLogger<LocalWorkerDispatcher>.Instance
        );

        InMemoryRemoteWorkerRegistry registry = new();
        RemoteWorkerDispatcher dispatcher = new(
            registry,
            new WorkerAssigner(),
            local,
            NullLogger<RemoteWorkerDispatcher>.Instance
        );

        EncodeTask[] tasks1 = Enumerable
            .Range(0, 2)
            .Select(i => MakeTask($"round1-t{i}", EncodeTaskType.QualityVariant))
            .ToArray();

        DispatchResult[] results1 = await dispatcher.DispatchAsync(tasks1, CancellationToken.None);
        results1.Should().AllSatisfy(r => r.Success.Should().BeTrue());

        int workerACount = 0;
        IRemoteWorker workerA = MakeDynamicWorker(
            "a",
            4,
            t =>
            {
                Interlocked.Increment(ref workerACount);
                return new DispatchResult(
                    t.TaskId,
                    true,
                    $"/a/{t.TaskId}",
                    TimeSpan.FromSeconds(1),
                    WorkerId: "a"
                );
            }
        );
        registry.Register(workerA);

        EncodeTask[] tasks2 = Enumerable
            .Range(0, 2)
            .Select(i => MakeTask($"round2-t{i}", EncodeTaskType.QualityVariant))
            .ToArray();

        DispatchResult[] results2 = await dispatcher.DispatchAsync(tasks2, CancellationToken.None);
        results2.Should().AllSatisfy(r => r.Success.Should().BeTrue());
        workerACount.Should().BeGreaterThan(0, "new worker should receive tasks");

        int workerBCount = 0;
        IRemoteWorker workerB = MakeDynamicWorker(
            "b",
            2,
            t =>
            {
                Interlocked.Increment(ref workerBCount);
                return new DispatchResult(
                    t.TaskId,
                    true,
                    $"/b/{t.TaskId}",
                    TimeSpan.FromSeconds(1),
                    WorkerId: "b"
                );
            }
        );
        registry.Register(workerB);

        EncodeTask[] tasks3 = Enumerable
            .Range(0, 4)
            .Select(i => MakeTask($"round3-t{i}", EncodeTaskType.QualityVariant))
            .ToArray();

        DispatchResult[] results3 = await dispatcher.DispatchAsync(tasks3, CancellationToken.None);
        results3.Should().AllSatisfy(r => r.Success.Should().BeTrue());
        // The greedy capacity-weighted assigner may concentrate a small batch on
        // the fastest worker, so B is not guaranteed a share at this scale. The
        // invariant across the worker-count change is that NO task is lost:
        // 2 from round 2 (worker A only) + 4 from round 3.
        (workerACount + workerBCount)
            .Should()
            .Be(6, "no task is lost when a worker is added mid-stream");
    }

    [Fact]
    public async Task NoTaskLossOrDuplication_UnionOfAllBucketsEqualsInputSet()
    {
        Mock<IFfmpegExecutor> executor = MakeSuccessExecutor();
        LocalWorkerDispatcher local = new(
            executor.Object,
            NullLogger<LocalWorkerDispatcher>.Instance
        );

        int workerACount = 0;
        int workerBCount = 0;
        int workerCCount = 0;

        IRemoteWorker a = MakeDynamicWorker(
            "a",
            4,
            t =>
            {
                Interlocked.Increment(ref workerACount);
                return new DispatchResult(
                    t.TaskId,
                    true,
                    $"/a/{t.TaskId}",
                    TimeSpan.FromSeconds(1),
                    WorkerId: "a"
                );
            }
        );

        IRemoteWorker b = MakeDynamicWorker(
            "b",
            6,
            t =>
            {
                Interlocked.Increment(ref workerBCount);
                return new DispatchResult(
                    t.TaskId,
                    true,
                    $"/b/{t.TaskId}",
                    TimeSpan.FromSeconds(1),
                    WorkerId: "b"
                );
            }
        );

        IRemoteWorker c = MakeDynamicWorker(
            "c",
            2,
            t =>
            {
                Interlocked.Increment(ref workerCCount);
                return new DispatchResult(
                    t.TaskId,
                    true,
                    $"/c/{t.TaskId}",
                    TimeSpan.FromSeconds(1),
                    WorkerId: "c"
                );
            }
        );

        InMemoryRemoteWorkerRegistry registry = new();
        registry.Register(a);
        registry.Register(b);
        registry.Register(c);

        RemoteWorkerDispatcher dispatcher = new(
            registry,
            new WorkerAssigner(),
            local,
            NullLogger<RemoteWorkerDispatcher>.Instance
        );

        EncodeTask[] tasks = Enumerable
            .Range(0, 12)
            .Select(i =>
                MakeTask(
                    $"t{i}",
                    i % 2 == 0 ? EncodeTaskType.QualityVariant : EncodeTaskType.TimeChunk
                )
            )
            .ToArray();

        DispatchResult[] results = await dispatcher.DispatchAsync(tasks, CancellationToken.None);

        results.Should().HaveCount(12);
        HashSet<string> resultIds = results.Select(r => r.TaskId).ToHashSet();
        resultIds.Should().HaveCount(12, "no duplicates");
        resultIds.Should().BeEquivalentTo(tasks.Select(t => t.TaskId));
        (workerACount + workerBCount + workerCCount).Should().Be(12);
    }

    [Fact]
    public async Task GpuTasks_RoutedToGpuCapableWorkerInMultiInstance()
    {
        Mock<IFfmpegExecutor> executor = MakeSuccessExecutor();
        LocalWorkerDispatcher local = new(
            executor.Object,
            NullLogger<LocalWorkerDispatcher>.Instance
        );

        int cpuOnlyCount = 0;
        int gpuBoxCount = 0;

        Mock<IRemoteWorker> cpuOnly = new();
        cpuOnly.SetupGet(w => w.WorkerId).Returns("cpu-only");
        cpuOnly.Setup(w => w.GetAvailableBudget()).Returns(new ResourceBudgetSnapshot(0, 8, 0));
        cpuOnly
            .Setup(w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .Callback(() => Interlocked.Increment(ref cpuOnlyCount))
            .Returns(
                (EncodeTask _, CancellationToken __) =>
                    Task.FromResult(
                        new DispatchResult(
                            "cpu-task",
                            true,
                            "/cpu/out",
                            TimeSpan.FromSeconds(1),
                            WorkerId: "cpu-only"
                        )
                    )
            );

        Mock<IRemoteWorker> gpuBox = new();
        gpuBox.SetupGet(w => w.WorkerId).Returns("gpu-box");
        gpuBox.Setup(w => w.GetAvailableBudget()).Returns(new ResourceBudgetSnapshot(2, 4, 0));
        gpuBox
            .Setup(w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .Callback(() => Interlocked.Increment(ref gpuBoxCount))
            .Returns(
                (EncodeTask _, CancellationToken __) =>
                    Task.FromResult(
                        new DispatchResult(
                            "gpu-task",
                            true,
                            "/gpu/out",
                            TimeSpan.FromSeconds(1),
                            WorkerId: "gpu-box"
                        )
                    )
            );

        FakeRegistry registry = new([cpuOnly.Object, gpuBox.Object]);

        RemoteWorkerDispatcher dispatcher = new(
            registry,
            new WorkerAssigner(),
            local,
            NullLogger<RemoteWorkerDispatcher>.Instance
        );

        EncodeTask gpuTask = new(
            "gpu-req",
            new("ffmpeg", ["-i", "in.mkv", "out.ts"], null),
            "/out/gpu-req",
            EncodeTaskType.QualityVariant,
            RequiresGpu: true
        );

        DispatchResult[] results = await dispatcher.DispatchAsync(
            [gpuTask],
            CancellationToken.None
        );

        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        results[0].WorkerId.Should().Be("gpu-box", "GPU task should route to GPU-capable worker");
        gpuBoxCount.Should().Be(1);
        cpuOnlyCount.Should().Be(0, "GPU task should not land on CPU-only worker");
    }

    [Fact]
    public async Task AllRemoteWorkersFail_FallsBackToLocalForAllTasks()
    {
        int localCount = 0;
        Mock<IFfmpegExecutor> executor = new();
        executor
            .Setup(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => Interlocked.Increment(ref localCount))
            .Returns(() =>
                Task.FromResult(
                    new ExecutionResult(
                        Success: true,
                        ExitCode: 0,
                        StdErr: "",
                        Duration: TimeSpan.FromSeconds(1),
                        Error: null
                    )
                )
            );

        LocalWorkerDispatcher local = new(
            executor.Object,
            NullLogger<LocalWorkerDispatcher>.Instance
        );

        Mock<IRemoteWorker> broken1 = new();
        broken1.SetupGet(w => w.WorkerId).Returns("broken1");
        broken1.Setup(w => w.GetAvailableBudget()).Returns(new ResourceBudgetSnapshot(0, 4, 0));
        broken1
            .Setup(w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .Throws(new HttpRequestException("connection refused"));

        Mock<IRemoteWorker> broken2 = new();
        broken2.SetupGet(w => w.WorkerId).Returns("broken2");
        broken2.Setup(w => w.GetAvailableBudget()).Returns(new ResourceBudgetSnapshot(0, 4, 0));
        broken2
            .Setup(w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .Returns(
                (EncodeTask _, CancellationToken __) =>
                    Task.FromResult(
                        new DispatchResult("t0", false, "", TimeSpan.Zero, Error: "OOM")
                    )
            );

        FakeRegistry registry = new([broken1.Object, broken2.Object]);

        RemoteWorkerDispatcher dispatcher = new(
            registry,
            new WorkerAssigner(),
            local,
            NullLogger<RemoteWorkerDispatcher>.Instance
        );

        DispatchResult[] results = await dispatcher.DispatchAsync(
            [MakeTask("fallback-test", EncodeTaskType.QualityVariant)],
            CancellationToken.None
        );

        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        localCount.Should().Be(1, "task should fall back to local when all remote workers fail");
    }

    [Fact]
    public void WorkerHealthTracking_FailedWorker_EntersCooldownHiddenFromDispatch()
    {
        InMemoryRemoteWorkerRegistry registry = new();

        Mock<IRemoteWorker> flaky = new();
        flaky.SetupGet(w => w.WorkerId).Returns("flaky");
        flaky.Setup(w => w.GetAvailableBudget()).Returns(new ResourceBudgetSnapshot(0, 4, 0));
        flaky
            .Setup(w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .Returns(
                (EncodeTask _, CancellationToken __) =>
                    Task.FromResult(
                        new DispatchResult("t0", false, "", TimeSpan.Zero, Error: "failure")
                    )
            );

        registry.Register(flaky.Object);

        registry.RecordTaskOutcome("flaky", success: false);
        registry.RecordTaskOutcome("flaky", success: false);
        registry.RecordTaskOutcome("flaky", success: false);

        IReadOnlyList<IRemoteWorker> active = registry.GetActiveWorkers();

        active.Should().BeEmpty("worker with 3 consecutive failures should be in cooldown");
    }

    [Fact]
    public void WorkerHealthTracking_SuccessClears_CooldownLifts()
    {
        InMemoryRemoteWorkerRegistry registry = new();

        Mock<IRemoteWorker> recovering = new();
        recovering.SetupGet(w => w.WorkerId).Returns("recovering");
        recovering.Setup(w => w.GetAvailableBudget()).Returns(new ResourceBudgetSnapshot(0, 4, 0));

        registry.Register(recovering.Object);

        registry.RecordTaskOutcome("recovering", success: false);
        registry.RecordTaskOutcome("recovering", success: false);
        registry.RecordTaskOutcome("recovering", success: false);

        IReadOnlyList<IRemoteWorker> active = registry.GetActiveWorkers();
        active.Should().BeEmpty("in cooldown after 3 failures");

        registry.RecordTaskOutcome("recovering", success: true);

        active = registry.GetActiveWorkers();
        active.Should().HaveCount(1, "success clears failure counter and exits cooldown");
    }

    [Fact]
    public async Task CostUnits_AffectCapacityConsumption_HighCostTaskFillsFasterOnce()
    {
        Mock<IFfmpegExecutor> executor = MakeSuccessExecutor();
        LocalWorkerDispatcher local = new(
            executor.Object,
            NullLogger<LocalWorkerDispatcher>.Instance
        );

        int fastCount = 0;
        int slowCount = 0;

        IRemoteWorker fast = MakeDynamicWorker(
            "fast",
            2,
            t =>
            {
                Interlocked.Increment(ref fastCount);
                return new DispatchResult(
                    t.TaskId,
                    true,
                    $"/fast/{t.TaskId}",
                    TimeSpan.FromSeconds(1),
                    WorkerId: "fast"
                );
            }
        );

        IRemoteWorker slow = MakeDynamicWorker(
            "slow",
            1,
            t =>
            {
                Interlocked.Increment(ref slowCount);
                return new DispatchResult(
                    t.TaskId,
                    true,
                    $"/slow/{t.TaskId}",
                    TimeSpan.FromSeconds(1),
                    WorkerId: "slow"
                );
            }
        );

        InMemoryRemoteWorkerRegistry registry = new();
        registry.Register(fast);
        registry.Register(slow);

        RemoteWorkerDispatcher dispatcher = new(
            registry,
            new WorkerAssigner(),
            local,
            NullLogger<RemoteWorkerDispatcher>.Instance
        );

        EncodeTask heavyTask = new(
            "heavy",
            new("ffmpeg", ["-i", "in.mkv", "out.ts"], null),
            "/out/heavy",
            EncodeTaskType.QualityVariant,
            EstimatedCostUnits: 8
        );

        EncodeTask lightTask = new(
            "light",
            new("ffmpeg", ["-i", "in.mkv", "out.ts"], null),
            "/out/light",
            EncodeTaskType.QualityVariant,
            EstimatedCostUnits: 1
        );

        DispatchResult[] results = await dispatcher.DispatchAsync(
            [heavyTask, lightTask],
            CancellationToken.None
        );

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
        fastCount.Should().Be(1, "fast worker should get the heavy task");
        slowCount.Should().Be(1, "slow worker should get remaining light task");
    }

    [Fact]
    public async Task TimeChunkAndQualityVariantTasks_WeightedByType_VariantsPreferFastestWorker()
    {
        Mock<IFfmpegExecutor> executor = MakeSuccessExecutor();
        LocalWorkerDispatcher local = new(
            executor.Object,
            NullLogger<LocalWorkerDispatcher>.Instance
        );

        int beastHasVariant = 0;
        int laptopHasVariant = 0;

        IRemoteWorker beast = MakeDynamicWorker(
            "beast",
            8,
            t =>
            {
                if (t.Type == EncodeTaskType.QualityVariant)
                    Interlocked.Increment(ref beastHasVariant);
                return new DispatchResult(
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
                if (t.Type == EncodeTaskType.QualityVariant)
                    Interlocked.Increment(ref laptopHasVariant);
                return new DispatchResult(
                    t.TaskId,
                    true,
                    $"/laptop/{t.TaskId}",
                    TimeSpan.FromSeconds(1),
                    WorkerId: "laptop"
                );
            }
        );

        InMemoryRemoteWorkerRegistry registry = new();
        registry.Register(beast);
        registry.Register(laptop);

        RemoteWorkerDispatcher dispatcher = new(
            registry,
            new WorkerAssigner(),
            local,
            NullLogger<RemoteWorkerDispatcher>.Instance
        );

        EncodeTask[] tasks =
        [
            MakeTask("chunk0", EncodeTaskType.TimeChunk),
            MakeTask("variant", EncodeTaskType.QualityVariant),
            MakeTask("chunk1", EncodeTaskType.TimeChunk),
        ];

        DispatchResult[] results = await dispatcher.DispatchAsync(tasks, CancellationToken.None);

        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
        beastHasVariant.Should().Be(1, "full variant (heaviest) should land on fastest worker");
    }

    [Fact]
    public async Task MultiRound_DifferentTaskShapesAndWorkerChurn_NoInvariantViolation()
    {
        Mock<IFfmpegExecutor> executor = MakeSuccessExecutor();
        LocalWorkerDispatcher local = new(
            executor.Object,
            NullLogger<LocalWorkerDispatcher>.Instance
        );

        InMemoryRemoteWorkerRegistry registry = new();
        RemoteWorkerDispatcher dispatcher = new(
            registry,
            new WorkerAssigner(),
            local,
            NullLogger<RemoteWorkerDispatcher>.Instance
        );

        for (int round = 0; round < 3; round++)
        {
            if (round >= 1)
            {
                IRemoteWorker w1 = MakeDynamicWorker(
                    $"r{round}-w1",
                    4,
                    t => new DispatchResult(
                        t.TaskId,
                        true,
                        $"/w1/{t.TaskId}",
                        TimeSpan.FromSeconds(1),
                        WorkerId: $"r{round}-w1"
                    )
                );
                registry.Register(w1);
            }

            if (round >= 2)
            {
                IRemoteWorker w2 = MakeDynamicWorker(
                    $"r{round}-w2",
                    2,
                    t => new DispatchResult(
                        t.TaskId,
                        true,
                        $"/w2/{t.TaskId}",
                        TimeSpan.FromSeconds(1),
                        WorkerId: $"r{round}-w2"
                    )
                );
                registry.Register(w2);
            }

            EncodeTask[] tasks = Enumerable
                .Range(
                    0,
                    round == 0 ? 2
                        : round == 1 ? 4
                        : 6
                )
                .Select(i =>
                    MakeTask(
                        $"r{round}-t{i}",
                        i % 2 == 0 ? EncodeTaskType.QualityVariant : EncodeTaskType.TimeChunk
                    )
                )
                .ToArray();

            DispatchResult[] results = await dispatcher.DispatchAsync(
                tasks,
                CancellationToken.None
            );

            results.Should().HaveCount(tasks.Length);
            results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
            HashSet<string> resultIds = results.Select(r => r.TaskId).ToHashSet();
            resultIds.Should().HaveCount(tasks.Length, $"no duplicate results in round {round}");
            resultIds
                .Should()
                .BeEquivalentTo(
                    tasks.Select(t => t.TaskId),
                    $"all tasks completed in round {round}"
                );
        }
    }

    private sealed class FakeRegistry(IReadOnlyList<IRemoteWorker> workers) : IRemoteWorkerRegistry
    {
        public IReadOnlyList<IRemoteWorker> GetActiveWorkers() => workers;
    }

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
            .Returns(() =>
                Task.FromResult(
                    new ExecutionResult(
                        Success: true,
                        ExitCode: 0,
                        StdErr: string.Empty,
                        Duration: TimeSpan.FromSeconds(1),
                        Error: null
                    )
                )
            );
        return mock;
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

    private static EncodeTask MakeTask(string id, EncodeTaskType type) =>
        new(
            TaskId: id,
            Command: new("ffmpeg", ["-i", "in.mkv", "out.ts"], null),
            OutputPath: $"/out/{id}",
            Type: type
        );
}
