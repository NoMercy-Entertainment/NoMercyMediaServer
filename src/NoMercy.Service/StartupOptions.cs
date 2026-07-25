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
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Ui;
using Serilog.Events;
using ConfigurationModel = NoMercy.Database.Models.Common.Configuration;

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

    [Option(
        'i',
        "internal-port",
        Required = false,
        HelpText = "Internal port to use for the server."
    )]
    public int InternalPort { get; set; }

    [Option(
        'x',
        "external-port",
        Required = false,
        HelpText = "External port to use for the server."
    )]
    public int ExternalPort { get; set; }

    [Option("internal-ip", Required = false, HelpText = "Internal ip to use for the server.")]
    public string? InternalIp { get; set; }

    [Option("external-ip", Required = false, HelpText = "External ip to use for the server.")]
    public string? ExternalIp { get; set; }

    [Option(
        "pipe-name",
        Required = false,
        HelpText = "Named pipe name for IPC (Windows) or Unix socket filename."
    )]
    public string? PipeName { get; set; }

    [Option(
        "service",
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
            Development = GetEnvBool("NOMERCY_DEV");

        if (LogLevel == nameof(LogEventLevel.Information))
        {
            string? envLogLevel = Environment.GetEnvironmentVariable("NOMERCY_LOG_LEVEL");
            if (!string.IsNullOrEmpty(envLogLevel))
                LogLevel = envLogLevel.Trim();
        }

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

        if (Development)
        {
            Config.IsDev = true;

            ExternalServicesConfig.Current.AppBaseUrl = "https://app-dev.nomercy.tv/";
            ExternalServicesConfig.Current.ApiBaseUrl = "https://api-dev.nomercy.tv/";
            ExternalServicesConfig.Current.ApiServerBaseUrl =
                $"{ExternalServicesConfig.Current.ApiBaseUrl}v1/server/";

            ExternalServicesConfig.Current.AuthBaseUrl =
                "https://auth-dev.nomercy.tv/realms/NoMercyTV/";

            Logger.App("Running in development mode.");
        }

        if (!string.IsNullOrEmpty(LogLevel))
        {
            if (TryParseLogLevel(LogLevel, out LogEventLevel level))
            {
                Logger.App($"Setting log level to: {LogLevel}.");
                Logger.SetLogLevel(level);
                options.Add("loglevel", LogLevel);
            }
            else
            {
                Logger.App(
                    $"Unknown log level '{LogLevel}', falling back to Information.",
                    LogEventLevel.Warning
                );
                Logger.SetLogLevel(LogEventLevel.Information);
            }
        }

        InternalPort = ResolvePort(
            InternalPort,
            "internalPort",
            "internal",
            port => RuntimeServerSettings.Current.InternalServerPort = port,
            options
        );
        ExternalPort = ResolvePort(
            ExternalPort,
            "externalPort",
            "external",
            port => RuntimeServerSettings.Current.ExternalServerPort = port,
            options
        );

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

        UserSettings.ApplySettings(options, true);
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
            Logger.App($"Setting {label} port to " + cliPort);
            setConfigPort(cliPort);
            options.Add(configKey, cliPort.ToString());
            return cliPort;
        }

        string? dbValue = null;
        try
        {
            AppDbContext appContext = new();
            ConfigurationModel? portConfig = appContext.Configuration.FirstOrDefault(c =>
                c.Key == configKey
            );
            dbValue = portConfig?.Value;
            appContext.Dispose();
        }
        catch (Exception)
        {
            Logger.App(
                $"Database not yet initialized, using default {label} port.",
                LogEventLevel.Debug
            );
        }

        bool hasValue = !string.IsNullOrEmpty(dbValue);
        bool parsed = hasValue && int.TryParse(dbValue, out int _);
        int resolved = ResolvePortFrom(cliPort, dbValue, 7626);

        if (parsed)
            Logger.App($"Loaded {label} port from database: " + resolved);
        else if (hasValue)
            Logger.App(
                $"Configured {label} port '{dbValue}' is not a valid number; using default {resolved}.",
                LogEventLevel.Warning
            );

        setConfigPort(resolved);
        options.Add(configKey, resolved.ToString());
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

        return !string.IsNullOrEmpty(dbValue) && int.TryParse(dbValue, out int port)
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
            !string.IsNullOrWhiteSpace(raw)
            && Enum.TryParse(raw, true, out level)
            && Enum.IsDefined(level)
        )
            return true;

        level = LogEventLevel.Information;
        return false;
    }
}
