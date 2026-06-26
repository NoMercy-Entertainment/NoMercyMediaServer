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
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;
using ConfigurationModel = NoMercy.Database.Models.Common.Configuration;

namespace NoMercy.Service.Seeds;

public static class ConfigSeed
{
    public static async Task Init(this AppDbContext dbContext)
    {
        Logger.Setup("Adding Configurations", LogEventLevel.Verbose);
        ConfigurationModel[] configs =
        [
            new() { Key = "internalPort", Value = Config.InternalServerPort.ToString() },
            new() { Key = "externalPort", Value = Config.ExternalServerPort.ToString() },
        ];

        try
        {
            await dbContext
                .Configuration.UpsertRange(configs)
                .On(v => new { v.Key })
                .WhenMatched((_, vi) => new() { Key = vi.Key, Value = vi.Value })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }
    }
}
