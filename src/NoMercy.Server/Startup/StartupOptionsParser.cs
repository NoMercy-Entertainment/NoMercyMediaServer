using CommandLine;
using NoMercy.Data.Logic;
using NoMercy.NmSystem;
using Serilog.Events;

namespace NoMercy.Server.Startup;

public class StartupOptionsParser(string[] args)
{
    public StartupOptions Options { get; private set; } = null!;

    public StartupOptions ParseAndApply()
    {
        ParseArguments();
        ApplySettings();
        return Options;
    }

    private void ParseArguments()
    {
        ParserResult<StartupOptions>? result = Parser.Default.ParseArguments<StartupOptions>(args);
        if (result is Parsed<StartupOptions> parsed)
        {
            Options = parsed.Value;
            return;
        }

        // Todo something with the errors we actually got.
        //      Maybe throw the actual errors as exceptions?
        Environment.ExitCode = -1;
        Environment.Exit(-1);

    }

    private void ApplySettings()
    {
        #region Dev
        if (Options.Dev)
        {
            Logger.App("Running in development mode.");

            Config.IsDev = true;

            Config.AppBaseUrl = "https://app-dev.nomercy.tv/";
            Config.ApiBaseUrl = "https://api-dev.nomercy.tv/";
            Config.ApiServerBaseUrl = $"{Config.ApiBaseUrl}v1/server/";

            Config.AuthBaseUrl = "https://auth-dev.nomercy.tv/realms/NoMercyTV/";
            Config.TokenClientSecret = "1lHWBazSTHfBpuIzjAI6xnNjmwUnryai";
        }
        #endregion
        #region Seeding
        if (Options.Seed)
        {
            Logger.App("Seeding database.");
            Options.ShouldSeedMarvel = true;
        }
        #endregion
        #region LogLevel
        if (!string.IsNullOrEmpty(Options.LogLevel))
        {
            Logger.App($"Setting log level to: {Options.LogLevel}.");
            Logger.SetLogLevel(Enum.Parse<LogEventLevel>(Options.LogLevel.ToTitleCase()));
        }
        #endregion
        #region Port Setup
        if (Options.InternalPort != 0)
        {
            Logger.App("Setting internal port to " + Options.InternalPort);
            Config.InternalServerPort = Options.InternalPort;
        }

        if (Options.ExternalPort != 0)
        {
            Logger.App("Setting external port to " + Options.ExternalPort);
            Config.ExternalServerPort = Options.ExternalPort;
        }
        #endregion
        
        Logger.App(Config.AuthBaseUrl);
    }
}
