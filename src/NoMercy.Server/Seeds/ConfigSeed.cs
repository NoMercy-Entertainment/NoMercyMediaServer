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
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Server.Seeds;

public static class ConfigSeed
{
    public static async Task Init(this MediaContext dbContext)
    {
        Logger.Setup("Adding Configurations", LogEventLevel.Verbose);
        Configuration[] configs =
        [
            new()
            {
                Key = "internalPort",
                Value = Config.InternalServerPort.ToString()
            },
            new()
            {
                Key = "externalPort",
                Value = Config.ExternalServerPort.ToString()
            }
        ];
        
        try
        {
            await dbContext.Configuration
                .UpsertRange(configs)
                .On(v => new { v.Key })
                .WhenMatched((_, vi) => new()
                {
                    Key = vi.Key,
                    Value = vi.Value
                })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }
    }
}
