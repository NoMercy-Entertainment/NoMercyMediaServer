namespace NoMercy.Encoder.Distribution;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Jobs;

/// <summary>
/// Dispatches tasks across registered remote workers using
/// <see cref="IWorkerAssigner"/> for capacity-weighted splitting. Falls
/// back to the local dispatcher when no remote workers are registered —
/// this means enabling distributed encoding is purely additive; rolling
/// it out doesn't break single-machine installs.
///
/// When workers ARE registered, each task lands on a chosen worker via
/// <see cref="IRemoteWorker.ExecuteTaskAsync"/>. Worker failures fall
/// back to the local dispatcher for the affected task (rather than
/// failing the whole job) so one bad worker doesn't stall the user's
/// encode. All worker calls run in parallel — the assigner already
/// balanced the weight so there's no per-worker queuing here.
/// </summary>
public class RemoteWorkerDispatcher(
    IRemoteWorkerRegistry registry,
    IWorkerAssigner assigner,
    LocalWorkerDispatcher localFallback,
    ILogger<RemoteWorkerDispatcher> logger
) : IWorkerDispatcher
{
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
                    AvailableSlots: slots
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

        // Run every task in parallel — the assigner already decided who
        // gets what. Each task-worker pair runs via ExecuteTaskAsync; any
        // failure falls back to the local dispatcher for that task only.
        Task<DispatchResult>[] dispatches = assignments
            .SelectMany(kvp =>
                kvp.Value.Select(task =>
                    RunOnWorkerWithFallbackAsync(workerById[kvp.Key], task, ct)
                )
            )
            .ToArray();

        DispatchResult[] results = await Task.WhenAll(dispatches).ConfigureAwait(false);
        return results;
    }

    private async Task<DispatchResult> RunOnWorkerWithFallbackAsync(
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
                "Worker {WorkerId} failed task {TaskId} ({Error}) — retrying on local dispatcher",
                worker.WorkerId,
                task.TaskId,
                result.Error
            );
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
                "Worker {WorkerId} threw on task {TaskId} — retrying on local dispatcher",
                worker.WorkerId,
                task.TaskId
            );
        }

        // Local fallback for this single task. LocalWorkerDispatcher handles
        // arrays; wrap in a 1-element array and unwrap the result.
        DispatchResult[] fallbackResults = await localFallback
            .DispatchAsync([task], ct)
            .ConfigureAwait(false);
        return fallbackResults.Length > 0
            ? fallbackResults[0]
            : new DispatchResult(
                task.TaskId,
                Success: false,
                OutputPath: task.OutputPath,
                Duration: TimeSpan.Zero,
                Error: "Local fallback returned no result"
            );
    }

    /// <summary>
    /// Reports the outcome to the registry for health tracking. Gracefully
    /// no-ops when the registry implementation doesn't track health — keeps
    /// plugin registries that only implement IRemoteWorkerRegistry working.
    /// </summary>
    private void RecordOutcome(string workerId, bool success)
    {
        if (registry is InMemoryRemoteWorkerRegistry healthTracking)
            healthTracking.RecordTaskOutcome(workerId, success);
    }
}
