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

using NoMercy.Encoder.Distribution;

namespace NoMercy.Tests.Encoder.Distribution;

public class WorkerAssignerTests
{
    [Fact]
    public void Assign_NoWorkers_ReturnsEmpty()
    {
        WorkerAssigner sut = new();

        Dictionary<string, EncodeTask[]> result = sut.Assign(
            tasks: [MakeTask(id: "t0", type: EncodeTaskType.QualityVariant)],
            workers: []
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public void Assign_NoTasks_YieldsEmptyBucketPerWorker()
    {
        WorkerAssigner sut = new();

        Dictionary<string, EncodeTask[]> result = sut.Assign(
            tasks: [],
            workers:
            [
                new(WorkerId: "a", SpeedMultiplier: 2.0, AvailableSlots: 4),
                new(WorkerId: "b", SpeedMultiplier: 1.0, AvailableSlots: 2),
            ]
        );

        result.Keys.Should().BeEquivalentTo(expectation: ["a", "b"]);
        result[key: "a"].Should().BeEmpty();
        result[key: "b"].Should().BeEmpty();
    }

    [Fact]
    public void Assign_FasterWorker_GetsMoreTasks()
    {
        WorkerAssigner sut = new();
        EncodeTask[] tasks =
        [
            MakeTask(id: "t0", type: EncodeTaskType.QualityVariant),
            MakeTask(id: "t1", type: EncodeTaskType.QualityVariant),
            MakeTask(id: "t2", type: EncodeTaskType.QualityVariant),
            MakeTask(id: "t3", type: EncodeTaskType.QualityVariant),
        ];

        Dictionary<string, EncodeTask[]> result = sut.Assign(
            tasks: tasks,
            workers:
            [
                new(WorkerId: "beast", SpeedMultiplier: 4.0, AvailableSlots: 4),
                new(WorkerId: "laptop", SpeedMultiplier: 1.0, AvailableSlots: 2),
            ]
        );

        result[key: "beast"].Length.Should().BeGreaterThan(expected: result[key: "laptop"].Length);
        (result[key: "beast"].Length + result[key: "laptop"].Length).Should().Be(expected: 4);
    }

    [Fact]
    public void Assign_EqualWorkers_SplitsEvenly()
    {
        WorkerAssigner sut = new();
        EncodeTask[] tasks =
        [
            MakeTask(id: "t0", type: EncodeTaskType.QualityVariant),
            MakeTask(id: "t1", type: EncodeTaskType.QualityVariant),
            MakeTask(id: "t2", type: EncodeTaskType.QualityVariant),
            MakeTask(id: "t3", type: EncodeTaskType.QualityVariant),
        ];

        Dictionary<string, EncodeTask[]> result = sut.Assign(
            tasks: tasks,
            workers:
            [
                new(WorkerId: "a", SpeedMultiplier: 2.0, AvailableSlots: 2),
                new(WorkerId: "b", SpeedMultiplier: 2.0, AvailableSlots: 2),
            ]
        );

        result[key: "a"].Length.Should().Be(expected: 2);
        result[key: "b"].Length.Should().Be(expected: 2);
    }

    [Fact]
    public void Assign_ZeroAvailableSlots_StillPlacesTasks()
    {
        WorkerAssigner sut = new();

        Dictionary<string, EncodeTask[]> result = sut.Assign(
            tasks: [MakeTask(id: "t0", type: EncodeTaskType.QualityVariant)],
            workers: [new(WorkerId: "only", SpeedMultiplier: 1.5, AvailableSlots: 0)]
        );

        result[key: "only"].Should().HaveCount(expected: 1);
    }

    [Fact]
    public void Assign_PrefersQualityVariantsOnFastestWorker()
    {
        // Quality variant = full encode. Time chunks = subset work.
        // Heaviest work should land on the fastest box.
        WorkerAssigner sut = new();
        EncodeTask[] tasks =
        [
            MakeTask(id: "chunk0", type: EncodeTaskType.TimeChunk),
            MakeTask(id: "variant0", type: EncodeTaskType.QualityVariant),
            MakeTask(id: "chunk1", type: EncodeTaskType.TimeChunk),
        ];

        Dictionary<string, EncodeTask[]> result = sut.Assign(
            tasks: tasks,
            workers:
            [
                new(WorkerId: "beast", SpeedMultiplier: 4.0, AvailableSlots: 4),
                new(WorkerId: "slow", SpeedMultiplier: 1.0, AvailableSlots: 2),
            ]
        );

        result[key: "beast"].Should().Contain(predicate: t => t.TaskId == "variant0");
    }

    [Fact]
    public void Assign_OutputCoversEveryInputTask()
    {
        // Regardless of how the assigner splits tasks, the union of all
        // buckets must equal the input set. No drops, no duplicates.
        WorkerAssigner sut = new();
        EncodeTask[] tasks =
        [
            MakeTask(id: "t0", type: EncodeTaskType.QualityVariant),
            MakeTask(id: "t1", type: EncodeTaskType.TimeChunk),
            MakeTask(id: "t2", type: EncodeTaskType.QualityVariant),
            MakeTask(id: "t3", type: EncodeTaskType.TimeChunk),
            MakeTask(id: "t4", type: EncodeTaskType.QualityVariant),
        ];

        Dictionary<string, EncodeTask[]> result = sut.Assign(
            tasks: tasks,
            workers: [new(WorkerId: "a", SpeedMultiplier: 2.0, AvailableSlots: 4), new(WorkerId: "b", SpeedMultiplier: 1.0, AvailableSlots: 2), new(WorkerId: "c", SpeedMultiplier: 0.5, AvailableSlots: 1)]
        );

        HashSet<string> placed = result.Values.SelectMany(selector: v => v).Select(selector: t => t.TaskId).ToHashSet();
        placed.Should().BeEquivalentTo(expectation: ["t0", "t1", "t2", "t3", "t4"]);
    }

    private static EncodeTask MakeTask(string id, EncodeTaskType type) =>
        new(
            TaskId: id,
            Command: new(Executable: "ffmpeg", Arguments: ["-i", "in.mkv", "out.ts"], WorkingDirectory: null),
            OutputPath: $"/out/{id}",
            Type: type
        );

    private static EncodeTask MakeGpuTask(
        string id,
        bool requiresGpu,
        int cost = 1,
        string variantId = ""
    ) =>
        new(
            TaskId: id,
            Command: new(Executable: "ffmpeg", Arguments: ["-i", "in.mkv", "out.ts"], WorkingDirectory: null),
            OutputPath: $"/out/{id}",
            Type: EncodeTaskType.QualityVariant,
            VariantId: variantId,
            EstimatedCostUnits: cost,
            RequiresGpu: requiresGpu
        );

    [Fact]
    public void Assign_GpuTask_RoutesToGpuWorker()
    {
        WorkerAssigner sut = new();

        Dictionary<string, EncodeTask[]> result = sut.Assign(
            tasks: [MakeGpuTask(id: "gpu-task", requiresGpu: true)],
            workers:
            [
                new(WorkerId: "cpu-only", SpeedMultiplier: 4.0, AvailableSlots: 8, HasGpu: false),
                new(WorkerId: "gpu-box", SpeedMultiplier: 2.0, AvailableSlots: 2, HasGpu: true),
            ]
        );

        result[key: "gpu-box"].Select(selector: t => t.TaskId).Should().Contain(expected: "gpu-task");
        result[key: "cpu-only"].Should().BeEmpty();
    }

    [Fact]
    public void Assign_GpuTask_FallsBackToAnyWorkerWhenNoGpuAvailable()
    {
        WorkerAssigner sut = new();

        Dictionary<string, EncodeTask[]> result = sut.Assign(
            tasks: [MakeGpuTask(id: "gpu-task", requiresGpu: true)],
            workers: [new(WorkerId: "cpu-only", SpeedMultiplier: 1.0, AvailableSlots: 4, HasGpu: false)]
        );

        result[key: "cpu-only"].Select(selector: t => t.TaskId).Should().Contain(expected: "gpu-task");
    }

    [Fact]
    public void Assign_HighCostTask_DrainsCapacityFaster()
    {
        WorkerAssigner sut = new();

        Dictionary<string, EncodeTask[]> result = sut.Assign(
            tasks:
            [
                MakeGpuTask(id: "heavy", requiresGpu: false, cost: 8),
                MakeGpuTask(id: "a", requiresGpu: false, cost: 1),
                MakeGpuTask(id: "b", requiresGpu: false, cost: 1),
                MakeGpuTask(id: "c", requiresGpu: false, cost: 1),
            ],
            workers:
            [
                new(WorkerId: "fast", SpeedMultiplier: 2.0, AvailableSlots: 2, HasGpu: false),
                new(WorkerId: "slow", SpeedMultiplier: 1.0, AvailableSlots: 2, HasGpu: false),
            ]
        );

        // The heavy task drains "fast"'s effective weight on first pass; the
        // following light tasks land on "slow" which still has full capacity.
        result[key: "fast"].Select(selector: t => t.TaskId).Should().Contain(expected: "heavy");
        result[key: "slow"]
            .Select(selector: t => t.TaskId)
            .Should()
            .Contain(predicate: id => id == "a" || id == "b" || id == "c");
    }
}
