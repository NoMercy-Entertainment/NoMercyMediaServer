using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Server.Seeds.Dto;
using Serilog.Events;

namespace NoMercy.Server.Seeds;

public static class LibrariesSeed
{
    public static async Task Init(this MediaContext dbContext)
    {
        if (!File.Exists(AppFiles.LibrariesSeedFile)) return;
        Logger.Setup("Adding Libraries", LogEventLevel.Verbose);

        List<LibrarySeedDto> librarySeed = File.ReadAllTextAsync(AppFiles.LibrariesSeedFile)
            .Result.FromJson<List<LibrarySeedDto>>() ?? [];

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
                Order = librarySeedDto.Order
            })
            .ToList();

        try
        {
            await dbContext.Libraries.UpsertRange(libraries)
                .On(v => new { v.Id })
                .WhenMatched((vs, vi) => new()
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
                    Order = vi.Order
                })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }
        
        if (!File.Exists(AppFiles.FolderRootsSeedFile)) return;
        Logger.Setup("Adding Folder Roots", LogEventLevel.Verbose);
        
        Folder[] folders = File.ReadAllTextAsync(AppFiles.FolderRootsSeedFile)
            .Result.FromJson<Folder[]>() ?? [];

        try
        {
            await dbContext.Folders.UpsertRange(folders)
                .On(v => new { v.Id })
                .WhenMatched((vs, vi) => new()
                {
                    Id = vi.Id,
                    Path = vi.Path
                })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }

        List<FolderLibrary> libraryFolders = [];

        foreach (LibrarySeedDto library in librarySeed)
            foreach (FolderSeedDto folder in library.Folders)
                libraryFolders.Add(new(folder.Id, library.Id));

        try
        {
            await dbContext.FolderLibrary
                .UpsertRange(libraryFolders)
                .On(v => new { v.FolderId, v.LibraryId })
                .WhenMatched((vs, vi) => new()
                {
                    FolderId = vi.FolderId,
                    LibraryId = vi.LibraryId
                })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }
    }
}
