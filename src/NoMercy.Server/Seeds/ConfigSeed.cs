using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Server.Seeds;

public static class ConfigSeed
{
    public static async Task Init(this MediaContext dbContext)
    {
        try
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

            await dbContext.Configuration
                .UpsertRange(configs)
                .On(v => new { v.Key })
                .WhenMatched((_, vi) => new()
                {
                    Key = vi.Key,
                    Value = vi.Value,
                    UpdatedAt = DateTime.Now
                })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }
    }
}
