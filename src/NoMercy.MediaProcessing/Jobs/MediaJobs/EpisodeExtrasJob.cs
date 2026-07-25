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
using NoMercy.MediaProcessing.Episodes;
using NoMercy.MediaProcessing.People;
using NoMercy.Providers.TMDB.Models.Episode;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class EpisodeExtrasJob : AbstractShowExtraDataJob<TmdbEpisodeAppends, string>
{
    public EpisodeExtrasJob() { }

    public EpisodeExtrasJob(ILoggerFactory loggerFactory)
        : base(loggerFactory) { }

    public override string QueueName => "extras";
    public override int Priority => 1;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        EpisodeRepository episodeRepository = new(context);
        EpisodeManager episodeManager = new(
            episodeRepository,
            jobDispatcher,
            LoggerFactory.CreateLogger<EpisodeManager>()
        );

        PersonRepository personRepository = new(
            context,
            LoggerFactory.CreateLogger<PersonRepository>()
        );
        PersonManager personManager = new(
            personRepository,
            jobDispatcher,
            LoggerFactory.CreateLogger<PersonManager>()
        );

        foreach (TmdbEpisodeAppends episode in Storage)
        {
            // Bounded so a stalled TMDB/NFS call fails this episode's pass
            // instead of hanging the whole job — see
            // JobOperationTimeoutExtensions.
            await personManager.Store(episode).WithTimeout(nameof(PersonManager.Store));
            await episodeManager
                .StoreTranslations(Name, episode)
                .WithTimeout(nameof(EpisodeManager.StoreTranslations));
            await episodeManager
                .StoreImages(Name, episode)
                .WithTimeout(nameof(EpisodeManager.StoreImages));
        }

        Log.LogDebug(
            "Show {Name}: Season {SeasonNumber} Episodes: Images and Translations stored", [Name, Storage.FirstOrDefault()?.SeasonNumber]
        );
    }
}
