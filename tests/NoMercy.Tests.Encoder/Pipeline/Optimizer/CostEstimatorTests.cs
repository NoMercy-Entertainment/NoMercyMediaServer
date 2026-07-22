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

using NoMercy.Encoder.Pipeline.Optimizer;

namespace NoMercy.Tests.Encoder.Pipeline.Optimizer;

public class CostEstimatorTests
{
    private static readonly CostEstimator Estimator = new();
    private static readonly TimeSpan TwoHours = TimeSpan.FromHours(hours: 2);
    private static readonly TimeSpan NinetyMinutes = TimeSpan.FromMinutes(minutes: 90);

    // ------------------------------------------------------------------
    // Subtitle / chapter group → near-instant estimate
    // ------------------------------------------------------------------

    [Fact]
    public void SubtitleGroup_EstimatesNearInstantDuration()
    {
        ExecutionGroup group = new(
            GroupId: "group_0",
            Nodes: [new(Id: "sub_0", Operation: OperationType.SubtitleExtract, DependsOn: [], Parameters: new())],
            DeviceId: null,
            GpuSlotsRequired: 0,
            CpuThreadsRequired: 1,
            RequiresGpu: false,
            Priority: 0
        );

        CostEstimate estimate = Estimator.EstimateGroup(group: group, inputDuration: TwoHours);

        estimate.EstimatedDuration.Should().BeLessThan(expected: TimeSpan.FromMinutes(minutes: 1));
        estimate.GpuUtilization.Should().Be(expected: 0);
    }

    [Fact]
    public void ChapterGroup_EstimatesNearInstantDuration()
    {
        ExecutionGroup group = new(
            GroupId: "group_0",
            Nodes: [new(Id: "ch_0", Operation: OperationType.ChapterExtract, DependsOn: [], Parameters: new())],
            DeviceId: null,
            GpuSlotsRequired: 0,
            CpuThreadsRequired: 1,
            RequiresGpu: false,
            Priority: 0
        );

        CostEstimate estimate = Estimator.EstimateGroup(group: group, inputDuration: TwoHours);

        estimate.EstimatedDuration.Should().BeLessThan(expected: TimeSpan.FromMinutes(minutes: 1));
    }

    // ------------------------------------------------------------------
    // Video encode group → estimate based on input duration
    // ------------------------------------------------------------------

    [Fact]
    public void GpuVideoEncodeGroup_EstimatesLessThanInputDuration()
    {
        ExecutionGroup group = new(
            GroupId: "group_0",
            Nodes:
            [
                new(Id: "decode_0", Operation: OperationType.Decode, DependsOn: [], Parameters: new()),
                new(Id: "encode_0", Operation: OperationType.Encode, DependsOn: ["decode_0"], Parameters: new()),
            ],
            DeviceId: "RTX 4090",
            GpuSlotsRequired: 1,
            CpuThreadsRequired: 0,
            RequiresGpu: true,
            Priority: 1
        );

        CostEstimate estimate = Estimator.EstimateGroup(group: group, inputDuration: NinetyMinutes);

        // GPU is faster-than-realtime: estimate should be less than input duration
        estimate.EstimatedDuration.Should().BeLessThan(expected: NinetyMinutes);
        estimate.EstimatedDuration.Should().BeGreaterThan(expected: TimeSpan.Zero);
    }

    [Fact]
    public void SoftwareVideoEncodeGroup_ReturnsPositiveDuration()
    {
        ExecutionGroup group = new(
            GroupId: "group_0",
            Nodes:
            [
                new(Id: "decode_0", Operation: OperationType.Decode, DependsOn: [], Parameters: new()),
                new(Id: "encode_0", Operation: OperationType.Encode, DependsOn: ["decode_0"], Parameters: new()),
            ],
            DeviceId: null,
            GpuSlotsRequired: 0,
            CpuThreadsRequired: 4,
            RequiresGpu: false,
            Priority: 1
        );

        CostEstimate estimate = Estimator.EstimateGroup(group: group, inputDuration: NinetyMinutes);

        estimate.EstimatedDuration.Should().BeGreaterThan(expected: TimeSpan.Zero);
        estimate.GpuUtilization.Should().Be(expected: 0);
        estimate.CpuUtilization.Should().BeGreaterThan(expected: 0);
    }

    [Fact]
    public void GpuEncodeGroup_GpuUtilizationIsProportionalToSlots()
    {
        ExecutionGroup group1Slot = new(
            GroupId: "group_1",
            Nodes: [new(Id: "enc_0", Operation: OperationType.Encode, DependsOn: [], Parameters: new())],
            DeviceId: "RTX 4090",
            GpuSlotsRequired: 1,
            CpuThreadsRequired: 0,
            RequiresGpu: true,
            Priority: 1
        );

        ExecutionGroup group6Slots = group1Slot with { GpuSlotsRequired = 6, GroupId = "group_6" };

        CostEstimate estimate1 = Estimator.EstimateGroup(group: group1Slot, inputDuration: NinetyMinutes);
        CostEstimate estimate6 = Estimator.EstimateGroup(group: group6Slots, inputDuration: NinetyMinutes);

        estimate6.GpuUtilization.Should().BeGreaterThan(expected: estimate1.GpuUtilization);
    }

    // ------------------------------------------------------------------
    // Total estimate
    // ------------------------------------------------------------------

    [Fact]
    public void EstimateTotal_SumsAllGroupDurations()
    {
        List<ExecutionGroup> groups =
        [
            new(
                GroupId: "sub",
                Nodes: [new(Id: "sub_0", Operation: OperationType.SubtitleExtract, DependsOn: [], Parameters: new())],
                DeviceId: null,
                GpuSlotsRequired: 0,
                CpuThreadsRequired: 1,
                RequiresGpu: false,
                Priority: 0
            ),
            new(
                GroupId: "main",
                Nodes:
                [
                    new(Id: "decode_0", Operation: OperationType.Decode, DependsOn: [], Parameters: new()),
                    new(Id: "encode_0", Operation: OperationType.Encode, DependsOn: ["decode_0"], Parameters: new()),
                ],
                DeviceId: "RTX 4090",
                GpuSlotsRequired: 1,
                CpuThreadsRequired: 0,
                RequiresGpu: true,
                Priority: 1
            ),
        ];

        TimeSpan total = Estimator.EstimateTotal(groups: groups, inputDuration: NinetyMinutes);

        // Sum of sub (instant ~10s) + main encode (< 90 min for GPU) should be positive and < 2h
        total.Should().BeGreaterThan(expected: TimeSpan.Zero);

        // Verify it's the sum of individual estimates
        TimeSpan manual = groups
            .Select(selector: g => Estimator.EstimateGroup(group: g, inputDuration: NinetyMinutes).EstimatedDuration)
            .Aggregate(seed: TimeSpan.Zero, func: (acc, d) => acc + d);
        total.Should().Be(expected: manual);
    }

    [Fact]
    public void EstimateTotal_EmptyGroups_ReturnsZero()
    {
        TimeSpan total = Estimator.EstimateTotal(groups: [], inputDuration: NinetyMinutes);
        total.Should().Be(expected: TimeSpan.Zero);
    }
}
