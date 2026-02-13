using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Service.Seeds;

public static class FolderRootsSeed
{
    public static async Task Init(this MediaContext dbContext)
    {
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
    }
}
