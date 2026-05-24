using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Setup.Ui;

public static class UserSettings
{
    public static bool TryGetUserSettings(out Dictionary<string, string> settings)
    {
        settings = new();

        try
        {
            using AppDbContext appContext = new();
            List<Configuration> configuration = appContext.Configuration.ToList();

            foreach (Configuration config in configuration)
            {
                switch (config.Key)
                {
                    case "internalPort" when Config.InternalServerPort != int.Parse(config.Value):
                        config.Value = Config.InternalServerPort.ToString();
                        appContext
                            .Configuration.Upsert(new() { Key = config.Key, Value = config.Value })
                            .On(c => c.Key)
                            .Run();
                        break;
                    case "externalPort" when Config.ExternalServerPort != int.Parse(config.Value):
                        config.Value = Config.ExternalServerPort.ToString();
                        appContext
                            .Configuration.Upsert(new() { Key = config.Key, Value = config.Value })
                            .On(c => c.Key)
                            .Run();
                        break;
                }
                settings[config.Key] = config.Value;
            }

            Logger.App(
                $"UserSettings: loaded {settings.Count} key(s) from Configuration table",
                LogEventLevel.Information
            );
            return true;
        }
        catch (Exception ex)
        {
            // Silent failure here is the original "worker counts reset on
            // boot" bug — every saved value gets discarded if the read throws
            // (schema not yet ready, SecureValue conversion error, etc.).
            // Surface the cause so operators can act.
            Logger.App(
                $"UserSettings: failed to read Configuration table — using defaults this boot. "
                    + $"{ex.GetType().Name}: {ex.Message}",
                LogEventLevel.Error
            );
            return false;
        }
    }

    public static void ApplySettings(Dictionary<string, string> settings, bool silent = false)
    {
        using AppDbContext appContext = new();
        foreach (KeyValuePair<string, string> setting in settings)
        {
            if (!silent)
                Logger.App($"Configuration: {setting.Key} = {setting.Value}");

            switch (setting.Key)
            {
                case "internalPort" when Config.InternalServerPort == int.Parse(setting.Value):
                    Config.InternalServerPort = int.Parse(setting.Value);
                    break;
                case "internalPort" when Config.InternalServerPort != int.Parse(setting.Value):
                    Config.InternalServerPort = int.Parse(setting.Value);
                    appContext
                        .Configuration.Upsert(
                            new()
                            {
                                Key = setting.Key,
                                Value = Config.InternalServerPort.ToString(),
                            }
                        )
                        .On(c => c.Key)
                        .Run();
                    break;
                case "externalPort" when Config.ExternalServerPort == int.Parse(setting.Value):
                    Config.ExternalServerPort = int.Parse(setting.Value);
                    break;
                case "externalPort" when Config.ExternalServerPort != int.Parse(setting.Value):
                    Config.ExternalServerPort = int.Parse(setting.Value);
                    appContext
                        .Configuration.Upsert(
                            new()
                            {
                                Key = setting.Key,
                                Value = Config.ExternalServerPort.ToString(),
                            }
                        )
                        .On(c => c.Key)
                        .Run();
                    break;
                case "libraryRunners":
                    Config.LibraryWorkers = new(Config.LibraryWorkers.Key, setting.Value.ToInt());
                    break;
                case "importRunners" or "queueRunners":
                    Config.ImportWorkers = new(Config.ImportWorkers.Key, setting.Value.ToInt());
                    break;
                case "extrasRunners" or "dataRunners":
                    Config.ExtrasWorkers = new(Config.ExtrasWorkers.Key, setting.Value.ToInt());
                    break;
                case "encoderRunners":
                    Config.EncoderWorkers = new(Config.EncoderWorkers.Key, setting.Value.ToInt());
                    Logger.App(
                        $"UserSettings: Config.EncoderWorkers loaded as {Config.EncoderWorkers.Value} (DB value '{setting.Value}')"
                    );
                    break;
                case "cronRunners":
                    Config.CronWorkers = new(Config.CronWorkers.Key, setting.Value.ToInt());
                    break;
                case "imageRunners":
                    Config.ImageWorkers = new(Config.ImageWorkers.Key, setting.Value.ToInt());
                    break;
                case "fileRunners":
                    Config.FileWorkers = new(Config.FileWorkers.Key, setting.Value.ToInt());
                    break;
                case "musicRunners":
                    Config.MusicWorkers = new(Config.MusicWorkers.Key, setting.Value.ToInt());
                    break;
                case "swagger":
                    Config.Swagger = setting.Value.ToBoolean();
                    break;
            }
        }
    }
}
