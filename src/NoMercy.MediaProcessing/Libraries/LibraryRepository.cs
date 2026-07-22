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
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.Libraries;

public class LibraryRepository(MediaContext context, IStorageDriver storageDriver)
    : ILibraryRepository
{
    public async Task<IEnumerable<MediaFolderExtend>> GetRootFoldersAsync(string path)
    {
        await using MediaScan mediaScan = new(driver: storageDriver);
        return (await mediaScan.DisableRegexFilter().Process(rootFolder: path, depth: 2))
            .SelectMany(selector: r => r.SubFolders ?? [])
            .ToList();
    }

    public Task<Library?> GetLibraryWithFolders(Ulid id)
    {
        return context
            .Libraries.AsNoTracking()
            .Include(navigationPropertyPath: library => library.FolderLibraries)
                .ThenInclude(navigationPropertyPath: folderLibrary => folderLibrary.Folder)
            .FirstOrDefaultAsync(predicate: library => library.Id == id);
    }

    public Task<Folder?> GetLibraryFolder(Ulid folderId)
    {
        return context
            .Folders.Include(navigationPropertyPath: folder => folder.FolderLibraries)
                .ThenInclude(navigationPropertyPath: folderLibrary => folderLibrary.Library)
                    .ThenInclude(navigationPropertyPath: f => f.FolderLibraries)
                        .ThenInclude(navigationPropertyPath: f => f.Folder)
            .Include(navigationPropertyPath: folder => folder.EncodingPresetFolders)
                .ThenInclude(navigationPropertyPath: link => link.Preset)
            .FirstOrDefaultAsync(predicate: folder => folder.Id == folderId);
    }

    public Task<Library?> GetLibraryByIdWithFolders(Ulid libraryId)
    {
        return context
            .Libraries.AsNoTracking()
            .Include(navigationPropertyPath: library => library.FolderLibraries)
                .ThenInclude(navigationPropertyPath: folderLibrary => folderLibrary.Folder)
            .FirstOrDefaultAsync(predicate: library => library.Id == libraryId);
    }

    public async Task<HashSet<string>> GetExistingFolderNamesAsync(
        Ulid libraryId,
        string libraryType
    )
    {
        IEnumerable<string?> folders = libraryType switch
        {
            MediaTypes.MovieMediaType => await context
                .LibraryMovie.Where(predicate: lm => lm.LibraryId == libraryId)
                .Include(navigationPropertyPath: lm => lm.Movie)
                .Select(selector: lm => lm.Movie.Folder)
                .ToListAsync(),
            _ => await context
                .LibraryTv.Where(predicate: lt => lt.LibraryId == libraryId)
                .Include(navigationPropertyPath: lt => lt.Tv)
                .Select(selector: lt => lt.Tv.Folder)
                .ToListAsync(),
        };

        return folders
            .Where(predicate: f => f is not null)
            .Select(selector: f => f!.Replace(oldValue: "/", newValue: "").NormalizeForComparison())
            .ToHashSet();
    }

    public void Dispose()
    {
        context.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await context.DisposeAsync();
    }
}
