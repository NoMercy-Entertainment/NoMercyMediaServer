using NoMercy.Data.Logic;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem;
using NoMercy.Queue;

namespace NoMercy.Data.Jobs;

[Serializable]
public class MusicJob : IShouldQueue, IDisposable, IAsyncDisposable
{
    private readonly MediaContext _mediaContext = new();

    public string? Folder { get; set; }
    public Library? Library { get; set; }

    public MusicJob()
    {
        //
    }


    public MusicJob(string folder, Library library)
    {
        Folder = folder;
        Library = library;
    }

    public async Task Handle()
    {
        if (Folder is null) return;
        if (Library is null) return;

        await using MediaScan mediaScan = new();
        IEnumerable<MediaFolder> mediaFolder = await mediaScan
            .EnableFileListing()
            .DisableRegexFilter()
            .Process(Folder, 20);

        foreach (MediaFolder list in mediaFolder)
        {
            Logger.App($"Music {list.Path}: Processing");

            MusicLogic music = new(Library, list);
            await music.Process();
        }
    }

    ~MusicJob()
    {
        Dispose();
    }

    public void Dispose()
    {
        _mediaContext.Dispose();
        GC.Collect();
        GC.WaitForFullGCComplete();
        GC.WaitForPendingFinalizers();
    }

    public async ValueTask DisposeAsync()
    {
        await _mediaContext.DisposeAsync();
        GC.Collect();
        GC.WaitForFullGCComplete();
        GC.WaitForPendingFinalizers();
    }
}