// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using CommandLine;
using NoMercy.Database;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Service.Seeds;
using NoMercy.Setup.Ui;
using Serilog.Events;
using ConfigurationModel = NoMercy.Database.Models.Common.Configuration;

namespace NoMercy.Service;

public class StartupOptions
{
    public static string? OverrideInternalIp { get; private set; }
    public static string? OverrideExternalIp { get; private set; }

    // dev
    [Option(shortName: 'd', longName: "dev", Required = false, HelpText = "Run the server in development mode.")]
    public bool Development { get; set; }

    [Option(shortName: 'l', longName: "loglevel", Required = false, HelpText = "Run the server in development mode.")]
    public string LogLevel { get; set; } = nameof(LogEventLevel.Information);

    [Option(longName: "seed", Required = false, HelpText = "Run the server in development mode.")]
    public bool ShouldSeed { get; set; }

    [Option(
        shortName: 'i',
        longName: "internal-port",
        Required = false,
        HelpText = "Internal port to use for the server."
    )]
    public int InternalPort { get; set; }

    [Option(
        shortName: 'x',
        longName: "external-port",
        Required = false,
        HelpText = "External port to use for the server."
    )]
    public int ExternalPort { get; set; }

    [Option(longName: "internal-ip", Required = false, HelpText = "Internal ip to use for the server.")]
    public string? InternalIp { get; set; }

    [Option(longName: "external-ip", Required = false, HelpText = "External ip to use for the server.")]
    public string? ExternalIp { get; set; }

    [Option(
        longName: "pipe-name",
        Required = false,
        HelpText = "Named pipe name for IPC (Windows) or Unix socket filename."
    )]
    public string? PipeName { get; set; }

    [Option(
        longName: "service",
        Required = false,
        HelpText = "Run as a platform service (Windows SCM, Linux systemd, macOS launchd)."
    )]
    public bool RunAsService { get; set; }

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
            Development = GetEnvBool(name: "NOMERCY_DEV");

        if (LogLevel == nameof(LogEventLevel.Information))
        {
            string? envLogLevel = Environment.GetEnvironmentVariable(variable: "NOMERCY_LOG_LEVEL");
            if (!string.IsNullOrEmpty(value: envLogLevel))
                LogLevel = envLogLevel.Trim();
        }

        if (!ShouldSeed)
            ShouldSeed = GetEnvBool(name: "NOMERCY_SEED");

        if (InternalPort == 0)
        {
            string? envPort = Environment.GetEnvironmentVariable(variable: "NOMERCY_INTERNAL_PORT");
            if (!string.IsNullOrEmpty(value: envPort) && int.TryParse(s: envPort, result: out int port))
                InternalPort = port;
        }

        if (ExternalPort == 0)
        {
            string? envPort = Environment.GetEnvironmentVariable(variable: "NOMERCY_EXTERNAL_PORT");
            if (!string.IsNullOrEmpty(value: envPort) && int.TryParse(s: envPort, result: out int port))
                ExternalPort = port;
        }

        if (string.IsNullOrEmpty(value: InternalIp))
            InternalIp = Environment.GetEnvironmentVariable(variable: "NOMERCY_INTERNAL_IP");

        if (string.IsNullOrEmpty(value: ExternalIp))
            ExternalIp = Environment.GetEnvironmentVariable(variable: "NOMERCY_EXTERNAL_IP");

        if (string.IsNullOrEmpty(value: PipeName))
            PipeName = Environment.GetEnvironmentVariable(variable: "NOMERCY_PIPE_NAME");
    }

    private static bool GetEnvBool(string name)
    {
        string? value = Environment.GetEnvironmentVariable(variable: name);
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

            ExternalServicesConfig.Current.AppBaseUrl = "https://app-dev.nomercy.tv/";
            ExternalServicesConfig.Current.ApiBaseUrl = "https://api-dev.nomercy.tv/";
            ExternalServicesConfig.Current.ApiServerBaseUrl =
                $"{ExternalServicesConfig.Current.ApiBaseUrl}v1/server/";

            ExternalServicesConfig.Current.AuthBaseUrl =
                "https://auth-dev.nomercy.tv/realms/NoMercyTV/";

            Logger.App(message: "Running in development mode.");
        }

        if (ShouldSeed)
            Logger.App(message: "Seeding database.");

        if (!string.IsNullOrEmpty(value: LogLevel))
        {
            if (TryParseLogLevel(raw: LogLevel, level: out LogEventLevel level))
            {
                Logger.App(message: $"Setting log level to: {LogLevel}.");
                Logger.SetLogLevel(level: level);
                options.Add(key: "loglevel", value: LogLevel);
            }
            else
            {
                Logger.App(
                    message: $"Unknown log level '{LogLevel}', falling back to Information.",
                    level: LogEventLevel.Warning
                );
                Logger.SetLogLevel(level: LogEventLevel.Information);
            }
        }

        InternalPort = ResolvePort(
            cliPort: InternalPort,
            configKey: "internalPort",
            label: "internal",
            setConfigPort: port => RuntimeServerSettings.Current.InternalServerPort = port,
            options: options
        );
        ExternalPort = ResolvePort(
            cliPort: ExternalPort,
            configKey: "externalPort",
            label: "external",
            setConfigPort: port => RuntimeServerSettings.Current.ExternalServerPort = port,
            options: options
        );

        if (!string.IsNullOrEmpty(value: PipeName))
        {
            Logger.App(message: "Setting IPC pipe name to " + PipeName);
            Config.ManagementPipeName = PipeName;
        }

        if (!string.IsNullOrEmpty(value: InternalIp))
        {
            Logger.App(message: "Setting internal ip to " + InternalIp);
            OverrideInternalIp = InternalIp;
            options.Add(key: "internalIp", value: InternalIp);
        }

        if (!string.IsNullOrEmpty(value: ExternalIp))
        {
            Logger.App(message: "Setting external ip to " + ExternalIp);
            OverrideExternalIp = ExternalIp;
            options.Add(key: "externalIp", value: ExternalIp);
        }

        UserSettings.ApplySettings(settings: options, silent: true);
    }

    private static int ResolvePort(
        int cliPort,
        string configKey,
        string label,
        Action<int> setConfigPort,
        Dictionary<string, string> options
    )
    {
        if (cliPort != 0)
        {
            Logger.App(message: $"Setting {label} port to " + cliPort);
            setConfigPort(obj: cliPort);
            options.Add(key: configKey, value: cliPort.ToString());
            return cliPort;
        }

        string? dbValue = null;
        try
        {
            AppDbContext appContext = new();
            ConfigurationModel? portConfig = appContext.Configuration.FirstOrDefault(predicate: c =>
                c.Key == configKey
            );
            dbValue = portConfig?.Value;
            appContext.Dispose();
        }
        catch (Exception)
        {
            Logger.App(
                message: $"Database not yet initialized, using default {label} port.",
                level: LogEventLevel.Debug
            );
        }

        bool hasValue = !string.IsNullOrEmpty(value: dbValue);
        bool parsed = hasValue && int.TryParse(s: dbValue, result: out int _);
        int resolved = ResolvePortFrom(cliPort: cliPort, dbValue: dbValue, fallback: 7626);

        if (parsed)
            Logger.App(message: $"Loaded {label} port from database: " + resolved);
        else if (hasValue)
            Logger.App(
                message: $"Configured {label} port '{dbValue}' is not a valid number; using default {resolved}.",
                level: LogEventLevel.Warning
            );

        setConfigPort(obj: resolved);
        options.Add(key: configKey, value: resolved.ToString());
        return resolved;
    }

    /// <summary>
    /// Resolves the effective port from layered sources: an explicit CLI/env port
    /// wins; otherwise a valid numeric database value; otherwise
    /// <paramref name="fallback"/>. A present-but-unparseable database value never
    /// throws — it falls back, so a corrupt configuration row cannot crash startup.
    /// </summary>
    public static int ResolvePortFrom(int cliPort, string? dbValue, int fallback)
    {
        if (cliPort != 0)
            return cliPort;

        return !string.IsNullOrEmpty(value: dbValue) && int.TryParse(s: dbValue, result: out int port)
            ? port
            : fallback;
    }

    /// <summary>
    /// Parses a Serilog log level from user input (CLI or NOMERCY_LOG_LEVEL),
    /// case-insensitively. Returns false for null/empty/unknown values instead of
    /// throwing, so a typo degrades to a warning rather than crashing startup.
    /// </summary>
    public static bool TryParseLogLevel(string? raw, out LogEventLevel level)
    {
        if (
            !string.IsNullOrWhiteSpace(value: raw)
            && Enum.TryParse(value: raw, ignoreCase: true, result: out level)
            && Enum.IsDefined(value: level)
        )
            return true;

        level = LogEventLevel.Information;
        return false;
    }
}
