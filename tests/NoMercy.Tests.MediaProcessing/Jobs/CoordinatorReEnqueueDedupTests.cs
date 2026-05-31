using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs.Support;
using NoMercyQueue;

namespace NoMercy.Tests.MediaProcessing.Jobs;

// Regression for the coordinator-dies-after-one-tick bug:
//
// JobQueue.Enqueue dedups by serialized Payload string. VideoEncodeJob.ReEnqueueSelf
// runs from inside the worker's Handle BEFORE the worker calls DeleteJob on the
// original (still-reserved) coordinator row. If the new payload is byte-identical
// to the original — which happens any time WaitChildren polls the same bundle —
// the new enqueue gets silently dropped, the original is then deleted, and the
// coordinator never wakes again. Bundles 2..N never dispatch and the encode
// publishes with only the first bundle's variants.
//
// Fix: WakeSequence bumps on every ReEnqueueSelf, guaranteeing payload divergence.
[Trait("Category", "Unit")]
public class CoordinatorReEnqueueDedupTests
{
    [Fact]
    public void CoordinatorState_WakeSequenceDefaultsToZero()
    {
        CoordinatorState state = new(
            GroupTag: "G",
            TaskIds: ["t1"],
            Phase: CoordinatorPhase.WaitChildren,
            Pass1DispatchedAt: null,
            Pass2DispatchedAt: null,
            Pass1StatsPath: null,
            PresetId: Ulid.NewUlid(),
            ExpectedFinalCount: 1
        );

        Assert.Equal(0, state.WakeSequence);
    }

    [Fact]
    public void SerializedPayload_DiffersWhenOnlyWakeSequenceChanges()
    {
        // Identical otherwise — same Phase, same bundle index, same group, same
        // task list. Pre-fix this collision is exactly what killed the coordinator.
        CoordinatorState first = new(
            GroupTag: "G",
            TaskIds: ["t1", "t2"],
            Phase: CoordinatorPhase.WaitChildren,
            Pass1DispatchedAt: null,
            Pass2DispatchedAt: null,
            Pass1StatsPath: null,
            PresetId: Ulid.Empty,
            ExpectedFinalCount: 2,
            CurrentBundleIndex: 0,
            WakeSequence: 1
        );

        CoordinatorState second = first with { WakeSequence = 2 };

        VideoEncodeJob firstJob = new() { Coordinator = first };
        VideoEncodeJob secondJob = new() { Coordinator = second };

        string firstPayload = SerializationHelper.Serialize(firstJob);
        string secondPayload = SerializationHelper.Serialize(secondJob);

        Assert.NotEqual(firstPayload, secondPayload);
    }

    [Fact]
    public void SerializedPayload_IdenticalWhenWakeSequenceAlsoMatches()
    {
        // Sanity check: dedup still works for genuinely duplicate dispatches
        // (e.g. retry after crash with the same WakeSequence). The nonce only
        // diverges via ReEnqueueSelf's explicit increment.
        CoordinatorState a = new(
            GroupTag: "G",
            TaskIds: ["t1"],
            Phase: CoordinatorPhase.WaitChildren,
            Pass1DispatchedAt: null,
            Pass2DispatchedAt: null,
            Pass1StatsPath: null,
            PresetId: Ulid.Empty,
            ExpectedFinalCount: 1,
            WakeSequence: 5
        );

        VideoEncodeJob jobA = new() { Coordinator = a };
        VideoEncodeJob jobB = new() { Coordinator = a };

        Assert.Equal(SerializationHelper.Serialize(jobA), SerializationHelper.Serialize(jobB));
    }
}
