namespace NoMercy.Tests.Encoder.Distribution;

using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Distribution;

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
                new("a", SpeedMultiplier: 2.0, AvailableSlots: 4),
                new("b", SpeedMultiplier: 1.0, AvailableSlots: 2),
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
                new("beast", SpeedMultiplier: 4.0, AvailableSlots: 4),
                new("laptop", SpeedMultiplier: 1.0, AvailableSlots: 2),
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
                new("a", SpeedMultiplier: 2.0, AvailableSlots: 2),
                new("b", SpeedMultiplier: 2.0, AvailableSlots: 2),
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
            [new("only", SpeedMultiplier: 1.5, AvailableSlots: 0)]
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
                new("beast", SpeedMultiplier: 4.0, AvailableSlots: 4),
                new("slow", SpeedMultiplier: 1.0, AvailableSlots: 2),
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
            [
                new("a", 2.0, 4),
                new("b", 1.0, 2),
                new("c", 0.5, 1),
            ]
        );

        HashSet<string> placed = result.Values.SelectMany(v => v).Select(t => t.TaskId).ToHashSet();
        placed.Should().BeEquivalentTo(["t0", "t1", "t2", "t3", "t4"]);
    }

    private static EncodeTask MakeTask(string id, EncodeTaskType type) =>
        new(
            TaskId: id,
            Command: new("ffmpeg", ["-i", "in.mkv", "out.ts"], null),
            OutputPath: $"/out/{id}",
            Type: type
        );
}
