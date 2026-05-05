using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Middleware;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Helpers.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Service.Seeds;

public static class FolderRootsSeed
{
    public static async Task Init(this MediaContext dbContext)
    {
        if (!File.Exists(AppFiles.FolderRootsSeedFile))
            return;

        Logger.Setup("Adding Folder Roots", LogEventLevel.Verbose);

        Folder[] folders =
            File.ReadAllTextAsync(AppFiles.FolderRootsSeedFile).Result.FromJson<Folder[]>() ?? [];

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
        // over HTTP. Filtering by Directory.Exists silently dropped folders
        // whose path wasn't reachable yet (e.g. NFS / SMB mounts not ready at
        // boot), and they 404'd for the rest of the process lifetime with no
        // log entry to point at the cause.
        foreach (Folder folder in folders)
        {
            try
            {
                DynamicStaticFilesMiddleware.AddPath(folder.Id, folder.Path);
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
