using CommandLine;
using NoMercy.Database;
using ConfigurationModel = NoMercy.Database.Models.Common.Configuration;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Service.Seeds;
using NoMercy.Setup;
using Serilog.Events;

namespace NoMercy.Service;

public class StartupOptions
{
    public static string? OverrideInternalIp { get; private set; }
    public static string? OverrideExternalIp { get; private set; }

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

    /// <summary>
    /// Apply environment variable overrides for options not set via CLI.
    /// Environment variables use NOMERCY_ prefix:
    ///   NOMERCY_DEV=true, NOMERCY_LOG_LEVEL=Debug,
    ///   NOMERCY_INTERNAL_PORT=7626, NOMERCY_EXTERNAL_PORT=7626,
    ///   NOMERCY_INTERNAL_IP=192.168.1.100, NOMERCY_EXTERNAL_IP=1.2.3.4,
    ///   NOMERCY_PIPE_NAME=MyPipe, NOMERCY_SEED=true
    /// </summary>
    private void ApplyEnvironmentVariables()
    {
        if (!Development)
            Development = GetEnvBool("NOMERCY_DEV");

        if (LogLevel == nameof(LogEventLevel.Information))
        {
            string? envLogLevel = Environment.GetEnvironmentVariable("NOMERCY_LOG_LEVEL");
            if (!string.IsNullOrEmpty(envLogLevel))
                LogLevel = envLogLevel.Trim();
        }

        if (!ShouldSeed)
            ShouldSeed = GetEnvBool("NOMERCY_SEED");

        if (InternalPort == 0)
        {
            string? envPort = Environment.GetEnvironmentVariable("NOMERCY_INTERNAL_PORT");
            if (!string.IsNullOrEmpty(envPort) && int.TryParse(envPort, out int port))
                InternalPort = port;
        }

        if (ExternalPort == 0)
        {
            string? envPort = Environment.GetEnvironmentVariable("NOMERCY_EXTERNAL_PORT");
            if (!string.IsNullOrEmpty(envPort) && int.TryParse(envPort, out int port))
                ExternalPort = port;
        }

        if (string.IsNullOrEmpty(InternalIp))
            InternalIp = Environment.GetEnvironmentVariable("NOMERCY_INTERNAL_IP");

        if (string.IsNullOrEmpty(ExternalIp))
            ExternalIp = Environment.GetEnvironmentVariable("NOMERCY_EXTERNAL_IP");

        if (string.IsNullOrEmpty(PipeName))
            PipeName = Environment.GetEnvironmentVariable("NOMERCY_PIPE_NAME");

    }

    private static bool GetEnvBool(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return value is "1" or "true" or "True" or "TRUE";
    }

    public void ApplySettings()
    {
        ApplyEnvironmentVariables();

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
                Logger.App("Database not yet initialized, using default internal port.", LogEventLevel.Debug);
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
                Logger.App("Database not yet initialized, using default external port.", LogEventLevel.Debug);
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
            OverrideInternalIp = InternalIp;
            options.Add("internalIp", InternalIp);
        }

        if (!string.IsNullOrEmpty(ExternalIp))
        {
            Logger.App("Setting external ip to " + ExternalIp);
            OverrideExternalIp = ExternalIp;
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
