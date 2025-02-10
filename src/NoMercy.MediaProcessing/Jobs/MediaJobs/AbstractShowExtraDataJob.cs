// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using NoMercy.Queue;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public abstract class AbstractShowExtraDataJob<T, TS> : IShouldQueue
{
    public abstract string QueueName { get; }
    public abstract int Priority { get; }

    public TS Name { get; set; } = default!;
    private T[]? _storage;

    public IEnumerable<T> Storage
    {
        get => _storage ??= [];
        set => _storage = value.ToArray();
    }

    public abstract Task Handle();

    public void Dispose()
    {
        _storage = null;
        GC.Collect();
        GC.WaitForFullGCComplete();
        GC.WaitForPendingFinalizers();
    }
}