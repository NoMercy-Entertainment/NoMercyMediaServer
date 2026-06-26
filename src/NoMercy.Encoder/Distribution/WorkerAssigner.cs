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
            // GPU-required tasks must land on a worker that has a GPU.
            // Falls back to the unconstrained pick if no GPU worker exists
            // — strict enforcement is the dispatcher's job, not ours.
            IReadOnlyList<WorkerCapacity> eligible = task.RequiresGpu
                ? [.. workers.Where(w => w.HasGpu)]
                : workers;
            if (eligible.Count == 0)
                eligible = workers;

            string chosen = PickHeaviestRemainingWorker(eligible, remainingWeight);
            buckets[chosen].Add(task);

            // Each assigned task consumes capacity weighted by its declared
            // cost units — heavy 4K HEVC two-pass tasks (cost ~8) drain a
            // worker faster than 1080p H.264 single-pass (cost 1). The
            // EncodeTaskType variant/chunk multiplier still applies.
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
    // Cost units (default 1) scale the consumed weight so heavy tasks pull
    // more capacity from a worker than light ones.
    private static double GetConsumedWeight(EncodeTask task)
    {
        double typeFactor = task.Type == EncodeTaskType.QualityVariant ? 1.0 : 0.5;
        double costFactor = Math.Max(1, task.EstimatedCostUnits);
        return typeFactor * costFactor;
    }
}
