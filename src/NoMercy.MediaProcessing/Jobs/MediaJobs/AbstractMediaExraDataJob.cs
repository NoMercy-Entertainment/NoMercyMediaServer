// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public abstract class AbstractMediaExraDataJob<T> : IShouldQueue
{
    public abstract string QueueName { get; }
    public abstract int Priority { get; }

    private T? _storage;

    public T Storage
    {
        get => _storage ??= default!;
        set => _storage = value;
    }

    public abstract Task Handle();

    public void Dispose()
    {
        _storage = default;
    }
}