namespace NoMercy.Encoder.Distribution;

/// <summary>
/// Greedy capacity-weighted assigner. Sorts workers by
/// <c>SpeedMultiplier × AvailableSlots</c> descending, then walks tasks
/// and hands each one to the worker with the most remaining weight.
/// Good enough for the common case (a beast box + a GPU box + a laptop);
/// accepts that it's not optimal packing — we're optimizing for "land
/// every task somewhere" over "minimize total wall time."
///
/// When no workers are supplied, returns an empty map — callers interpret
/// this as "fall back to the local dispatcher." When all workers have
/// zero available slots the assigner still places tasks on the fastest
/// worker rather than refusing; strict capacity enforcement lives at the
/// dispatcher layer, not here.
/// </summary>
public class WorkerAssigner : IWorkerAssigner
{
    public Dictionary<string, EncodeTask[]> Assign(
        EncodeTask[] tasks,
        IReadOnlyList<WorkerCapacity> workers
    )
    {
        if (workers.Count == 0)
            return [];

        // Seed each worker with an empty bucket so the output shape matches
        // the input — callers can iterate every worker uniformly.
        Dictionary<string, List<EncodeTask>> buckets = workers.ToDictionary(
            w => w.WorkerId,
            _ => new List<EncodeTask>()
        );

        if (tasks.Length == 0)
            return buckets.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());

        // Remaining weight tracks capacity consumption as we assign tasks.
        // Max(1, AvailableSlots) so a worker with zero slots still gets a
        // fallback weight — we'd rather overload one box than strand tasks.
        Dictionary<string, double> remainingWeight = workers.ToDictionary(
            w => w.WorkerId,
            w => w.SpeedMultiplier * Math.Max(1, w.AvailableSlots)
        );

        // Sort tasks so the heaviest (QualityVariant at the top — implies a
        // full encode) land on the fastest workers. TimeChunk tasks are
        // typically smaller and fill remaining capacity well.
        IEnumerable<EncodeTask> ordered = tasks.OrderBy(t =>
            t.Type == EncodeTaskType.QualityVariant ? 0 : 1
        );

        foreach (EncodeTask task in ordered)
        {
            string chosen = PickHeaviestRemainingWorker(workers, remainingWeight);
            buckets[chosen].Add(task);

            // Each assigned task consumes one unit of effective multiplier —
            // keeps a single fast box from swallowing every task.
            remainingWeight[chosen] = Math.Max(
                0,
                remainingWeight[chosen] - GetConsumedWeight(task)
            );
        }

        return buckets.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
    }

    private static string PickHeaviestRemainingWorker(
        IReadOnlyList<WorkerCapacity> workers,
        Dictionary<string, double> remainingWeight
    )
    {
        string? best = null;
        double bestWeight = double.NegativeInfinity;

        foreach (WorkerCapacity worker in workers)
        {
            double weight = remainingWeight[worker.WorkerId];
            if (weight > bestWeight)
            {
                bestWeight = weight;
                best = worker.WorkerId;
            }
        }

        // At least one worker exists (guarded above) so `best` is never null.
        return best!;
    }

    // Quality variants are "full encodes" and consume one full slot of
    // weight. Time chunks are subset work and consume proportionally less.
    private static double GetConsumedWeight(EncodeTask task) =>
        task.Type == EncodeTaskType.QualityVariant ? 1.0 : 0.5;
}
