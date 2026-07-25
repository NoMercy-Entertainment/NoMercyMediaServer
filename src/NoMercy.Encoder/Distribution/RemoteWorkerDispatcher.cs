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

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Jobs;

namespace NoMercy.Encoder.Distribution;

/// <summary>
/// Dispatches tasks across registered remote workers using
/// <see cref="IWorkerAssigner"/> for capacity-weighted splitting. Falls
/// back to the local dispatcher when no remote workers are registered —
/// this means enabling distributed encoding is purely additive; rolling
/// it out doesn't break single-machine installs.
///
/// When workers ARE registered, each task lands on a chosen worker via
/// <see cref="IRemoteWorker.ExecuteTaskAsync"/>. Worker failures cascade
/// through a short retry chain before giving up: the initial worker
/// picked by the assigner → another active worker not yet tried →
/// local fallback. All task-level dispatches run in parallel; the
/// retry chain is per-task.
/// </summary>
public class RemoteWorkerDispatcher(
    IRemoteWorkerRegistry registry,
    IWorkerAssigner assigner,
    LocalWorkerDispatcher localFallback,
    ILogger<RemoteWorkerDispatcher> logger
) : IWorkerDispatcher
{
    /// <summary>
    /// Maximum remote workers to try per task before falling back to local.
    /// Includes the assigner's initial pick. Value of 2 = one retry.
    /// </summary>
    private const int MaxRemoteAttempts = 2;

    public int AvailableWorkerCount =>
        Math.Max(localFallback.AvailableWorkerCount, registry.GetActiveWorkers().Count);

    public async Task<DispatchResult[]> DispatchAsync(EncodeTask[] tasks, CancellationToken ct)
    {
        IReadOnlyList<IRemoteWorker> remoteWorkers = registry.GetActiveWorkers();

        if (remoteWorkers.Count == 0)
        {
            logger.LogDebug("No remote workers registered — falling back to local dispatcher");
            return await localFallback.DispatchAsync(tasks, ct).ConfigureAwait(false);
        }

        List<WorkerCapacity> capacities = remoteWorkers
            .Select(w =>
            {
                ResourceBudgetSnapshot budget = w.GetAvailableBudget();
                int slots = Math.Max(0, budget.AvailableGpuSlots + budget.AvailableCpuThreads);
                return new WorkerCapacity(
                    WorkerId: w.WorkerId,
                    SpeedMultiplier: Math.Max(1, slots),
                    AvailableSlots: slots,
                    // Carry the GPU capability through to the assigner. Without
                    // this the flag defaults false, the assigner's GPU-routing
                    // (RequiresGpu -> workers.Where(HasGpu)) finds no eligible
                    // worker, and a GPU task falls through to the unconstrained
                    // pick — landing on a CPU-only box. A free GPU slot is the
                    // signal that this worker can take GPU work right now.
                    HasGpu: budget.AvailableGpuSlots > 0
                );
            })
            .ToList();

        Dictionary<string, EncodeTask[]> assignments = assigner.Assign(tasks, capacities);

        // Build a lookup so we can map worker IDs back to concrete workers.
        Dictionary<string, IRemoteWorker> workerById = remoteWorkers.ToDictionary(w => w.WorkerId);

        logger.LogInformation(
            "Dispatching {Count} tasks across {Workers} remote workers",
            tasks.Length,
            remoteWorkers.Count
        );

        // Run every task in parallel. Each task gets its own retry chain
        // across up to MaxRemoteAttempts distinct workers before the
        // local fallback kicks in.
        Task<DispatchResult>[] dispatches = assignments
            .SelectMany(kvp =>
                kvp.Value.Select(task =>
                    RunWithRemoteRetryAsync(workerById[kvp.Key], task, remoteWorkers, ct)
                )
            )
            .ToArray();

        DispatchResult[] results = await Task.WhenAll(dispatches).ConfigureAwait(false);
        return results;
    }

    private async Task<DispatchResult> RunWithRemoteRetryAsync(
        IRemoteWorker initialWorker,
        EncodeTask task,
        IReadOnlyList<IRemoteWorker> allWorkers,
        CancellationToken ct
    )
    {
        HashSet<string> attempted = [];
        IRemoteWorker? current = initialWorker;

        for (int attempt = 0; attempt < MaxRemoteAttempts && current is not null; attempt++)
        {
            attempted.Add(current.WorkerId);

            DispatchResult? attemptResult = await TryWorkerAsync(current, task, ct)
                .ConfigureAwait(false);
            if (attemptResult is { Success: true })
                return attemptResult;

            // Find the next worker we haven't tried yet. Prefer workers
            // with the most available slots so a transient error on the
            // first pick doesn't cascade into the weakest worker.
            current = allWorkers
                .Where(w => !attempted.Contains(w.WorkerId))
                .OrderByDescending(w =>
                {
                    ResourceBudgetSnapshot b = w.GetAvailableBudget();
                    return b.AvailableGpuSlots + b.AvailableCpuThreads;
                })
                .FirstOrDefault();

            if (current is not null)
            {
                logger.LogInformation(
                    "Task {TaskId} failed on {FailedWorker}; retrying on {NextWorker}",
                    task.TaskId,
                    attempted.Last(),
                    current.WorkerId
                );
            }
        }

        // All remote attempts exhausted — fall back to local for this task.
        logger.LogWarning(
            "Task {TaskId} exhausted remote retries ({Attempts} workers tried) — using local dispatcher",
            task.TaskId,
            attempted.Count
        );
        DispatchResult[] fallbackResults = await localFallback
            .DispatchAsync([task], ct)
            .ConfigureAwait(false);
        return fallbackResults.Length > 0
            ? fallbackResults[0]
            : new(
                task.TaskId,
                Success: false,
                OutputPath: task.OutputPath,
                Duration: TimeSpan.Zero,
                Error: "Local fallback returned no result"
            );
    }

    /// <summary>
    /// Runs one task on one worker. Returns the result on success,
    /// null when the worker should be considered failed so the caller
    /// can try another. Exceptions are caught and treated as failures.
    /// </summary>
    private async Task<DispatchResult?> TryWorkerAsync(
        IRemoteWorker worker,
        EncodeTask task,
        CancellationToken ct
    )
    {
        try
        {
            DispatchResult result = await worker.ExecuteTaskAsync(task, ct).ConfigureAwait(false);
            RecordOutcome(worker.WorkerId, result.Success);
            if (result.Success)
                return result;

            logger.LogWarning(
                "Worker {WorkerId} failed task {TaskId} ({Error})",
                worker.WorkerId,
                task.TaskId,
                result.Error
            );
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            RecordOutcome(worker.WorkerId, success: false);
            logger.LogWarning(
                ex,
                "Worker {WorkerId} threw on task {TaskId}",
                worker.WorkerId,
                task.TaskId
            );
            return null;
        }
    }

    /// <summary>
    /// Reports the outcome to the registry for health tracking. Gracefully
    /// no-ops when the registry implementation doesn't track health — keeps
    /// plugin registries that only implement IRemoteWorkerRegistry working.
    /// </summary>
    private void RecordOutcome(string workerId, bool success)
    {
        switch (registry)
        {
            case JsonRemoteWorkerRegistry json:
                json.RecordTaskOutcome(workerId, success);
                break;
            case InMemoryRemoteWorkerRegistry mem:
                mem.RecordTaskOutcome(workerId, success);
                break;
        }
    }
}
