using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NoMercy.Data.Logic;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.Storage;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.Data.Jobs;

[Serializable]
public class MusicJob : IShouldQueue, IJobStorageInjector, IDisposable, IAsyncDisposable
{
    private readonly MediaContext _mediaContext = new();

    public string QueueName => "import";
    public int Priority => 5;

    public string? Folder { get; set; }
    public Library? Library { get; set; }

    [JsonIgnore]
    public IStorageFactory StorageFactory { get; set; } = null!;

    [JsonIgnore]
    public IStorageDriver storageDriver { get; set; } = null!;

    public MusicJob()
    {
        //
    }

    public MusicJob(string folder, Library library)
    {
        Folder = folder;
        Library = library;
    }

    public void InjectStorageServices(IServiceProvider serviceProvider)
    {
        StorageFactory = serviceProvider.GetRequiredService<IStorageFactory>();
        storageDriver = serviceProvider.GetRequiredService<IStorageDriver>();
    }

    public async Task Handle()
    {
        if (Folder is null)
            return;
        if (Library is null)
            return;

        await using MediaScan mediaScan = new(storageDriver);
        IEnumerable<MediaFolderExtend> mediaFolder = await mediaScan
            .EnableFileListing()
            .DisableRegexFilter()
            .Process(Folder, 20);

        foreach (MediaFolderExtend list in mediaFolder)
        {
            Logger.App($"Music {list.Path}: Processing");

            MusicLogic music = new(Library, list, _mediaContext);
            await music.Process();
        }
    }

    public void Dispose()
    {
        _mediaContext.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _mediaContext.DisposeAsync();
    }
}
