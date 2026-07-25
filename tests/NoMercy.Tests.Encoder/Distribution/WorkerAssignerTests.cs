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
            [MakeTask("t0", EncodeTaskType.QualityVariant)],
            []
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public void Assign_NoTasks_YieldsEmptyBucketPerWorker()
    {
        WorkerAssigner sut = new();

        Dictionary<string, EncodeTask[]> result = sut.Assign(
            [],
            [
                new("a", 2.0, 4),
                new("b", 1.0, 2),
            ]
        );

        result.Keys.Should().BeEquivalentTo(["a", "b"]);
        result["a"].Should().BeEmpty();
        result["b"].Should().BeEmpty();
    }

    [Fact]
    public void Assign_FasterWorker_GetsMoreTasks()
    {
        WorkerAssigner sut = new();
        EncodeTask[] tasks =
        [
            MakeTask("t0", EncodeTaskType.QualityVariant),
            MakeTask("t1", EncodeTaskType.QualityVariant),
            MakeTask("t2", EncodeTaskType.QualityVariant),
            MakeTask("t3", EncodeTaskType.QualityVariant),
        ];

        Dictionary<string, EncodeTask[]> result = sut.Assign(
            tasks,
            [
                new("beast", 4.0, 4),
                new("laptop", 1.0, 2),
            ]
        );

        result["beast"].Length.Should().BeGreaterThan(result["laptop"].Length);
        (result["beast"].Length + result["laptop"].Length).Should().Be(4);
    }

    [Fact]
    public void Assign_EqualWorkers_SplitsEvenly()
    {
        WorkerAssigner sut = new();
        EncodeTask[] tasks =
        [
            MakeTask("t0", EncodeTaskType.QualityVariant),
            MakeTask("t1", EncodeTaskType.QualityVariant),
            MakeTask("t2", EncodeTaskType.QualityVariant),
            MakeTask("t3", EncodeTaskType.QualityVariant),
        ];

        Dictionary<string, EncodeTask[]> result = sut.Assign(
            tasks,
            [
                new("a", 2.0, 2),
                new("b", 2.0, 2),
            ]
        );

        result["a"].Length.Should().Be(2);
        result["b"].Length.Should().Be(2);
    }

    [Fact]
    public void Assign_ZeroAvailableSlots_StillPlacesTasks()
    {
        WorkerAssigner sut = new();

        Dictionary<string, EncodeTask[]> result = sut.Assign(
            [MakeTask("t0", EncodeTaskType.QualityVariant)],
            [new("only", 1.5, 0)]
        );

        result["only"].Should().HaveCount(1);
    }

    [Fact]
    public void Assign_PrefersQualityVariantsOnFastestWorker()
    {
        // Quality variant = full encode. Time chunks = subset work.
        // Heaviest work should land on the fastest box.
        WorkerAssigner sut = new();
        EncodeTask[] tasks =
        [
            MakeTask("chunk0", EncodeTaskType.TimeChunk),
            MakeTask("variant0", EncodeTaskType.QualityVariant),
            MakeTask("chunk1", EncodeTaskType.TimeChunk),
        ];

        Dictionary<string, EncodeTask[]> result = sut.Assign(
            tasks,
            [
                new("beast", 4.0, 4),
                new("slow", 1.0, 2),
            ]
        );

        result["beast"].Should().Contain(t => t.TaskId == "variant0");
    }

    [Fact]
    public void Assign_OutputCoversEveryInputTask()
    {
        // Regardless of how the assigner splits tasks, the union of all
        // buckets must equal the input set. No drops, no duplicates.
        WorkerAssigner sut = new();
        EncodeTask[] tasks =
        [
            MakeTask("t0", EncodeTaskType.QualityVariant),
            MakeTask("t1", EncodeTaskType.TimeChunk),
            MakeTask("t2", EncodeTaskType.QualityVariant),
            MakeTask("t3", EncodeTaskType.TimeChunk),
            MakeTask("t4", EncodeTaskType.QualityVariant),
        ];

        Dictionary<string, EncodeTask[]> result = sut.Assign(
            tasks,
            [new("a", 2.0, 4), new("b", 1.0, 2), new("c", 0.5, 1)]
        );

        HashSet<string> placed = result.Values.SelectMany(v => v).Select(t => t.TaskId).ToHashSet();
        placed.Should().BeEquivalentTo(["t0", "t1", "t2", "t3", "t4"]);
    }

    private static EncodeTask MakeTask(string id, EncodeTaskType type) =>
        new(
            id,
            new("ffmpeg", ["-i", "in.mkv", "out.ts"], null),
            $"/out/{id}",
            type
        );

    private static EncodeTask MakeGpuTask(
        string id,
        bool requiresGpu,
        int cost = 1,
        string variantId = ""
    ) =>
        new(
            id,
            Command: new("ffmpeg", ["-i", "in.mkv", "out.ts"], null),
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
            [MakeGpuTask("gpu-task", true)],
            [
                new("cpu-only", 4.0, 8, false),
                new("gpu-box", 2.0, 2, true),
            ]
        );

        result["gpu-box"].Select(t => t.TaskId).Should().Contain("gpu-task");
        result["cpu-only"].Should().BeEmpty();
    }

    [Fact]
    public void Assign_GpuTask_FallsBackToAnyWorkerWhenNoGpuAvailable()
    {
        WorkerAssigner sut = new();

        Dictionary<string, EncodeTask[]> result = sut.Assign(
            [MakeGpuTask("gpu-task", true)],
            [new("cpu-only", 1.0, 4, false)]
        );

        result["cpu-only"].Select(t => t.TaskId).Should().Contain("gpu-task");
    }

    [Fact]
    public void Assign_HighCostTask_DrainsCapacityFaster()
    {
        WorkerAssigner sut = new();

        Dictionary<string, EncodeTask[]> result = sut.Assign(
            [
                MakeGpuTask("heavy", false, 8),
                MakeGpuTask("a", false, 1),
                MakeGpuTask("b", false, 1),
                MakeGpuTask("c", false, 1),
            ],
            [
                new("fast", 2.0, 2, false),
                new("slow", 1.0, 2, false),
            ]
        );

        // The heavy task drains "fast"'s effective weight on first pass; the
        // following light tasks land on "slow" which still has full capacity.
        result["fast"].Select(t => t.TaskId).Should().Contain("heavy");
        result["slow"]
            .Select(t => t.TaskId)
            .Should()
            .Contain(id => id == "a" || id == "b" || id == "c");
    }
}
