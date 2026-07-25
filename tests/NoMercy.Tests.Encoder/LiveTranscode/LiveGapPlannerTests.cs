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

using NoMercy.Encoder.LiveTranscode;

namespace NoMercy.Tests.Encoder.LiveTranscode;

/// <summary>
/// Pure-logic coverage of <see cref="LiveGapPlanner"/> — the deciding surface for
/// where every runner (re)spawn starts and where it must stop so it never
/// re-encodes a segment an earlier runner generation already produced.
/// </summary>
public class LiveGapPlannerTests
{
    [Fact]
    public void Plan_EmptyCoverage_StartsAtDesired_RunsToEof()
    {
        LiveGapPlan? plan = LiveGapPlanner.Plan(
            existing: new HashSet<int>(),
            desiredIndex: 0,
            segmentDurationSeconds: 6,
            lastIndex: null
        );

        plan.Should().NotBeNull();
        plan!.Start.Should().Be(TimeSpan.Zero);
        plan.StopAt.Should().BeNull();
    }

    [Fact]
    public void Plan_FullyCovered_ReturnsNull()
    {
        HashSet<int> existing = [.. Enumerable.Range(0, 10)]; // 0..9

        LiveGapPlan? plan = LiveGapPlanner.Plan(
            existing: existing,
            desiredIndex: 0,
            segmentDurationSeconds: 6,
            lastIndex: 9
        );

        plan.Should().BeNull();
    }

    [Fact]
    public void Plan_GapBetweenTwoCoveredRanges_StopsAtTheSecondRange()
    {
        // Covered 0..50 and 200..260 — desired lands in the gap at 100. Without a
        // stop bound the respawn would eat straight through 200..260 (re-encoding
        // already-produced content) and continue to EOF — the bug this planner
        // exists to prevent.
        HashSet<int> existing = [.. Enumerable.Range(0, 51), .. Enumerable.Range(200, 61)];
        const int segDur = 6;

        LiveGapPlan? plan = LiveGapPlanner.Plan(
            existing: existing,
            desiredIndex: 100,
            segmentDurationSeconds: segDur,
            lastIndex: null
        );

        plan.Should().NotBeNull();
        plan!.Start.Should().Be(TimeSpan.FromSeconds(100 * segDur));
        plan.StopAt.Should().Be(TimeSpan.FromSeconds(200 * segDur));
    }

    [Fact]
    public void Plan_DesiredAlreadyCovered_SkipsForwardToTheRealGap()
    {
        HashSet<int> existing = [.. Enumerable.Range(0, 51)]; // 0..50
        const int segDur = 6;

        LiveGapPlan? plan = LiveGapPlanner.Plan(
            existing: existing,
            desiredIndex: 20,
            segmentDurationSeconds: segDur,
            lastIndex: null
        );

        plan.Should().NotBeNull();
        plan!.Start.Should().Be(TimeSpan.FromSeconds(51 * segDur));
        plan.StopAt.Should().BeNull();
    }

    [Fact]
    public void Plan_SmallCoveredIslandAheadOfDesired_StopsAtIt()
    {
        HashSet<int> existing = [.. Enumerable.Range(10, 10)]; // 10..19
        const int segDur = 6;

        LiveGapPlan? plan = LiveGapPlanner.Plan(
            existing: existing,
            desiredIndex: 5,
            segmentDurationSeconds: segDur,
            lastIndex: null
        );

        plan.Should().NotBeNull();
        plan!.Start.Should().Be(TimeSpan.FromSeconds(5 * segDur));
        plan.StopAt.Should().Be(TimeSpan.FromSeconds(10 * segDur));
    }

    [Fact]
    public void Plan_DesiredPastLastIndex_ReturnsNull()
    {
        LiveGapPlan? plan = LiveGapPlanner.Plan(
            existing: new HashSet<int>(),
            desiredIndex: 500,
            segmentDurationSeconds: 6,
            lastIndex: 100
        );

        plan.Should().BeNull();
    }

    [Fact]
    public void Plan_ZeroSegmentDuration_FallsBackToSixInsteadOfDividingByZero()
    {
        LiveGapPlan? plan = LiveGapPlanner.Plan(
            existing: new HashSet<int>(),
            desiredIndex: 2,
            segmentDurationSeconds: 0,
            lastIndex: null
        );

        plan.Should().NotBeNull();
        plan!.Start.Should().Be(TimeSpan.FromSeconds(2 * 6));
    }

    [Fact]
    public void Plan_NegativeDesiredIndex_ClampsToZero()
    {
        LiveGapPlan? plan = LiveGapPlanner.Plan(
            existing: new HashSet<int>(),
            desiredIndex: -5,
            segmentDurationSeconds: 6,
            lastIndex: null
        );

        plan.Should().NotBeNull();
        plan!.Start.Should().Be(TimeSpan.Zero);
    }
}
