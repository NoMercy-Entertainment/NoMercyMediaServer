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

// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.MediaProcessing.People;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Models.People;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class PersonExtrasJob : AbstractShowExtraDataJob<TmdbPersonAppends, string>
{
    public override string QueueName => "extras";
    public override int Priority => 1;

    /** Note: TmdbPersonAppends is a reduced set to improve performance. */
    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        PersonRepository personRepository = new(context);
        PersonManager personManager = new(
            personRepository,
            jobDispatcher,
            LoggerFactory.CreateLogger<PersonManager>()
        );

        foreach (TmdbPersonAppends person in Storage)
        {
            await personManager.StoreTranslations(person);
            await personManager.StoreImages(person);
        }

        Logger.MovieDb($"Show {Name}: People: Translations and Images stored", LogEventLevel.Debug);
    }
}
