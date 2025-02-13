using CommandLine;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using Serilog.Events;

namespace NoMercy.Server;

public class StartupOptions
{
    // dev
    [Option('d', "dev", Required = false, HelpText = "Run the server in development mode.")]
    public bool Dev { get; set; }
    
    [Option('l', "loglevel", Required = false, HelpText = "Run the server in development mode.")]
    public string LogLevel { get; set; } = LogEventLevel.Information.ToString();
    
    [Option("seed", Required = false, HelpText = "Run the server in development mode.")]
    public bool Seed { get; set; }
    
    [Option('i', "internal-port", Required = false, HelpText = "Internal port to use for the server.")]
    public int InternalPort { get; set; }
    
    [Option('e', "external-port", Required = false, HelpText = "External port to use for the server.")]
    public int ExternalPort { get; set; }

    public void ApplySettings(out bool shouldSeedMarvel)
    {
        shouldSeedMarvel = false;

        if (Dev)
        {
            Logger.App("Running in development mode.");

            Config.IsDev = true;

            Config.AppBaseUrl = "https://app-dev.nomercy.tv/";
            Config.ApiBaseUrl = "https://api-dev.nomercy.tv/";
            Config.ApiServerBaseUrl = $"{Config.ApiBaseUrl}v1/server/";

            Config.AuthBaseUrl = "https://auth-dev.nomercy.tv/realms/NoMercyTV/";
            Config.TokenClientSecret = "1lHWBazSTHfBpuIzjAI6xnNjmwUnryai";
        }

        if (Seed)
        {
            Logger.App("Seeding database.");
            shouldSeedMarvel = true;
        }

        if (!string.IsNullOrEmpty(LogLevel))
        {
            Logger.App($"Setting log level to: {LogLevel}.");
            Logger.SetLogLevel(Enum.Parse<LogEventLevel>(LogLevel.ToTitleCase()));
        }

        if (InternalPort != 0)
        {
            Logger.App("Setting internal port to " + InternalPort);
            Config.InternalServerPort = InternalPort;
        }

        if (ExternalPort != 0)
        {
            Logger.App("Setting external port to " + ExternalPort);
            Config.ExternalServerPort = ExternalPort;
        }
       
    }

}