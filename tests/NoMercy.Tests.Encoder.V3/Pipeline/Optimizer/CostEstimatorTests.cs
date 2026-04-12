namespace NoMercy.Tests.Encoder.V3.Pipeline.Optimizer;

using NoMercy.Encoder.V3.Pipeline.Optimizer;

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
            GroupId: "group_0",
            Nodes:
            [
                new ExecutionNode(
                    "sub_0",
                    OperationType.SubtitleExtract,
                    [],
                    new Dictionary<string, string>()
                ),
            ],
            DeviceId: null,
            GpuSlotsRequired: 0,
            CpuThreadsRequired: 1,
            RequiresGpu: false,
            Priority: 0
        );

        CostEstimate estimate = Estimator.EstimateGroup(group, TwoHours);

        estimate.EstimatedDuration.Should().BeLessThan(TimeSpan.FromMinutes(1));
        estimate.GpuUtilization.Should().Be(0);
    }

    [Fact]
    public void ChapterGroup_EstimatesNearInstantDuration()
    {
        ExecutionGroup group = new(
            GroupId: "group_0",
            Nodes:
            [
                new ExecutionNode(
                    "ch_0",
                    OperationType.ChapterExtract,
                    [],
                    new Dictionary<string, string>()
                ),
            ],
            DeviceId: null,
            GpuSlotsRequired: 0,
            CpuThreadsRequired: 1,
            RequiresGpu: false,
            Priority: 0
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
            GroupId: "group_0",
            Nodes:
            [
                new ExecutionNode(
                    "decode_0",
                    OperationType.Decode,
                    [],
                    new Dictionary<string, string>()
                ),
                new ExecutionNode(
                    "encode_0",
                    OperationType.Encode,
                    ["decode_0"],
                    new Dictionary<string, string>()
                ),
            ],
            DeviceId: "RTX 4090",
            GpuSlotsRequired: 1,
            CpuThreadsRequired: 0,
            RequiresGpu: true,
            Priority: 1
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
            GroupId: "group_0",
            Nodes:
            [
                new ExecutionNode(
                    "decode_0",
                    OperationType.Decode,
                    [],
                    new Dictionary<string, string>()
                ),
                new ExecutionNode(
                    "encode_0",
                    OperationType.Encode,
                    ["decode_0"],
                    new Dictionary<string, string>()
                ),
            ],
            DeviceId: null,
            GpuSlotsRequired: 0,
            CpuThreadsRequired: 4,
            RequiresGpu: false,
            Priority: 1
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
            GroupId: "group_1",
            Nodes:
            [
                new ExecutionNode(
                    "enc_0",
                    OperationType.Encode,
                    [],
                    new Dictionary<string, string>()
                ),
            ],
            DeviceId: "RTX 4090",
            GpuSlotsRequired: 1,
            CpuThreadsRequired: 0,
            RequiresGpu: true,
            Priority: 1
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
            new ExecutionGroup(
                GroupId: "sub",
                Nodes:
                [
                    new ExecutionNode(
                        "sub_0",
                        OperationType.SubtitleExtract,
                        [],
                        new Dictionary<string, string>()
                    ),
                ],
                DeviceId: null,
                GpuSlotsRequired: 0,
                CpuThreadsRequired: 1,
                RequiresGpu: false,
                Priority: 0
            ),
            new ExecutionGroup(
                GroupId: "main",
                Nodes:
                [
                    new ExecutionNode(
                        "decode_0",
                        OperationType.Decode,
                        [],
                        new Dictionary<string, string>()
                    ),
                    new ExecutionNode(
                        "encode_0",
                        OperationType.Encode,
                        ["decode_0"],
                        new Dictionary<string, string>()
                    ),
                ],
                DeviceId: "RTX 4090",
                GpuSlotsRequired: 1,
                CpuThreadsRequired: 0,
                RequiresGpu: true,
                Priority: 1
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
