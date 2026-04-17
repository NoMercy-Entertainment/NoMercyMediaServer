namespace NoMercy.Encoder.Distribution;

public interface IWorkerAssigner
{
    /// <summary>
    /// Distributes <paramref name="tasks"/> across <paramref name="workers"/>
    /// so each worker receives roughly as much work as it can handle given
    /// its <see cref="WorkerCapacity.SpeedMultiplier"/>. Returns one slot per
    /// worker, keyed by <see cref="WorkerCapacity.WorkerId"/>. Workers with
    /// no tasks still appear in the output with an empty array so callers
    /// can reason about idle workers uniformly.
    /// </summary>
    Dictionary<string, EncodeTask[]> Assign(
        EncodeTask[] tasks,
        IReadOnlyList<WorkerCapacity> workers
    );
}
