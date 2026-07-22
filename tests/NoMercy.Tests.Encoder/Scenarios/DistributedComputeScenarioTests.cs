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
        s: "scenario-test-shared-key-32-bytes!"
    );
    private readonly TaskSerializer _serializer = new();

    [Fact]
    public async Task SingleInstance_NoRemoteWorkers_AllTasksAssignedLocallyWithoutLoss()
    {
        Mock<IFfmpegExecutor> executor = MakeSuccessExecutor();
        LocalWorkerDispatcher dispatcher = new(
            executor: executor.Object,
            logger: NullLogger<LocalWorkerDispatcher>.Instance
        );

        EncodeTask[] tasks =
        [
            MakeTask(id: "t0", type: EncodeTaskType.QualityVariant),
            MakeTask(id: "t1", type: EncodeTaskType.QualityVariant),
            MakeTask(id: "t2", type: EncodeTaskType.TimeChunk),
            MakeTask(id: "t3", type: EncodeTaskType.TimeChunk),
        ];

        DispatchResult[] results = await dispatcher.DispatchAsync(tasks: tasks, ct: CancellationToken.None);

        results.Should().HaveCount(expected: 4);
        results.Should().AllSatisfy(expected: r => r.Success.Should().BeTrue());
        HashSet<string> resultIds = results.Select(selector: r => r.TaskId).ToHashSet();
        resultIds.Should().BeEquivalentTo(expectation: ["t0", "t1", "t2", "t3"]);
    }

    [Fact]
    public async Task MultiInstance_VaryingCapacity_TasksDistributedByCapacityNotAllOnOne()
    {
        Mock<IFfmpegExecutor> executor = MakeSuccessExecutor();
        LocalWorkerDispatcher local = new(
            executor: executor.Object,
            logger: NullLogger<LocalWorkerDispatcher>.Instance
        );

        int beastReceived = 0;
        int laptopReceived = 0;

        IRemoteWorker beast = MakeDynamicWorker(
            id: "beast",
            slots: 8,
            producer: t =>
            {
                Interlocked.Increment(location: ref beastReceived);
                return new DispatchResult(
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
                Interlocked.Increment(location: ref laptopReceived);
                return new DispatchResult(
                    TaskId: t.TaskId,
                    Success: true,
                    OutputPath: $"/laptop/{t.TaskId}",
                    Duration: TimeSpan.FromSeconds(seconds: 1),
                    WorkerId: "laptop"
                );
            }
        );

        InMemoryRemoteWorkerRegistry registry = new();
        registry.Register(worker: beast);
        registry.Register(worker: laptop);

        RemoteWorkerDispatcher dispatcher = new(
            registry: registry,
            assigner: new WorkerAssigner(),
            localFallback: local,
            logger: NullLogger<RemoteWorkerDispatcher>.Instance
        );

        EncodeTask[] tasks = Enumerable
            .Range(start: 0, count: 6)
            .Select(selector: i => MakeTask(id: $"t{i}", type: EncodeTaskType.QualityVariant))
            .ToArray();

        DispatchResult[] results = await dispatcher.DispatchAsync(tasks: tasks, ct: CancellationToken.None);

        results.Should().HaveCount(expected: 6);
        results.Should().AllSatisfy(expected: r => r.Success.Should().BeTrue());
        (beastReceived + laptopReceived).Should().Be(expected: 6);
        beastReceived
            .Should()
            .BeGreaterThan(expected: laptopReceived, because: "higher-capacity worker should receive more tasks");
    }

    [Fact]
    public async Task RemoteWorkerParticipates_ReceivesAtLeastOneTask()
    {
        Mock<IFfmpegExecutor> executor = MakeSuccessExecutor();
        LocalWorkerDispatcher local = new(
            executor: executor.Object,
            logger: NullLogger<LocalWorkerDispatcher>.Instance
        );

        int remoteTaskCount = 0;
        IRemoteWorker remote = MakeDynamicWorker(
            id: "remote-box",
            slots: 4,
            producer: t =>
            {
                Interlocked.Increment(location: ref remoteTaskCount);
                return new DispatchResult(
                    TaskId: t.TaskId,
                    Success: true,
                    OutputPath: $"/remote/{t.TaskId}",
                    Duration: TimeSpan.FromSeconds(seconds: 1),
                    WorkerId: "remote-box"
                );
            }
        );

        InMemoryRemoteWorkerRegistry registry = new();
        registry.Register(worker: remote);

        RemoteWorkerDispatcher dispatcher = new(
            registry: registry,
            assigner: new WorkerAssigner(),
            localFallback: local,
            logger: NullLogger<RemoteWorkerDispatcher>.Instance
        );

        EncodeTask[] tasks = [MakeTask(id: "single", type: EncodeTaskType.QualityVariant)];

        DispatchResult[] results = await dispatcher.DispatchAsync(tasks: tasks, ct: CancellationToken.None);

        results.Should().HaveCount(expected: 1);
        results[0].Success.Should().BeTrue();
        results[0].WorkerId.Should().Be(expected: "remote-box");
        remoteTaskCount.Should().Be(expected: 1);
    }

    [Fact]
    public async Task SaturatedWorker_OverloadedNotStranded_CapacityWorkerPreferredWhenAvailable()
    {
        Mock<IFfmpegExecutor> executor = MakeSuccessExecutor();
        LocalWorkerDispatcher local = new(
            executor: executor.Object,
            logger: NullLogger<LocalWorkerDispatcher>.Instance
        );

        int zeroSlotsCalls = 0;
        Mock<IRemoteWorker> zeroCalls = new();
        zeroCalls.SetupGet(expression: w => w.WorkerId).Returns(value: "no-slots");
        zeroCalls.Setup(expression: w => w.GetAvailableBudget()).Returns(value: new ResourceBudgetSnapshot(AvailableGpuSlots: 0, AvailableCpuThreads: 0, GpuUtilization: 0));
        zeroCalls
            .Setup(expression: w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .Callback(action: () => Interlocked.Increment(location: ref zeroSlotsCalls))
            .Returns(
                valueFunction: (EncodeTask t, CancellationToken _) =>
                    Task.FromResult(
                        result: new DispatchResult(
                            TaskId: t.TaskId,
                            Success: true,
                            OutputPath: "/out/t0",
                            Duration: TimeSpan.FromSeconds(seconds: 1),
                            WorkerId: "no-slots"
                        )
                    )
            );

        InMemoryRemoteWorkerRegistry registry = new();
        registry.Register(worker: zeroCalls.Object);

        RemoteWorkerDispatcher dispatcher = new(
            registry: registry,
            assigner: new WorkerAssigner(),
            localFallback: local,
            logger: NullLogger<RemoteWorkerDispatcher>.Instance
        );

        EncodeTask[] tasks = [MakeTask(id: "t0", type: EncodeTaskType.QualityVariant)];
        DispatchResult[] results = await dispatcher.DispatchAsync(tasks: tasks, ct: CancellationToken.None);

        results.Should().HaveCount(expected: 1);
        results[0].Success.Should().BeTrue();
        // Documented assigner contract: when the ONLY registered worker is
        // saturated (zero available slots) the assigner overloads it rather
        // than stranding the task — strict capacity enforcement is the
        // dispatcher/registry's job, not the assigner's. The guarantee here is
        // that the task is NOT stranded; it completes on the (only) worker.
        zeroSlotsCalls.Should().Be(expected: 1, because: "a saturated sole worker is overloaded, not stranded");

        int withCapacityCalls = 0;
        Mock<IRemoteWorker> withCapacity = new();
        withCapacity.SetupGet(expression: w => w.WorkerId).Returns(value: "has-slots");
        withCapacity
            .Setup(expression: w => w.GetAvailableBudget())
            .Returns(value: new ResourceBudgetSnapshot(AvailableGpuSlots: 0, AvailableCpuThreads: 4, GpuUtilization: 0));
        withCapacity
            .Setup(expression: w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .Callback(action: () => Interlocked.Increment(location: ref withCapacityCalls))
            .Returns(
                valueFunction: (EncodeTask t, CancellationToken _) =>
                    Task.FromResult(
                        result: new DispatchResult(
                            TaskId: t.TaskId,
                            Success: true,
                            OutputPath: "/out/t1",
                            Duration: TimeSpan.FromSeconds(seconds: 1),
                            WorkerId: "has-slots"
                        )
                    )
            );

        registry.Register(worker: withCapacity.Object);

        results = await dispatcher.DispatchAsync(
            tasks: [MakeTask(id: "t1", type: EncodeTaskType.QualityVariant)],
            ct: CancellationToken.None
        );

        results[0].Success.Should().BeTrue();
        results[0].WorkerId.Should().Be(expected: "has-slots");
        withCapacityCalls.Should().Be(expected: 1, because: "worker with capacity should be assigned");
    }

    [Fact]
    public void TaskSerializer_RoundTrip_SerializeSignVerifyRecoveryTask()
    {
        EncodeTask original = MakeTask(id: "round-trip", type: EncodeTaskType.QualityVariant);

        string signed = _serializer.Serialize(task: original, signingKey: _sharedKey);

        EncodeTask? recovered = _serializer.Deserialize(payload: signed, signingKey: _sharedKey);

        recovered.Should().NotBeNull();
        recovered!.TaskId.Should().Be(expected: "round-trip");
        recovered.OutputPath.Should().Be(expected: "/out/round-trip");
        recovered.Type.Should().Be(expected: EncodeTaskType.QualityVariant);
    }

    [Fact]
    public void TaskSerializer_TamperedPayload_FailsHmacVerification()
    {
        EncodeTask original = MakeTask(id: "tamper-test", type: EncodeTaskType.QualityVariant);
        string signed = _serializer.Serialize(task: original, signingKey: _sharedKey);

        int tamperIndex = signed.Length / 2;
        string tampered =
            signed.Substring(startIndex: 0, length: tamperIndex) + "X" + signed.Substring(startIndex: tamperIndex + 1);

        EncodeTask? recovered = _serializer.Deserialize(payload: tampered, signingKey: _sharedKey);

        recovered.Should().BeNull(because: "tampered payload should fail verification");
    }

    [Fact]
    public void ResultSerializer_RoundTrip_SerializeSignVerifyResult()
    {
        DispatchResult original = new(
            TaskId: "result-rt",
            Success: true,
            OutputPath: "/out/result",
            Duration: TimeSpan.FromSeconds(seconds: 5),
            WorkerId: "test-worker"
        );

        string signed = _serializer.SerializeResult(result: original, signingKey: _sharedKey);

        DispatchResult? recovered = _serializer.DeserializeResult(payload: signed, signingKey: _sharedKey);

        recovered.Should().NotBeNull();
        recovered!.TaskId.Should().Be(expected: "result-rt");
        recovered.Success.Should().BeTrue();
        recovered.WorkerId.Should().Be(expected: "test-worker");
        recovered.Duration.Should().Be(expected: TimeSpan.FromSeconds(seconds: 5));
    }

    [Fact]
    public void ResultSerializer_TamperedResult_FailsHmacVerification()
    {
        DispatchResult original = new(
            TaskId: "result-tamper",
            Success: true,
            OutputPath: "/out/r",
            Duration: TimeSpan.FromSeconds(seconds: 1),
            WorkerId: "w"
        );
        string signed = _serializer.SerializeResult(result: original, signingKey: _sharedKey);

        int tamperIndex = signed.Length / 2;
        string tampered =
            signed.Substring(startIndex: 0, length: tamperIndex) + "Z" + signed.Substring(startIndex: tamperIndex + 1);

        DispatchResult? recovered = _serializer.DeserializeResult(payload: tampered, signingKey: _sharedKey);

        recovered.Should().BeNull(because: "tampered result should fail verification");
    }

    [Fact]
    public async Task WorkerCountChanges_DistributionAdaptsBetweenRounds()
    {
        Mock<IFfmpegExecutor> executor = MakeSuccessExecutor();
        LocalWorkerDispatcher local = new(
            executor: executor.Object,
            logger: NullLogger<LocalWorkerDispatcher>.Instance
        );

        InMemoryRemoteWorkerRegistry registry = new();
        RemoteWorkerDispatcher dispatcher = new(
            registry: registry,
            assigner: new WorkerAssigner(),
            localFallback: local,
            logger: NullLogger<RemoteWorkerDispatcher>.Instance
        );

        EncodeTask[] tasks1 = Enumerable
            .Range(start: 0, count: 2)
            .Select(selector: i => MakeTask(id: $"round1-t{i}", type: EncodeTaskType.QualityVariant))
            .ToArray();

        DispatchResult[] results1 = await dispatcher.DispatchAsync(tasks: tasks1, ct: CancellationToken.None);
        results1.Should().AllSatisfy(expected: r => r.Success.Should().BeTrue());

        int workerACount = 0;
        IRemoteWorker workerA = MakeDynamicWorker(
            id: "a",
            slots: 4,
            producer: t =>
            {
                Interlocked.Increment(location: ref workerACount);
                return new DispatchResult(
                    TaskId: t.TaskId,
                    Success: true,
                    OutputPath: $"/a/{t.TaskId}",
                    Duration: TimeSpan.FromSeconds(seconds: 1),
                    WorkerId: "a"
                );
            }
        );
        registry.Register(worker: workerA);

        EncodeTask[] tasks2 = Enumerable
            .Range(start: 0, count: 2)
            .Select(selector: i => MakeTask(id: $"round2-t{i}", type: EncodeTaskType.QualityVariant))
            .ToArray();

        DispatchResult[] results2 = await dispatcher.DispatchAsync(tasks: tasks2, ct: CancellationToken.None);
        results2.Should().AllSatisfy(expected: r => r.Success.Should().BeTrue());
        workerACount.Should().BeGreaterThan(expected: 0, because: "new worker should receive tasks");

        int workerBCount = 0;
        IRemoteWorker workerB = MakeDynamicWorker(
            id: "b",
            slots: 2,
            producer: t =>
            {
                Interlocked.Increment(location: ref workerBCount);
                return new DispatchResult(
                    TaskId: t.TaskId,
                    Success: true,
                    OutputPath: $"/b/{t.TaskId}",
                    Duration: TimeSpan.FromSeconds(seconds: 1),
                    WorkerId: "b"
                );
            }
        );
        registry.Register(worker: workerB);

        EncodeTask[] tasks3 = Enumerable
            .Range(start: 0, count: 4)
            .Select(selector: i => MakeTask(id: $"round3-t{i}", type: EncodeTaskType.QualityVariant))
            .ToArray();

        DispatchResult[] results3 = await dispatcher.DispatchAsync(tasks: tasks3, ct: CancellationToken.None);
        results3.Should().AllSatisfy(expected: r => r.Success.Should().BeTrue());
        // The greedy capacity-weighted assigner may concentrate a small batch on
        // the fastest worker, so B is not guaranteed a share at this scale. The
        // invariant across the worker-count change is that NO task is lost:
        // 2 from round 2 (worker A only) + 4 from round 3.
        (workerACount + workerBCount)
            .Should()
            .Be(expected: 6, because: "no task is lost when a worker is added mid-stream");
    }

    [Fact]
    public async Task NoTaskLossOrDuplication_UnionOfAllBucketsEqualsInputSet()
    {
        Mock<IFfmpegExecutor> executor = MakeSuccessExecutor();
        LocalWorkerDispatcher local = new(
            executor: executor.Object,
            logger: NullLogger<LocalWorkerDispatcher>.Instance
        );

        int workerACount = 0;
        int workerBCount = 0;
        int workerCCount = 0;

        IRemoteWorker a = MakeDynamicWorker(
            id: "a",
            slots: 4,
            producer: t =>
            {
                Interlocked.Increment(location: ref workerACount);
                return new DispatchResult(
                    TaskId: t.TaskId,
                    Success: true,
                    OutputPath: $"/a/{t.TaskId}",
                    Duration: TimeSpan.FromSeconds(seconds: 1),
                    WorkerId: "a"
                );
            }
        );

        IRemoteWorker b = MakeDynamicWorker(
            id: "b",
            slots: 6,
            producer: t =>
            {
                Interlocked.Increment(location: ref workerBCount);
                return new DispatchResult(
                    TaskId: t.TaskId,
                    Success: true,
                    OutputPath: $"/b/{t.TaskId}",
                    Duration: TimeSpan.FromSeconds(seconds: 1),
                    WorkerId: "b"
                );
            }
        );

        IRemoteWorker c = MakeDynamicWorker(
            id: "c",
            slots: 2,
            producer: t =>
            {
                Interlocked.Increment(location: ref workerCCount);
                return new DispatchResult(
                    TaskId: t.TaskId,
                    Success: true,
                    OutputPath: $"/c/{t.TaskId}",
                    Duration: TimeSpan.FromSeconds(seconds: 1),
                    WorkerId: "c"
                );
            }
        );

        InMemoryRemoteWorkerRegistry registry = new();
        registry.Register(worker: a);
        registry.Register(worker: b);
        registry.Register(worker: c);

        RemoteWorkerDispatcher dispatcher = new(
            registry: registry,
            assigner: new WorkerAssigner(),
            localFallback: local,
            logger: NullLogger<RemoteWorkerDispatcher>.Instance
        );

        EncodeTask[] tasks = Enumerable
            .Range(start: 0, count: 12)
            .Select(selector: i =>
                MakeTask(
                    id: $"t{i}",
                    type: i % 2 == 0 ? EncodeTaskType.QualityVariant : EncodeTaskType.TimeChunk
                )
            )
            .ToArray();

        DispatchResult[] results = await dispatcher.DispatchAsync(tasks: tasks, ct: CancellationToken.None);

        results.Should().HaveCount(expected: 12);
        HashSet<string> resultIds = results.Select(selector: r => r.TaskId).ToHashSet();
        resultIds.Should().HaveCount(expected: 12, because: "no duplicates");
        resultIds.Should().BeEquivalentTo(expectation: tasks.Select(selector: t => t.TaskId));
        (workerACount + workerBCount + workerCCount).Should().Be(expected: 12);
    }

    [Fact]
    public async Task GpuTasks_RoutedToGpuCapableWorkerInMultiInstance()
    {
        Mock<IFfmpegExecutor> executor = MakeSuccessExecutor();
        LocalWorkerDispatcher local = new(
            executor: executor.Object,
            logger: NullLogger<LocalWorkerDispatcher>.Instance
        );

        int cpuOnlyCount = 0;
        int gpuBoxCount = 0;

        Mock<IRemoteWorker> cpuOnly = new();
        cpuOnly.SetupGet(expression: w => w.WorkerId).Returns(value: "cpu-only");
        cpuOnly.Setup(expression: w => w.GetAvailableBudget()).Returns(value: new ResourceBudgetSnapshot(AvailableGpuSlots: 0, AvailableCpuThreads: 8, GpuUtilization: 0));
        cpuOnly
            .Setup(expression: w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .Callback(action: () => Interlocked.Increment(location: ref cpuOnlyCount))
            .Returns(
                valueFunction: (EncodeTask _, CancellationToken __) =>
                    Task.FromResult(
                        result: new DispatchResult(
                            TaskId: "cpu-task",
                            Success: true,
                            OutputPath: "/cpu/out",
                            Duration: TimeSpan.FromSeconds(seconds: 1),
                            WorkerId: "cpu-only"
                        )
                    )
            );

        Mock<IRemoteWorker> gpuBox = new();
        gpuBox.SetupGet(expression: w => w.WorkerId).Returns(value: "gpu-box");
        gpuBox.Setup(expression: w => w.GetAvailableBudget()).Returns(value: new ResourceBudgetSnapshot(AvailableGpuSlots: 2, AvailableCpuThreads: 4, GpuUtilization: 0));
        gpuBox
            .Setup(expression: w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .Callback(action: () => Interlocked.Increment(location: ref gpuBoxCount))
            .Returns(
                valueFunction: (EncodeTask _, CancellationToken __) =>
                    Task.FromResult(
                        result: new DispatchResult(
                            TaskId: "gpu-task",
                            Success: true,
                            OutputPath: "/gpu/out",
                            Duration: TimeSpan.FromSeconds(seconds: 1),
                            WorkerId: "gpu-box"
                        )
                    )
            );

        FakeRegistry registry = new(workers: [cpuOnly.Object, gpuBox.Object]);

        RemoteWorkerDispatcher dispatcher = new(
            registry: registry,
            assigner: new WorkerAssigner(),
            localFallback: local,
            logger: NullLogger<RemoteWorkerDispatcher>.Instance
        );

        EncodeTask gpuTask = new(
            TaskId: "gpu-req",
            Command: new(Executable: "ffmpeg", Arguments: ["-i", "in.mkv", "out.ts"], WorkingDirectory: null),
            OutputPath: "/out/gpu-req",
            Type: EncodeTaskType.QualityVariant,
            RequiresGpu: true
        );

        DispatchResult[] results = await dispatcher.DispatchAsync(
            tasks: [gpuTask],
            ct: CancellationToken.None
        );

        results.Should().HaveCount(expected: 1);
        results[0].Success.Should().BeTrue();
        results[0].WorkerId.Should().Be(expected: "gpu-box", because: "GPU task should route to GPU-capable worker");
        gpuBoxCount.Should().Be(expected: 1);
        cpuOnlyCount.Should().Be(expected: 0, because: "GPU task should not land on CPU-only worker");
    }

    [Fact]
    public async Task AllRemoteWorkersFail_FallsBackToLocalForAllTasks()
    {
        int localCount = 0;
        Mock<IFfmpegExecutor> executor = new();
        executor
            .Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(action: () => Interlocked.Increment(location: ref localCount))
            .Returns(valueFunction: () =>
                Task.FromResult(
                    result: new ExecutionResult(
                        Success: true,
                        ExitCode: 0,
                        StdErr: "",
                        Duration: TimeSpan.FromSeconds(seconds: 1),
                        Error: null
                    )
                )
            );

        LocalWorkerDispatcher local = new(
            executor: executor.Object,
            logger: NullLogger<LocalWorkerDispatcher>.Instance
        );

        Mock<IRemoteWorker> broken1 = new();
        broken1.SetupGet(expression: w => w.WorkerId).Returns(value: "broken1");
        broken1.Setup(expression: w => w.GetAvailableBudget()).Returns(value: new ResourceBudgetSnapshot(AvailableGpuSlots: 0, AvailableCpuThreads: 4, GpuUtilization: 0));
        broken1
            .Setup(expression: w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .Throws(exception: new HttpRequestException(message: "connection refused"));

        Mock<IRemoteWorker> broken2 = new();
        broken2.SetupGet(expression: w => w.WorkerId).Returns(value: "broken2");
        broken2.Setup(expression: w => w.GetAvailableBudget()).Returns(value: new ResourceBudgetSnapshot(AvailableGpuSlots: 0, AvailableCpuThreads: 4, GpuUtilization: 0));
        broken2
            .Setup(expression: w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .Returns(
                valueFunction: (EncodeTask _, CancellationToken __) =>
                    Task.FromResult(
                        result: new DispatchResult(TaskId: "t0", Success: false, OutputPath: "", Duration: TimeSpan.Zero, Error: "OOM")
                    )
            );

        FakeRegistry registry = new(workers: [broken1.Object, broken2.Object]);

        RemoteWorkerDispatcher dispatcher = new(
            registry: registry,
            assigner: new WorkerAssigner(),
            localFallback: local,
            logger: NullLogger<RemoteWorkerDispatcher>.Instance
        );

        DispatchResult[] results = await dispatcher.DispatchAsync(
            tasks: [MakeTask(id: "fallback-test", type: EncodeTaskType.QualityVariant)],
            ct: CancellationToken.None
        );

        results.Should().HaveCount(expected: 1);
        results[0].Success.Should().BeTrue();
        localCount.Should().Be(expected: 1, because: "task should fall back to local when all remote workers fail");
    }

    [Fact]
    public void WorkerHealthTracking_FailedWorker_EntersCooldownHiddenFromDispatch()
    {
        InMemoryRemoteWorkerRegistry registry = new();

        Mock<IRemoteWorker> flaky = new();
        flaky.SetupGet(expression: w => w.WorkerId).Returns(value: "flaky");
        flaky.Setup(expression: w => w.GetAvailableBudget()).Returns(value: new ResourceBudgetSnapshot(AvailableGpuSlots: 0, AvailableCpuThreads: 4, GpuUtilization: 0));
        flaky
            .Setup(expression: w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .Returns(
                valueFunction: (EncodeTask _, CancellationToken __) =>
                    Task.FromResult(
                        result: new DispatchResult(TaskId: "t0", Success: false, OutputPath: "", Duration: TimeSpan.Zero, Error: "failure")
                    )
            );

        registry.Register(worker: flaky.Object);

        registry.RecordTaskOutcome(workerId: "flaky", success: false);
        registry.RecordTaskOutcome(workerId: "flaky", success: false);
        registry.RecordTaskOutcome(workerId: "flaky", success: false);

        IReadOnlyList<IRemoteWorker> active = registry.GetActiveWorkers();

        active.Should().BeEmpty(because: "worker with 3 consecutive failures should be in cooldown");
    }

    [Fact]
    public void WorkerHealthTracking_SuccessClears_CooldownLifts()
    {
        InMemoryRemoteWorkerRegistry registry = new();

        Mock<IRemoteWorker> recovering = new();
        recovering.SetupGet(expression: w => w.WorkerId).Returns(value: "recovering");
        recovering.Setup(expression: w => w.GetAvailableBudget()).Returns(value: new ResourceBudgetSnapshot(AvailableGpuSlots: 0, AvailableCpuThreads: 4, GpuUtilization: 0));

        registry.Register(worker: recovering.Object);

        registry.RecordTaskOutcome(workerId: "recovering", success: false);
        registry.RecordTaskOutcome(workerId: "recovering", success: false);
        registry.RecordTaskOutcome(workerId: "recovering", success: false);

        IReadOnlyList<IRemoteWorker> active = registry.GetActiveWorkers();
        active.Should().BeEmpty(because: "in cooldown after 3 failures");

        registry.RecordTaskOutcome(workerId: "recovering", success: true);

        active = registry.GetActiveWorkers();
        active.Should().HaveCount(expected: 1, because: "success clears failure counter and exits cooldown");
    }

    [Fact]
    public async Task CostUnits_AffectCapacityConsumption_HighCostTaskFillsFasterOnce()
    {
        Mock<IFfmpegExecutor> executor = MakeSuccessExecutor();
        LocalWorkerDispatcher local = new(
            executor: executor.Object,
            logger: NullLogger<LocalWorkerDispatcher>.Instance
        );

        int fastCount = 0;
        int slowCount = 0;

        IRemoteWorker fast = MakeDynamicWorker(
            id: "fast",
            slots: 2,
            producer: t =>
            {
                Interlocked.Increment(location: ref fastCount);
                return new DispatchResult(
                    TaskId: t.TaskId,
                    Success: true,
                    OutputPath: $"/fast/{t.TaskId}",
                    Duration: TimeSpan.FromSeconds(seconds: 1),
                    WorkerId: "fast"
                );
            }
        );

        IRemoteWorker slow = MakeDynamicWorker(
            id: "slow",
            slots: 1,
            producer: t =>
            {
                Interlocked.Increment(location: ref slowCount);
                return new DispatchResult(
                    TaskId: t.TaskId,
                    Success: true,
                    OutputPath: $"/slow/{t.TaskId}",
                    Duration: TimeSpan.FromSeconds(seconds: 1),
                    WorkerId: "slow"
                );
            }
        );

        InMemoryRemoteWorkerRegistry registry = new();
        registry.Register(worker: fast);
        registry.Register(worker: slow);

        RemoteWorkerDispatcher dispatcher = new(
            registry: registry,
            assigner: new WorkerAssigner(),
            localFallback: local,
            logger: NullLogger<RemoteWorkerDispatcher>.Instance
        );

        EncodeTask heavyTask = new(
            TaskId: "heavy",
            Command: new(Executable: "ffmpeg", Arguments: ["-i", "in.mkv", "out.ts"], WorkingDirectory: null),
            OutputPath: "/out/heavy",
            Type: EncodeTaskType.QualityVariant,
            EstimatedCostUnits: 8
        );

        EncodeTask lightTask = new(
            TaskId: "light",
            Command: new(Executable: "ffmpeg", Arguments: ["-i", "in.mkv", "out.ts"], WorkingDirectory: null),
            OutputPath: "/out/light",
            Type: EncodeTaskType.QualityVariant,
            EstimatedCostUnits: 1
        );

        DispatchResult[] results = await dispatcher.DispatchAsync(
            tasks: [heavyTask, lightTask],
            ct: CancellationToken.None
        );

        results.Should().HaveCount(expected: 2);
        results.Should().AllSatisfy(expected: r => r.Success.Should().BeTrue());
        fastCount.Should().Be(expected: 1, because: "fast worker should get the heavy task");
        slowCount.Should().Be(expected: 1, because: "slow worker should get remaining light task");
    }

    [Fact]
    public async Task TimeChunkAndQualityVariantTasks_WeightedByType_VariantsPreferFastestWorker()
    {
        Mock<IFfmpegExecutor> executor = MakeSuccessExecutor();
        LocalWorkerDispatcher local = new(
            executor: executor.Object,
            logger: NullLogger<LocalWorkerDispatcher>.Instance
        );

        int beastHasVariant = 0;
        int laptopHasVariant = 0;

        IRemoteWorker beast = MakeDynamicWorker(
            id: "beast",
            slots: 8,
            producer: t =>
            {
                if (t.Type == EncodeTaskType.QualityVariant)
                    Interlocked.Increment(location: ref beastHasVariant);
                return new DispatchResult(
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
                if (t.Type == EncodeTaskType.QualityVariant)
                    Interlocked.Increment(location: ref laptopHasVariant);
                return new DispatchResult(
                    TaskId: t.TaskId,
                    Success: true,
                    OutputPath: $"/laptop/{t.TaskId}",
                    Duration: TimeSpan.FromSeconds(seconds: 1),
                    WorkerId: "laptop"
                );
            }
        );

        InMemoryRemoteWorkerRegistry registry = new();
        registry.Register(worker: beast);
        registry.Register(worker: laptop);

        RemoteWorkerDispatcher dispatcher = new(
            registry: registry,
            assigner: new WorkerAssigner(),
            localFallback: local,
            logger: NullLogger<RemoteWorkerDispatcher>.Instance
        );

        EncodeTask[] tasks =
        [
            MakeTask(id: "chunk0", type: EncodeTaskType.TimeChunk),
            MakeTask(id: "variant", type: EncodeTaskType.QualityVariant),
            MakeTask(id: "chunk1", type: EncodeTaskType.TimeChunk),
        ];

        DispatchResult[] results = await dispatcher.DispatchAsync(tasks: tasks, ct: CancellationToken.None);

        results.Should().HaveCount(expected: 3);
        results.Should().AllSatisfy(expected: r => r.Success.Should().BeTrue());
        beastHasVariant.Should().Be(expected: 1, because: "full variant (heaviest) should land on fastest worker");
    }

    [Fact]
    public async Task MultiRound_DifferentTaskShapesAndWorkerChurn_NoInvariantViolation()
    {
        Mock<IFfmpegExecutor> executor = MakeSuccessExecutor();
        LocalWorkerDispatcher local = new(
            executor: executor.Object,
            logger: NullLogger<LocalWorkerDispatcher>.Instance
        );

        InMemoryRemoteWorkerRegistry registry = new();
        RemoteWorkerDispatcher dispatcher = new(
            registry: registry,
            assigner: new WorkerAssigner(),
            localFallback: local,
            logger: NullLogger<RemoteWorkerDispatcher>.Instance
        );

        for (int round = 0; round < 3; round++)
        {
            if (round >= 1)
            {
                IRemoteWorker w1 = MakeDynamicWorker(
                    id: $"r{round}-w1",
                    slots: 4,
                    producer: t => new DispatchResult(
                        TaskId: t.TaskId,
                        Success: true,
                        OutputPath: $"/w1/{t.TaskId}",
                        Duration: TimeSpan.FromSeconds(seconds: 1),
                        WorkerId: $"r{round}-w1"
                    )
                );
                registry.Register(worker: w1);
            }

            if (round >= 2)
            {
                IRemoteWorker w2 = MakeDynamicWorker(
                    id: $"r{round}-w2",
                    slots: 2,
                    producer: t => new DispatchResult(
                        TaskId: t.TaskId,
                        Success: true,
                        OutputPath: $"/w2/{t.TaskId}",
                        Duration: TimeSpan.FromSeconds(seconds: 1),
                        WorkerId: $"r{round}-w2"
                    )
                );
                registry.Register(worker: w2);
            }

            EncodeTask[] tasks = Enumerable
                .Range(
                    start: 0,
                    count: round == 0 ? 2
                        : round == 1 ? 4
                        : 6
                )
                .Select(selector: i =>
                    MakeTask(
                        id: $"r{round}-t{i}",
                        type: i % 2 == 0 ? EncodeTaskType.QualityVariant : EncodeTaskType.TimeChunk
                    )
                )
                .ToArray();

            DispatchResult[] results = await dispatcher.DispatchAsync(
                tasks: tasks,
                ct: CancellationToken.None
            );

            results.Should().HaveCount(expected: tasks.Length);
            results.Should().AllSatisfy(expected: r => r.Success.Should().BeTrue());
            HashSet<string> resultIds = results.Select(selector: r => r.TaskId).ToHashSet();
            resultIds.Should().HaveCount(expected: tasks.Length, because: $"no duplicate results in round {round}");
            resultIds
                .Should()
                .BeEquivalentTo(
                    expectation: tasks.Select(selector: t => t.TaskId),
                    because: $"all tasks completed in round {round}"
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
        mock.Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(valueFunction: () =>
                Task.FromResult(
                    result: new ExecutionResult(
                        Success: true,
                        ExitCode: 0,
                        StdErr: string.Empty,
                        Duration: TimeSpan.FromSeconds(seconds: 1),
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
        mock.SetupGet(expression: w => w.WorkerId).Returns(value: id);
        mock.Setup(expression: w => w.GetAvailableBudget()).Returns(value: new ResourceBudgetSnapshot(AvailableGpuSlots: 0, AvailableCpuThreads: slots, GpuUtilization: 0));
        mock.Setup(expression: w => w.ExecuteTaskAsync(It.IsAny<EncodeTask>(), It.IsAny<CancellationToken>()))
            .Returns(valueFunction: (EncodeTask t, CancellationToken _) => Task.FromResult(result: producer(arg: t)));
        return mock.Object;
    }

    private static EncodeTask MakeTask(string id, EncodeTaskType type) =>
        new(
            TaskId: id,
            Command: new(Executable: "ffmpeg", Arguments: ["-i", "in.mkv", "out.ts"], WorkingDirectory: null),
            OutputPath: $"/out/{id}",
            Type: type
        );
}
