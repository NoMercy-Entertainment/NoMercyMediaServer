using CommandLine;
using NoMercy.Database;
using ConfigurationModel = NoMercy.Database.Models.Common.Configuration;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Server.Seeds;
using NoMercy.Setup;
using Serilog.Events;

namespace NoMercy.Server;

public class StartupOptions
{
    // dev
    [Option('d', "dev", Required = false, HelpText = "Run the server in development mode.")]
    public bool Development { get; set; }

    [Option('l', "loglevel", Required = false, HelpText = "Run the server in development mode.")]
    public string LogLevel { get; set; } = nameof(LogEventLevel.Information);

    [Option("seed", Required = false, HelpText = "Run the server in development mode.")]
    public bool ShouldSeed { get; set; }

    [Option('i', "internal-port", Required = false, HelpText = "Internal port to use for the server.")]
    public int InternalPort { get; set; }

    [Option('x', "external-port", Required = false, HelpText = "External port to use for the server.")]
    public int ExternalPort { get; set; }

    [Option("internal-ip", Required = false, HelpText = "Internal ip to use for the server.")]
    public string? InternalIp { get; set; }

    [Option("external-ip", Required = false, HelpText = "External ip to use for the server.")]
    public string? ExternalIp { get; set; }

    [Option("pipe-name", Required = false, HelpText = "Named pipe name for IPC (Windows) or Unix socket filename.")]
    public string? PipeName { get; set; }

    [Option("service", Required = false, HelpText = "Run as a platform service (Windows SCM, Linux systemd, macOS launchd).")]
    public bool RunAsService { get; set; }

    [Option("sentry", Required = false, HelpText = "Enable Sentry.")]
    public bool Sentry { get; set; }

    [Option("dsn", Required = false, HelpText = "Sentry DSN.")]
    public string? SentryDsn { get; set; }

    public void ApplySettings()
    {
        Dictionary<string, string> options = new();
        
        DatabaseSeeder.ShouldSeedMarvel = ShouldSeed;
        if (Development)
        {
            Config.IsDev = true;

            Config.AppBaseUrl = "https://app-dev.nomercy.tv/";
            Config.ApiBaseUrl = "https://api-dev.nomercy.tv/";
            Config.ApiServerBaseUrl = $"{Config.ApiBaseUrl}v1/server/";

            Config.AuthBaseUrl = "https://auth-dev.nomercy.tv/realms/NoMercyTV/";

            Logger.App("Running in development mode.");
        }

        if (ShouldSeed) Logger.App("Seeding database.");

        if (!string.IsNullOrEmpty(LogLevel))
        {
            Logger.App($"Setting log level to: {LogLevel}.");
            Logger.SetLogLevel(Enum.Parse<LogEventLevel>(LogLevel.ToTitleCase()));
            options.Add("loglevel", LogLevel);
        }

        if (InternalPort != 0)
        {
            Logger.App("Setting internal port to " + InternalPort);
            Config.InternalServerPort = InternalPort;
            options.Add("internalPort", InternalPort.ToString());
        }
        else
        {
            InternalPort = 7626;
            try
            {
                MediaContext mediaContext = new();
                ConfigurationModel? internalPortConfig = mediaContext.Configuration
                    .FirstOrDefault(c => c.Key == "internalPort");
                if (internalPortConfig != null)
                {
                    InternalPort = int.Parse(internalPortConfig.Value);
                    Logger.App("Loaded internal port from database: " + InternalPort);
                }
                mediaContext.Dispose();
            }
            catch (Exception)
            {
                Logger.App("Database not yet initialized, using default internal port.");
            }
            Config.InternalServerPort = InternalPort;
            options.Add("internalPort", InternalPort.ToString());
        }

        if (ExternalPort != 0)
        {
            Logger.App("Setting external port to " + ExternalPort);
            Config.ExternalServerPort = ExternalPort;
            options.Add("externalPort", ExternalPort.ToString());
        }
        else
        {
            ExternalPort = 7626;
            try
            {
                MediaContext mediaContext = new();
                ConfigurationModel? externalPortConfig = mediaContext.Configuration
                    .FirstOrDefault(c => c.Key == "externalPort");
                if (externalPortConfig != null)
                {
                    ExternalPort = int.Parse(externalPortConfig.Value);
                    Logger.App("Loaded external port from database: " + ExternalPort);
                }
                mediaContext.Dispose();
            }
            catch (Exception)
            {
                Logger.App("Database not yet initialized, using default external port.");
            }
            Config.ExternalServerPort = ExternalPort;
            options.Add("externalPort", ExternalPort.ToString());
        }

        if (!string.IsNullOrEmpty(PipeName))
        {
            Logger.App("Setting IPC pipe name to " + PipeName);
            Config.ManagementPipeName = PipeName;
        }

        if (!string.IsNullOrEmpty(InternalIp))
        {
            Logger.App("Setting internal ip to " + InternalIp);
            Networking.Networking.InternalIp = InternalIp;
            options.Add("internalIp", InternalIp);
        }

        if (!string.IsNullOrEmpty(ExternalIp))
        {
            Logger.App("Setting external ip to " + ExternalIp);
            Networking.Networking.ExternalIp = ExternalIp;
            options.Add("externalIp", ExternalIp);
        }

        if (Sentry)
        {
            Config.Sentry = Sentry;

            if (!string.IsNullOrEmpty(SentryDsn))
            {
                Config.SentryDsn = SentryDsn;
            }
            else
            {
                Logger.App("Sentry DSN is not set. Sentry will not be enabled.");
                Config.Sentry = false;
            }

            Logger.App("Sentry is enabled.");
        }

        UserSettings.ApplySettings(options, silent: true);
    }
}