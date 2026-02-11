using NoMercy.Data.Logic;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.Queue;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

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