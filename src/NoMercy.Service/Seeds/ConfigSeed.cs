using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;
using ConfigurationModel = NoMercy.Database.Models.Common.Configuration;

namespace NoMercy.Service.Seeds;

public static class ConfigSeed
{
    public static async Task Init(this MediaContext dbContext)
    {
        Logger.Setup("Adding Configurations", LogEventLevel.Verbose);
        ConfigurationModel[] configs =
        [
            new() { Key = "internalPort", Value = Config.InternalServerPort.ToString() },
            new() { Key = "externalPort", Value = Config.ExternalServerPort.ToString() },
        ];

        try
        {
            await dbContext
                .Configuration.UpsertRange(configs)
                .On(v => new { v.Key })
                .WhenMatched((_, vi) => new() { Key = vi.Key, Value = vi.Value })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }
    }
}
