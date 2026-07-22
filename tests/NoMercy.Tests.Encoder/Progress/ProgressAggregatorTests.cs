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

using NoMercy.Encoder.Progress;

namespace NoMercy.Tests.Encoder.Progress;

public class ProgressAggregatorTests
{
    // ------------------------------------------------------------------
    // Single group — direct passthrough
    // ------------------------------------------------------------------

    [Fact]
    public void SingleGroup_OverallPercentage_MatchesGroupProgress()
    {
        ProgressAggregator aggregator = new(estimatedDurations: [TimeSpan.FromMinutes(minutes: 10)]);
        aggregator.UpdateGroup(groupIndex: 0, percentage: 75.0);

        aggregator.OverallPercentage.Should().BeApproximately(expectedValue: 75.0, precision: 0.001);
    }

    [Fact]
    public void SingleGroup_Zero_ReturnsZero()
    {
        ProgressAggregator aggregator = new(estimatedDurations: [TimeSpan.FromMinutes(minutes: 10)]);
        aggregator.UpdateGroup(groupIndex: 0, percentage: 0.0);

        aggregator.OverallPercentage.Should().Be(expected: 0.0);
    }

    [Fact]
    public void SingleGroup_Completed_Returns100()
    {
        ProgressAggregator aggregator = new(estimatedDurations: [TimeSpan.FromMinutes(minutes: 5)]);
        aggregator.UpdateGroup(groupIndex: 0, percentage: 100.0);

        aggregator.OverallPercentage.Should().BeApproximately(expectedValue: 100.0, precision: 0.001);
    }

    // ------------------------------------------------------------------
    // Two equal-weight groups — average
    // ------------------------------------------------------------------

    [Fact]
    public void TwoEqualGroups_BothAt50_Returns50()
    {
        ProgressAggregator aggregator = new(estimatedDurations: [TimeSpan.FromMinutes(minutes: 10), TimeSpan.FromMinutes(minutes: 10)]);
        aggregator.UpdateGroup(groupIndex: 0, percentage: 50.0);
        aggregator.UpdateGroup(groupIndex: 1, percentage: 50.0);

        aggregator.OverallPercentage.Should().BeApproximately(expectedValue: 50.0, precision: 0.001);
    }

    [Fact]
    public void TwoEqualGroups_FirstAt100SecondAt0_Returns50()
    {
        ProgressAggregator aggregator = new(estimatedDurations: [TimeSpan.FromMinutes(minutes: 10), TimeSpan.FromMinutes(minutes: 10)]);
        aggregator.UpdateGroup(groupIndex: 0, percentage: 100.0);
        aggregator.UpdateGroup(groupIndex: 1, percentage: 0.0);

        aggregator.OverallPercentage.Should().BeApproximately(expectedValue: 50.0, precision: 0.001);
    }

    // ------------------------------------------------------------------
    // Unequal weights — weighted computation
    // ------------------------------------------------------------------

    [Fact]
    public void UnequalWeights_LargerGroupDominates()
    {
        // Group 0: 10 min, Group 1: 90 min
        // Group 0 at 100%, Group 1 at 0% → (100*600 + 0*5400) / 6000 = 10%
        ProgressAggregator aggregator = new(estimatedDurations: [TimeSpan.FromMinutes(minutes: 10), TimeSpan.FromMinutes(minutes: 90)]);
        aggregator.UpdateGroup(groupIndex: 0, percentage: 100.0);
        aggregator.UpdateGroup(groupIndex: 1, percentage: 0.0);

        double expected = (100.0 * 600.0) / (600.0 + 5400.0);
        aggregator.OverallPercentage.Should().BeApproximately(expectedValue: expected, precision: 0.001);
    }

    [Fact]
    public void UnequalWeights_BothAt50_Returns50()
    {
        // Any set of weights — if all groups are at 50%, overall is 50%
        ProgressAggregator aggregator = new(estimatedDurations: [TimeSpan.FromMinutes(minutes: 5), TimeSpan.FromMinutes(minutes: 55)]);
        aggregator.UpdateGroup(groupIndex: 0, percentage: 50.0);
        aggregator.UpdateGroup(groupIndex: 1, percentage: 50.0);

        aggregator.OverallPercentage.Should().BeApproximately(expectedValue: 50.0, precision: 0.001);
    }

    // ------------------------------------------------------------------
    // Clamping
    // ------------------------------------------------------------------

    [Fact]
    public void UpdateGroup_PercentageAbove100_ClampsTo100()
    {
        ProgressAggregator aggregator = new(estimatedDurations: [TimeSpan.FromMinutes(minutes: 10)]);
        aggregator.UpdateGroup(groupIndex: 0, percentage: 150.0);

        aggregator.OverallPercentage.Should().BeApproximately(expectedValue: 100.0, precision: 0.001);
    }

    [Fact]
    public void UpdateGroup_NegativePercentage_ClampsTo0()
    {
        ProgressAggregator aggregator = new(estimatedDurations: [TimeSpan.FromMinutes(minutes: 10)]);
        aggregator.UpdateGroup(groupIndex: 0, percentage: -10.0);

        aggregator.OverallPercentage.Should().Be(expected: 0.0);
    }

    [Fact]
    public void UpdateGroup_OutOfRangeIndex_IsIgnored()
    {
        ProgressAggregator aggregator = new(estimatedDurations: [TimeSpan.FromMinutes(minutes: 10)]);
        aggregator.UpdateGroup(groupIndex: 0, percentage: 50.0);
        aggregator.UpdateGroup(groupIndex: 99, percentage: 100.0); // out of range — should not throw

        aggregator.OverallPercentage.Should().BeApproximately(expectedValue: 50.0, precision: 0.001);
    }

    // ------------------------------------------------------------------
    // EstimatedRemaining
    // ------------------------------------------------------------------

    [Fact]
    public void EstimatedRemaining_AtZeroPercent_ReturnsNull()
    {
        ProgressAggregator aggregator = new(estimatedDurations: [TimeSpan.FromMinutes(minutes: 60)]);
        aggregator.UpdateGroup(groupIndex: 0, percentage: 0.0);

        aggregator.EstimatedRemaining(elapsed: TimeSpan.FromMinutes(minutes: 5)).Should().BeNull();
    }

    [Fact]
    public void EstimatedRemaining_AtHalfway_IsPositive()
    {
        ProgressAggregator aggregator = new(estimatedDurations: [TimeSpan.FromMinutes(minutes: 60)]);
        aggregator.UpdateGroup(groupIndex: 0, percentage: 50.0);

        TimeSpan? remaining = aggregator.EstimatedRemaining(elapsed: TimeSpan.FromMinutes(minutes: 10));

        remaining.Should().NotBeNull();
        remaining!.Value.Should().BeGreaterThan(expected: TimeSpan.Zero);
    }

    [Fact]
    public void EstimatedRemaining_At100Percent_ReturnsZero()
    {
        ProgressAggregator aggregator = new(estimatedDurations: [TimeSpan.FromMinutes(minutes: 60)]);
        aggregator.UpdateGroup(groupIndex: 0, percentage: 100.0);

        TimeSpan? remaining = aggregator.EstimatedRemaining(elapsed: TimeSpan.FromMinutes(minutes: 15));

        remaining.Should().NotBeNull();
        remaining!.Value.Should().Be(expected: TimeSpan.Zero);
    }

    [Fact]
    public void EstimatedRemaining_TypicalCase_IsReasonable()
    {
        // 50% done in 30 minutes → estimate ~30 more minutes remaining
        ProgressAggregator aggregator = new(estimatedDurations: [TimeSpan.FromMinutes(minutes: 60)]);
        aggregator.UpdateGroup(groupIndex: 0, percentage: 50.0);

        TimeSpan? remaining = aggregator.EstimatedRemaining(elapsed: TimeSpan.FromMinutes(minutes: 30));

        remaining.Should().NotBeNull();
        // Allow generous range: between 20 and 40 minutes (linear projection = exactly 30)
        remaining!.Value.Should().BeGreaterThan(expected: TimeSpan.FromMinutes(minutes: 20));
        remaining.Value.Should().BeLessThan(expected: TimeSpan.FromMinutes(minutes: 40));
    }
}
