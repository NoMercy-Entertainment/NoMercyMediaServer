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
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;
using ConfigurationModel = NoMercy.Database.Models.Common.Configuration;

namespace NoMercy.Service.Seeds;

public static class ConfigSeed
{
    public static async Task Init(this AppDbContext dbContext)
    {
        Logger.Setup(message: "Adding Configurations", level: LogEventLevel.Verbose);
        ConfigurationModel[] configs =
        [
            new()
            {
                Key = "internalPort",
                Value = RuntimeServerSettings.Current.InternalServerPort.ToString(),
            },
            new()
            {
                Key = "externalPort",
                Value = RuntimeServerSettings.Current.ExternalServerPort.ToString(),
            },
        ];

        try
        {
            await dbContext
                .Configuration.UpsertRange(entities: configs)
                .On(match: v => new { v.Key })
                .WhenMatched(updater: (_, vi) => new() { Key = vi.Key, Value = vi.Value })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(message: e.Message, level: LogEventLevel.Fatal);
        }
    }
}
