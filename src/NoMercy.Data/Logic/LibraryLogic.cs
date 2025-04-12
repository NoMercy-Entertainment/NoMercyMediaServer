using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Jobs;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.Queue;
using Serilog.Events;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.Data.Logic;

public class LibraryLogic(Ulid id) : IDisposable, IAsyncDisposable
{
    private readonly MediaContext _mediaContext = new();
    private Library Library { get; set; } = new();

    public Ulid Id { get; set; } = id;

    private int Depth { get; set; }

    public List<dynamic> Titles { get; } = [];
    private List<string> Paths { get; } = [];

    public async Task<bool> Process()
    {
        Library? library = await _mediaContext.Libraries
            .AsNoTracking()
            .Include(library => library.FolderLibraries)
            .ThenInclude(folderLibrary => folderLibrary.Folder)
            .FirstOrDefaultAsync(library => library.Id == Id);

        if (library is null) return false;

        Library = library;

        Paths.AddRange(Library.FolderLibraries
            .Select(folderLibrary => folderLibrary.Folder.Path));

        GetDepth();

        await ScanFolder();

        await Store();

        return true;
    }

    private async Task Store()
    {
        await Task.CompletedTask;
    }

    private void GetDepth()
    {
        Depth = Library.Type switch
        {
            "music" => 3,
            _ => 1
        };
    }

    private async Task ScanFolder()
    {
        foreach (string path in Paths)
            switch (Library?.Type)
            {
                case "music":
                    await ScanAudioFolder(path);
                    break;
            }

        Logger.App("Scanning done");
    }

    private async Task ScanAudioFolder(string path)
    {
        await using MediaScan mediaScan = new();
        IEnumerable<MediaFolderExtend> rootFolders = (await mediaScan
                .DisableRegexFilter()
                .Process(path, 2))
            .SelectMany(r => r.SubFolders ?? [])
            .ToList();

        foreach (MediaFolderExtend rootFolder in rootFolders)
        {
            if (rootFolder.Path == path) return;

            Titles.Add(rootFolder.Path);

            Logger.App($"Processing {rootFolder.Path}", LogEventLevel.Verbose);

            MusicJob musicJob = new(rootFolder.Path, Library);
            JobDispatcher.Dispatch(musicJob, "queue", 5);
        }

        Logger.App("Found " + Titles.Count + " subfolders");
    }

    ~LibraryLogic()
    {
        Dispose();
    }

    public void Dispose()
    {
        _mediaContext.Dispose();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    public async ValueTask DisposeAsync()
    {
        await _mediaContext.DisposeAsync();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}