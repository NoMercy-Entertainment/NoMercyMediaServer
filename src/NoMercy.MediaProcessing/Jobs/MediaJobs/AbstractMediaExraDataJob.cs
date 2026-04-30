// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NoMercy.Storage;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public abstract class AbstractMediaExraDataJob<T> : IShouldQueue, IJobStorageInjector
{
    public abstract string QueueName { get; }
    public abstract int Priority { get; }

    private T? _storage;

    public T Storage
    {
        get => _storage ??= default!;
        set => _storage = value;
    }

    [JsonIgnore]
    public IStorageFactory StorageFactory { get; set; } = null!;

    [JsonIgnore]
    public IStorageBackend StorageBackend { get; set; } = null!;

    public abstract Task Handle();

    public void InjectStorageServices(IServiceProvider serviceProvider)
    {
        StorageFactory = serviceProvider.GetRequiredService<IStorageFactory>();
        StorageBackend = serviceProvider.GetRequiredService<IStorageBackend>();
    }

    public void Dispose()
    {
        _storage = default;
    }
}
