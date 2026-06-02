using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Jobs;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.NmSystem.Dto;
using NoMercy.Storage;
using NoMercyQueue;
using Serilog.Events;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.Data.Logic;

public class LibraryLogic(
    Ulid id,
    MediaContext mediaContext,
    IStorageDriver storageDriver,
    IStorageFactory storageFactory
) : IDisposable, IAsyncDisposable
{
    private readonly IStorageDriver _storageDriver = storageDriver;
    private Library Library { get; set; } = new();

    public Ulid Id { get; set; } = id;

    private int Depth { get; set; }

    public List<dynamic> Titles { get; } = [];
    private List<Folder> FolderList { get; } = [];

    public async Task<bool> Process()
    {
        Library? library = await mediaContext
            .Libraries.AsNoTracking()
            .Include(library => library.FolderLibraries)
                .ThenInclude(folderLibrary => folderLibrary.Folder)
            .FirstOrDefaultAsync(library => library.Id == Id);

        if (library is null)
            return false;

        Library = library;

        FolderList.AddRange(Library.FolderLibraries.Select(folderLibrary => folderLibrary.Folder));

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
            _ => 1,
        };
    }

    private async Task ScanFolder()
    {
        foreach (Folder folder in FolderList)
            switch (Library?.Type)
            {
                case "music":
                    await ScanAudioFolder(folder);
                    break;
            }

        Logger.App("Scanning done");
    }

    private async Task ScanAudioFolder(Folder folder)
    {
        IStorage folderStorage = storageFactory.For(folder.Id, folder.DriverId, string.Empty);
        string scanRoot = folderStorage.GetFullPath(folder.Path);

        await using MediaScan mediaScan = new(folderStorage.Driver);
        IEnumerable<MediaFolderExtend> rootFolders = (
            await mediaScan.DisableRegexFilter().Process(scanRoot, 2)
        )
            .SelectMany(r => r.SubFolders ?? [])
            .ToList();

        foreach (MediaFolderExtend rootFolder in rootFolders)
        {
            if (rootFolder.Path == scanRoot)
                return;

            Titles.Add(rootFolder.Path);

            Logger.App($"Processing {rootFolder.Path}", LogEventLevel.Verbose);

            MusicJob musicJob = new(rootFolder.Path, Library);
            QueueRunner.Current!.Dispatcher.Dispatch(musicJob);
        }

        Logger.App("Found " + Titles.Count + " subfolders");
    }

    public void Dispose()
    {
        mediaContext.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await mediaContext.DisposeAsync();
    }
}
