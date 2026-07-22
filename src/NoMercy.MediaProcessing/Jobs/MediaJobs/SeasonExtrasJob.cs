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
using NoMercy.Providers.TMDB.Models.Season;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class SeasonExtrasJob : AbstractShowExtraDataJob<TmdbSeasonAppends, string>
{
    public SeasonExtrasJob() { }

    public SeasonExtrasJob(ILoggerFactory loggerFactory)
        : base(loggerFactory: loggerFactory) { }

    public override string QueueName => "extras";
    public override int Priority => 1;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        SeasonRepository seasonRepository = new(context: context);
        SeasonManager seasonManager = new(
            seasonRepository: seasonRepository,
            jobDispatcher: jobDispatcher,
            logger: LoggerFactory.CreateLogger<SeasonManager>()
        );

        PersonRepository personRepository = new(
            context: context,
            logger: LoggerFactory.CreateLogger<PersonRepository>()
        );
        PersonManager personManager = new(
            personRepository: personRepository,
            jobDispatcher: jobDispatcher,
            logger: LoggerFactory.CreateLogger<PersonManager>()
        );

        foreach (TmdbSeasonAppends season in Storage)
        {
            // Bounded so a stalled TMDB/NFS call fails this season's pass
            // instead of hanging the whole job — see
            // JobOperationTimeoutExtensions.
            await personManager.Store(season: season).WithTimeout(operationName: nameof(PersonManager.Store));
            await seasonManager
                .StoreImages(showName: Name, season: season)
                .WithTimeout(operationName: nameof(SeasonManager.StoreImages));
            await seasonManager
                .StoreTranslations(showName: Name, season: season)
                .WithTimeout(operationName: nameof(SeasonManager.StoreTranslations));
        }

        Log.LogTrace(message: "Show {Name}: Seasons: Images and Translations stored", args: Name);
    }
}
