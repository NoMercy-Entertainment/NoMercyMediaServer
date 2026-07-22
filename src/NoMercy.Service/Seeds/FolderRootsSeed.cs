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
using NoMercy.Storage;
using Serilog.Events;

namespace NoMercy.Service.Seeds;

public static class FolderRootsSeed
{
    public static async Task Init(
        this MediaContext dbContext,
        IStorage storage,
        IStorageDriver storageDriver
    )
    {
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
        // over HTTP. The middleware resolves the actual backend per-request
        // via IStorageFactory using DriverId + sub-path. Wrap each registration
        // so a single bad row (empty path, unreachable mount) doesn't take down
        // the whole boot path silently.
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
    }
}
