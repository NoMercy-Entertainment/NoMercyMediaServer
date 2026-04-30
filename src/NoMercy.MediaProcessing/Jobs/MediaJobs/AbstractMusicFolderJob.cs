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
public abstract class AbstractMusicFolderJob : IShouldQueue, IJobStorageInjector
{
    public string InputFolder { get; set; } = string.Empty;
    public Ulid LibraryId { get; set; }
    public Ulid FolderId { get; set; }
    public Guid ReleaseId { get; set; }

    [JsonIgnore]
    public IStorageFactory StorageFactory { get; set; } = null!;

    [JsonIgnore]
    public IStorageBackend StorageBackend { get; set; } = null!;

    public abstract string QueueName { get; }
    public abstract int Priority { get; }

    public abstract Task Handle();

    public void InjectStorageServices(IServiceProvider serviceProvider)
    {
        StorageFactory = serviceProvider.GetRequiredService<IStorageFactory>();
        StorageBackend = serviceProvider.GetRequiredService<IStorageBackend>();
    }

    public void Dispose() { }
}
