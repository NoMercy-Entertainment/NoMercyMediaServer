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
    private static readonly TimeSpan TwoHours = TimeSpan.FromHours(2);
    private static readonly TimeSpan NinetyMinutes = TimeSpan.FromMinutes(90);

    // ------------------------------------------------------------------
    // Subtitle / chapter group → near-instant estimate
    // ------------------------------------------------------------------

    [Fact]
    public void SubtitleGroup_EstimatesNearInstantDuration()
    {
        ExecutionGroup group = new(
            "group_0",
            [new("sub_0", OperationType.SubtitleExtract, [], new())],
            null,
            0,
            1,
            false,
            0
        );

        CostEstimate estimate = Estimator.EstimateGroup(group, TwoHours);

        estimate.EstimatedDuration.Should().BeLessThan(TimeSpan.FromMinutes(1));
        estimate.GpuUtilization.Should().Be(0);
    }

    [Fact]
    public void ChapterGroup_EstimatesNearInstantDuration()
    {
        ExecutionGroup group = new(
            "group_0",
            [new("ch_0", OperationType.ChapterExtract, [], new())],
            null,
            0,
            1,
            false,
            0
        );

        CostEstimate estimate = Estimator.EstimateGroup(group, TwoHours);

        estimate.EstimatedDuration.Should().BeLessThan(TimeSpan.FromMinutes(1));
    }

    // ------------------------------------------------------------------
    // Video encode group → estimate based on input duration
    // ------------------------------------------------------------------

    [Fact]
    public void GpuVideoEncodeGroup_EstimatesLessThanInputDuration()
    {
        ExecutionGroup group = new(
            "group_0",
            [
                new("decode_0", OperationType.Decode, [], new()),
                new("encode_0", OperationType.Encode, ["decode_0"], new()),
            ],
            "RTX 4090",
            1,
            0,
            true,
            1
        );

        CostEstimate estimate = Estimator.EstimateGroup(group, NinetyMinutes);

        // GPU is faster-than-realtime: estimate should be less than input duration
        estimate.EstimatedDuration.Should().BeLessThan(NinetyMinutes);
        estimate.EstimatedDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void SoftwareVideoEncodeGroup_ReturnsPositiveDuration()
    {
        ExecutionGroup group = new(
            "group_0",
            [
                new("decode_0", OperationType.Decode, [], new()),
                new("encode_0", OperationType.Encode, ["decode_0"], new()),
            ],
            null,
            0,
            4,
            false,
            1
        );

        CostEstimate estimate = Estimator.EstimateGroup(group, NinetyMinutes);

        estimate.EstimatedDuration.Should().BeGreaterThan(TimeSpan.Zero);
        estimate.GpuUtilization.Should().Be(0);
        estimate.CpuUtilization.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GpuEncodeGroup_GpuUtilizationIsProportionalToSlots()
    {
        ExecutionGroup group1Slot = new(
            "group_1",
            [new("enc_0", OperationType.Encode, [], new())],
            "RTX 4090",
            1,
            0,
            true,
            1
        );

        ExecutionGroup group6Slots = group1Slot with { GpuSlotsRequired = 6, GroupId = "group_6" };

        CostEstimate estimate1 = Estimator.EstimateGroup(group1Slot, NinetyMinutes);
        CostEstimate estimate6 = Estimator.EstimateGroup(group6Slots, NinetyMinutes);

        estimate6.GpuUtilization.Should().BeGreaterThan(estimate1.GpuUtilization);
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
                "sub",
                [new("sub_0", OperationType.SubtitleExtract, [], new())],
                null,
                0,
                1,
                false,
                0
            ),
            new(
                "main",
                [
                    new("decode_0", OperationType.Decode, [], new()),
                    new("encode_0", OperationType.Encode, ["decode_0"], new()),
                ],
                "RTX 4090",
                1,
                0,
                true,
                1
            ),
        ];

        TimeSpan total = Estimator.EstimateTotal(groups, NinetyMinutes);

        // Sum of sub (instant ~10s) + main encode (< 90 min for GPU) should be positive and < 2h
        total.Should().BeGreaterThan(TimeSpan.Zero);

        // Verify it's the sum of individual estimates
        TimeSpan manual = groups
            .Select(g => Estimator.EstimateGroup(g, NinetyMinutes).EstimatedDuration)
            .Aggregate(TimeSpan.Zero, (acc, d) => acc + d);
        total.Should().Be(manual);
    }

    [Fact]
    public void EstimateTotal_EmptyGroups_ReturnsZero()
    {
        TimeSpan total = Estimator.EstimateTotal([], NinetyMinutes);
        total.Should().Be(TimeSpan.Zero);
    }
}
