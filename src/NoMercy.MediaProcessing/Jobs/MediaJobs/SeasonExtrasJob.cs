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
using NoMercy.MediaProcessing.Seasons;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Models.Season;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class SeasonExtrasJob : AbstractShowExtraDataJob<TmdbSeasonAppends, string>
{
    public SeasonExtrasJob() { }

    public SeasonExtrasJob(
        ILoggerFactory loggerFactory
    )
        : base(loggerFactory) { }

    public override string QueueName => "extras";
    public override int Priority => 1;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        SeasonRepository seasonRepository = new(context);
        SeasonManager seasonManager = new(
            seasonRepository,
            jobDispatcher,
            LoggerFactory.CreateLogger<SeasonManager>()
        );

        PersonRepository personRepository = new(context, LoggerFactory.CreateLogger<PersonRepository>());
        PersonManager personManager = new(
            personRepository,
            jobDispatcher,
            LoggerFactory.CreateLogger<PersonManager>()
        );

        foreach (TmdbSeasonAppends season in Storage)
        {
            await personManager.Store(season);

            await seasonManager.StoreImages(Name, season);
            await seasonManager.StoreTranslations(Name, season);
        }

        Log.LogTrace("Show {Name}: Seasons: Images and Translations stored", Name);
    }
}
