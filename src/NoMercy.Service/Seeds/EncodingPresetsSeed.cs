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

using NoMercy.Database;
using NoMercy.Encoder.Profiles;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;
using Serilog.Events;

namespace NoMercy.Service.Seeds;

/// <summary>
/// Seeds the built-in preset library. Built-ins always seed on every startup;
/// <see cref="BuiltinPresetSeeder"/> handles stale-builtin pruning internally.
/// No example/starter presets are seeded — a curated built-in set is the whole
/// library, and re-seeding editable "Example:" shells only re-created bloat a
/// user had deleted.
/// </summary>
public static class EncodingPresetsSeed
{
    public static async Task Init(MediaContext context, IStorage storage)
    {
        Logger.Setup(message: "Adding Encoding Presets", level: LogEventLevel.Verbose);

        try
        {
            await new BuiltinPresetSeeder(context: context).SeedAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(message: e.Message, level: LogEventLevel.Fatal);
        }
    }
}
