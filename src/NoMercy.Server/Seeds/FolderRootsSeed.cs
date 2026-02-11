using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Server.Seeds;

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
