using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Setup;

public static class UserSettings
{
    public static bool TryGetUserSettings(out Dictionary<string, string> settings)
    {
        settings = new();

        try
        {
            using MediaContext mediaContext = new();
            List<Configuration> configuration = mediaContext.Configuration.ToList();
            
            foreach (Configuration config in configuration)
            {
                switch (config.Key)
                {
                    case "internalPort" when Config.InternalServerPort != int.Parse(config.Value):
                        config.Value = Config.InternalServerPort.ToString();
                        mediaContext.Configuration.Upsert(new()
                            {
                            Key = config.Key,
                            Value = config.Value
                        }).On(c => c.Key)
                        .Run();
                        break;
                    case "externalPort" when Config.ExternalServerPort != int.Parse(config.Value):
                        config.Value = Config.ExternalServerPort.ToString();
                        mediaContext.Configuration.Upsert(new()
                            {
                            Key = config.Key,
                            Value = config.Value
                        }).On(c => c.Key)
                        .Run();
                        break;
                }
                settings[config.Key] = config.Value;
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static void ApplySettings(Dictionary<string, string> settings, bool silent = false)
    {
        using MediaContext mediaContext = new();
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
                    mediaContext.Configuration.Upsert(new()
                        {
                        Key = setting.Key,
                        Value = Config.InternalServerPort.ToString()
                    }).On(c => c.Key)
                    .Run();
                break;
                case "externalPort" when Config.ExternalServerPort == int.Parse(setting.Value):
                    Config.ExternalServerPort = int.Parse(setting.Value);
                    break;
                case "externalPort" when Config.ExternalServerPort != int.Parse(setting.Value):
                    Config.ExternalServerPort = int.Parse(setting.Value);
                    mediaContext.Configuration.Upsert(new()
                        {
                            Key = setting.Key,
                            Value = Config.ExternalServerPort.ToString()
                        }).On(c => c.Key)
                        .Run();
                    break;
                case "queueRunners":
                    Config.QueueWorkers = new(Config.QueueWorkers.Key, setting.Value.ToInt());
                    break;
                case "encoderRunners":
                    Config.EncoderWorkers = new(Config.EncoderWorkers.Key, setting.Value.ToInt());
                    break;
                case "cronRunners":
                    Config.CronWorkers = new(Config.CronWorkers.Key, setting.Value.ToInt());
                    break;
                case "dataRunners":
                    Config.DataWorkers = new(Config.DataWorkers.Key, setting.Value.ToInt());
                    break;
                case "imageRunners":
                    Config.ImageWorkers = new(Config.ImageWorkers.Key, setting.Value.ToInt());
                    break;
                case "requestRunners":
                    Config.RequestWorkers = new(Config.RequestWorkers.Key, setting.Value.ToInt());
                    break;
                case "swagger":
                    Config.Swagger = setting.Value.ToBoolean();
                    break;
            }
        }
    }
}