// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Middleware;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Authorization;
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
        if (!storage.Exists(path: AppFiles.LibrariesSeedFile))
            return;
        Logger.Setup(message: "Adding Libraries", level: LogEventLevel.Verbose);

        List<LibrarySeedDto> librarySeed =
            storage
                .ReadAllTextAsync(path: AppFiles.LibrariesSeedFile, ct: CancellationToken.None)
                .Result.FromJson<List<LibrarySeedDto>>()
            ?? [];

        List<Library> libraries = librarySeed
            .Select(selector: librarySeedDto => new Library
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
                .Libraries.UpsertRange(entities: libraries)
                .On(match: v => new { v.Id })
                .WhenMatched(
                    updater: (vs, vi) =>
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
            Logger.Setup(message: e.Message, level: LogEventLevel.Fatal);
        }

        if (!storage.Exists(path: AppFiles.FolderRootsSeedFile))
            return;
        Logger.Setup(message: "Adding Folder Roots", level: LogEventLevel.Verbose);

        Folder[] folders =
            storage
                .ReadAllTextAsync(path: AppFiles.FolderRootsSeedFile, ct: CancellationToken.None)
                .Result.FromJson<Folder[]>()
            ?? [];

        try
        {
            await dbContext
                .Folders.UpsertRange(entities: folders)
                .On(match: v => new { v.Id })
                .WhenMatched(updater: (vs, vi) => new() { Id = vi.Id, Path = vi.Path })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(message: e.Message, level: LogEventLevel.Fatal);
        }

        // Register seeded folders with the middleware so they can serve files
        // over HTTP. Per-request resolution through IStorageFactory. Wrap each
        // registration so a single bad row doesn't silently drop folders or
        // crash boot.
        foreach (Folder folder in folders)
        {
            try
            {
                DynamicStaticFilesMiddleware.AddFolder(folderId: folder.Id, driverId: folder.DriverId, subPath: folder.Path);
            }
            catch (Exception ex)
                when (ex
                        is DirectoryNotFoundException
                            or IOException
                            or UnauthorizedAccessException
                            or ArgumentException
                )
            {
                Logger.Setup(
                    message: $"[FolderRegistration] folder {folder.Id} not registered — '{folder.Path}': {ex.Message}",
                    level: LogEventLevel.Warning
                );
            }
        }

        await UserCache.Current.RefreshFolderIdsAsync(context: dbContext);

        List<FolderLibrary> libraryFolders = [];

        foreach (LibrarySeedDto library in librarySeed)
        foreach (FolderSeedDto folder in library.Folders)
            libraryFolders.Add(item: new(folderId: folder.Id, libraryId: library.Id));

        try
        {
            await dbContext
                .FolderLibrary.UpsertRange(entities: libraryFolders)
                .On(match: v => new { v.FolderId, v.LibraryId })
                .WhenMatched(updater: (vs, vi) => new() { FolderId = vi.FolderId, LibraryId = vi.LibraryId })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(message: e.Message, level: LogEventLevel.Fatal);
        }
    }
}
