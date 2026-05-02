using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Middleware;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Helpers.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Service.Seeds.Dto;
using NoMercy.Storage;
using Serilog.Events;

namespace NoMercy.Service.Seeds;

public static class LibrariesSeed
{
    public static async Task Init(
        this MediaContext dbContext,
        IStorage storage,
        IStorageDriver storageDriver
    )
    {
        if (!storage.Exists(AppFiles.LibrariesSeedFile))
            return;
        Logger.Setup("Adding Libraries", LogEventLevel.Verbose);

        List<LibrarySeedDto> librarySeed =
            storage
                .ReadAllTextAsync(AppFiles.LibrariesSeedFile, CancellationToken.None)
                .Result.FromJson<List<LibrarySeedDto>>()
            ?? [];

        List<Library> libraries = librarySeed
            .Select(librarySeedDto => new Library
            {
                Id = librarySeedDto.Id,
                AutoRefreshInterval = librarySeedDto.AutoRefreshInterval,
                ChapterImages = librarySeedDto.ChapterImages,
                ExtractChapters = librarySeedDto.ExtractChapters,
                ExtractChaptersDuring = librarySeedDto.ExtractChaptersDuring,
                Image = librarySeedDto.Image,
                PerfectSubtitleMatch = librarySeedDto.PerfectSubtitleMatch,
                Realtime = librarySeedDto.Realtime,
                SpecialSeasonName = librarySeedDto.SpecialSeasonName,
                Title = librarySeedDto.Title,
                Type = librarySeedDto.Type,
                Order = librarySeedDto.Order,
            })
            .ToList();

        try
        {
            await dbContext
                .Libraries.UpsertRange(libraries)
                .On(v => new { v.Id })
                .WhenMatched(
                    (vs, vi) =>
                        new()
                        {
                            Id = vi.Id,
                            AutoRefreshInterval = vi.AutoRefreshInterval,
                            ChapterImages = vi.ChapterImages,
                            ExtractChapters = vi.ExtractChapters,
                            ExtractChaptersDuring = vi.ExtractChaptersDuring,
                            Image = vi.Image,
                            PerfectSubtitleMatch = vi.PerfectSubtitleMatch,
                            Realtime = vi.Realtime,
                            SpecialSeasonName = vi.SpecialSeasonName,
                            Title = vi.Title,
                            Type = vi.Type,
                            Order = vi.Order,
                        }
                )
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }

        if (!storage.Exists(AppFiles.FolderRootsSeedFile))
            return;
        Logger.Setup("Adding Folder Roots", LogEventLevel.Verbose);

        Folder[] folders =
            storage
                .ReadAllTextAsync(AppFiles.FolderRootsSeedFile, CancellationToken.None)
                .Result.FromJson<Folder[]>()
            ?? [];

        try
        {
            await dbContext
                .Folders.UpsertRange(folders)
                .On(v => new { v.Id })
                .WhenMatched((vs, vi) => new() { Id = vi.Id, Path = vi.Path })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }

        // Register seeded folders with the middleware so they can serve files
        // over HTTP. Per-request resolution through IStorageFactory.
        foreach (Folder folder in folders)
            DynamicStaticFilesMiddleware.AddFolder(folder.Id, folder.DriverId, folder.Path);

        await ClaimsPrincipleExtensions.RefreshFolderIdsAsync(dbContext);

        List<FolderLibrary> libraryFolders = [];

        foreach (LibrarySeedDto library in librarySeed)
        foreach (FolderSeedDto folder in library.Folders)
            libraryFolders.Add(new(folder.Id, library.Id));

        try
        {
            await dbContext
                .FolderLibrary.UpsertRange(libraryFolders)
                .On(v => new { v.FolderId, v.LibraryId })
                .WhenMatched((vs, vi) => new() { FolderId = vi.FolderId, LibraryId = vi.LibraryId })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }
    }
}
