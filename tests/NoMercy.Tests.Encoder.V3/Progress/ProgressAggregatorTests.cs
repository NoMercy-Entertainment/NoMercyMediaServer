namespace NoMercy.Tests.Encoder.V3.Progress;

using NoMercy.Encoder.V3.Progress;

public class ProgressAggregatorTests
{
    // ------------------------------------------------------------------
    // Single group — direct passthrough
    // ------------------------------------------------------------------

    [Fact]
    public void SingleGroup_OverallPercentage_MatchesGroupProgress()
    {
        ProgressAggregator aggregator = new([TimeSpan.FromMinutes(10)]);
        aggregator.UpdateGroup(0, 75.0);

        aggregator.OverallPercentage.Should().BeApproximately(75.0, 0.001);
    }

    [Fact]
    public void SingleGroup_Zero_ReturnsZero()
    {
        ProgressAggregator aggregator = new([TimeSpan.FromMinutes(10)]);
        aggregator.UpdateGroup(0, 0.0);

        aggregator.OverallPercentage.Should().Be(0.0);
    }

    [Fact]
    public void SingleGroup_Completed_Returns100()
    {
        ProgressAggregator aggregator = new([TimeSpan.FromMinutes(5)]);
        aggregator.UpdateGroup(0, 100.0);

        aggregator.OverallPercentage.Should().BeApproximately(100.0, 0.001);
    }

    // ------------------------------------------------------------------
    // Two equal-weight groups — average
    // ------------------------------------------------------------------

    [Fact]
    public void TwoEqualGroups_BothAt50_Returns50()
    {
        ProgressAggregator aggregator = new([TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10)]);
        aggregator.UpdateGroup(0, 50.0);
        aggregator.UpdateGroup(1, 50.0);

        aggregator.OverallPercentage.Should().BeApproximately(50.0, 0.001);
    }

    [Fact]
    public void TwoEqualGroups_FirstAt100SecondAt0_Returns50()
    {
        ProgressAggregator aggregator = new([TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10)]);
        aggregator.UpdateGroup(0, 100.0);
        aggregator.UpdateGroup(1, 0.0);

        aggregator.OverallPercentage.Should().BeApproximately(50.0, 0.001);
    }

    // ------------------------------------------------------------------
    // Unequal weights — weighted computation
    // ------------------------------------------------------------------

    [Fact]
    public void UnequalWeights_LargerGroupDominates()
    {
        // Group 0: 10 min, Group 1: 90 min
        // Group 0 at 100%, Group 1 at 0% → (100*600 + 0*5400) / 6000 = 10%
        ProgressAggregator aggregator = new([TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(90)]);
        aggregator.UpdateGroup(0, 100.0);
        aggregator.UpdateGroup(1, 0.0);

        double expected = (100.0 * 600.0) / (600.0 + 5400.0);
        aggregator.OverallPercentage.Should().BeApproximately(expected, 0.001);
    }

    [Fact]
    public void UnequalWeights_BothAt50_Returns50()
    {
        // Any set of weights — if all groups are at 50%, overall is 50%
        ProgressAggregator aggregator = new([TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(55)]);
        aggregator.UpdateGroup(0, 50.0);
        aggregator.UpdateGroup(1, 50.0);

        aggregator.OverallPercentage.Should().BeApproximately(50.0, 0.001);
    }

    // ------------------------------------------------------------------
    // Clamping
    // ------------------------------------------------------------------

    [Fact]
    public void UpdateGroup_PercentageAbove100_ClampsTo100()
    {
        ProgressAggregator aggregator = new([TimeSpan.FromMinutes(10)]);
        aggregator.UpdateGroup(0, 150.0);

        aggregator.OverallPercentage.Should().BeApproximately(100.0, 0.001);
    }

    [Fact]
    public void UpdateGroup_NegativePercentage_ClampsTo0()
    {
        ProgressAggregator aggregator = new([TimeSpan.FromMinutes(10)]);
        aggregator.UpdateGroup(0, -10.0);

        aggregator.OverallPercentage.Should().Be(0.0);
    }

    [Fact]
    public void UpdateGroup_OutOfRangeIndex_IsIgnored()
    {
        ProgressAggregator aggregator = new([TimeSpan.FromMinutes(10)]);
        aggregator.UpdateGroup(0, 50.0);
        aggregator.UpdateGroup(99, 100.0); // out of range — should not throw

        aggregator.OverallPercentage.Should().BeApproximately(50.0, 0.001);
    }

    // ------------------------------------------------------------------
    // EstimatedRemaining
    // ------------------------------------------------------------------

    [Fact]
    public void EstimatedRemaining_AtZeroPercent_ReturnsNull()
    {
        ProgressAggregator aggregator = new([TimeSpan.FromMinutes(60)]);
        aggregator.UpdateGroup(0, 0.0);

        aggregator.EstimatedRemaining(TimeSpan.FromMinutes(5)).Should().BeNull();
    }

    [Fact]
    public void EstimatedRemaining_AtHalfway_IsPositive()
    {
        ProgressAggregator aggregator = new([TimeSpan.FromMinutes(60)]);
        aggregator.UpdateGroup(0, 50.0);

        TimeSpan? remaining = aggregator.EstimatedRemaining(TimeSpan.FromMinutes(10));

        remaining.Should().NotBeNull();
        remaining!.Value.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void EstimatedRemaining_At100Percent_ReturnsZero()
    {
        ProgressAggregator aggregator = new([TimeSpan.FromMinutes(60)]);
        aggregator.UpdateGroup(0, 100.0);

        TimeSpan? remaining = aggregator.EstimatedRemaining(TimeSpan.FromMinutes(15));

        remaining.Should().NotBeNull();
        remaining!.Value.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void EstimatedRemaining_TypicalCase_IsReasonable()
    {
        // 50% done in 30 minutes → estimate ~30 more minutes remaining
        ProgressAggregator aggregator = new([TimeSpan.FromMinutes(60)]);
        aggregator.UpdateGroup(0, 50.0);

        TimeSpan? remaining = aggregator.EstimatedRemaining(TimeSpan.FromMinutes(30));

        remaining.Should().NotBeNull();
        // Allow generous range: between 20 and 40 minutes (linear projection = exactly 30)
        remaining!.Value.Should().BeGreaterThan(TimeSpan.FromMinutes(20));
        remaining.Value.Should().BeLessThan(TimeSpan.FromMinutes(40));
    }
}
