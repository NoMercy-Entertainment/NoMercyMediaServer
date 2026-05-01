// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NoMercy.MediaProcessing.Jobs.Dto;
using NoMercy.NmSystem.Dto;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Storage;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public abstract class AbstractMusicEncoderJob : IShouldQueue, IJobStorageInjector
{
    public Ulid LibraryId { get; set; }
    public Guid Id { get; set; }

    public Ulid FolderId { get; set; }

    public MusicBrainzTrack FoundTrack { get; set; } = null!;
    public FolderMetadata FolderMetaData { get; set; } = null!;
    public MediaFile MediaFile { get; set; } = null!;

    public string InputFolder { get; set; } = string.Empty;

    public string InputFile { get; set; } = string.Empty;

    [JsonIgnore]
    public IStorageFactory StorageFactory { get; set; } = null!;

    [JsonIgnore]
    public IStorageDriver StorageDriver { get; set; } = null!;

    public abstract string QueueName { get; }
    public abstract int Priority { get; }

    public abstract Task Handle();

    public void InjectStorageServices(IServiceProvider serviceProvider)
    {
        StorageFactory = serviceProvider.GetRequiredService<IStorageFactory>();
        StorageDriver = serviceProvider.GetRequiredService<IStorageDriver>();
    }

    public void Dispose() { }
}
