using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Middleware;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Helpers.Extensions;
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
        // over HTTP. The middleware resolves the actual backend per-request
        // via IStorageFactory using DriverId + sub-path. Wrap each registration
        // so a single bad row (empty path, unreachable mount) doesn't take down
        // the whole boot path silently.
        foreach (Folder folder in folders)
        {
            try
            {
                DynamicStaticFilesMiddleware.AddFolder(folder.Id, folder.DriverId, folder.Path);
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
                    $"[FolderRegistration] folder {folder.Id} not registered — '{folder.Path}': {ex.Message}",
                    LogEventLevel.Warning
                );
            }
        }

        await ClaimsPrincipleExtensions.RefreshFolderIdsAsync(dbContext);
    }
}
