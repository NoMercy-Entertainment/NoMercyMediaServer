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
/// The actual network protocol (how a remote worker accepts an encode
/// task over the wire) is still pending — <see cref="IRemoteWorker"/>
/// today takes a full <c>EncodingJob</c>, not an <c>EncodeTask</c>.
/// Once the task-level remote execution interface lands this dispatcher
/// invokes workers directly; for now the capacity computation + assignment
/// run so the seam is exercised, but results come from the local fallback.
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

        // Wire the capacity + assignment plumbing even though the network
        // protocol isn't implemented — when IRemoteWorker gains an
        // ExecuteTaskAsync(EncodeTask) method, this dispatcher fills in.
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
        logger.LogInformation(
            "Task-level remote execution not wired — computed {Count} assignments across {Workers} workers, running locally",
            tasks.Length,
            remoteWorkers.Count
        );
        _ = assignments; // Observed for future use.

        return await localFallback.DispatchAsync(tasks, ct).ConfigureAwait(false);
    }
}
