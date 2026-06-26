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

using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.NmSystem.Configuration;
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
                    RuntimeServerSettings.Current.LibraryWorkers = new(
                        RuntimeServerSettings.Current.LibraryWorkers.Key,
                        setting.Value.ToInt()
                    );
                    break;
                case "importRunners" or "queueRunners":
                    RuntimeServerSettings.Current.ImportWorkers = new(
                        RuntimeServerSettings.Current.ImportWorkers.Key,
                        setting.Value.ToInt()
                    );
                    break;
                case "extrasRunners" or "dataRunners":
                    RuntimeServerSettings.Current.ExtrasWorkers = new(
                        RuntimeServerSettings.Current.ExtrasWorkers.Key,
                        setting.Value.ToInt()
                    );
                    break;
                case "encoderRunners":
                    RuntimeServerSettings.Current.EncoderWorkers = new(
                        RuntimeServerSettings.Current.EncoderWorkers.Key,
                        setting.Value.ToInt()
                    );
                    Logger.App(
                        $"UserSettings: RuntimeServerSettings.Current.EncoderWorkers loaded as {RuntimeServerSettings.Current.EncoderWorkers.Value} (DB value '{setting.Value}')"
                    );
                    break;
                case "cronRunners":
                    RuntimeServerSettings.Current.CronWorkers = new(
                        RuntimeServerSettings.Current.CronWorkers.Key,
                        setting.Value.ToInt()
                    );
                    break;
                case "imageRunners":
                    RuntimeServerSettings.Current.ImageWorkers = new(
                        RuntimeServerSettings.Current.ImageWorkers.Key,
                        setting.Value.ToInt()
                    );
                    break;
                case "fileRunners":
                    RuntimeServerSettings.Current.FileWorkers = new(
                        RuntimeServerSettings.Current.FileWorkers.Key,
                        setting.Value.ToInt()
                    );
                    break;
                case "musicRunners":
                    RuntimeServerSettings.Current.MusicWorkers = new(
                        RuntimeServerSettings.Current.MusicWorkers.Key,
                        setting.Value.ToInt()
                    );
                    break;
                case "swagger":
                    Config.Swagger = setting.Value.ToBoolean();
                    break;
                case "allowAdultContent":
                    Config.AllowAdultContent = setting.Value.ToBoolean();
                    break;
            }
        }
    }
}
