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
using NoMercy.MediaProcessing.Shows;
using NoMercyQueue;

namespace NoMercy.Service.Workers;

/// <summary>
/// On boot, dispatches a single <see cref="AnimeEnrichmentBackfillJob"/> onto
/// the low-priority "extras" queue if the one-shot anime enrichment backfill
/// (libraries imported before AniList/Jikan enrichment shipped) has not yet
/// completed. Mirrors <see cref="PaletteBackfillStartupService"/>'s pattern:
/// the job drains in small cursor-tracked batches and re-enqueues itself, so
/// a restart resumes where the last batch left off instead of redoing the
/// whole library.
/// </summary>
public class AnimeEnrichmentBackfillStartupService(
    ILogger<AnimeEnrichmentBackfillStartupService> logger
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using AppDbContext appDb = new();
            bool complete = await AnimeEnrichmentBackfillState.IsCompleteAsync(
                appDb,
                cancellationToken
            );
            if (complete)
            {
                logger.LogDebug("Anime enrichment backfill already complete — skipping dispatch");
                return;
            }

            QueueRunner.Current?.Dispatcher.Dispatch(new AnimeEnrichmentBackfillJob(), "extras", 0);
            logger.LogInformation("Anime enrichment backfill job dispatched on startup");
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to dispatch anime enrichment backfill on startup; continuing"
            );
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
