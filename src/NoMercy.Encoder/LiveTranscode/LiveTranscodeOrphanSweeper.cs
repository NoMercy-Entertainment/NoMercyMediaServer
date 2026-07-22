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

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Storage;

namespace NoMercy.Encoder.LiveTranscode;

/// <summary>
/// One-shot hosted service that runs once at startup and deletes any
/// <c>lts-*</c> folders left behind under
/// <see cref="EncoderOptions.ResolvedLiveTranscodeCachePath"/> by a previous
/// server crash or unclean shutdown.
/// </summary>
public class LiveTranscodeOrphanSweeper(
    EncoderOptions options,
    ILogger<LiveTranscodeOrphanSweeper> logger,
    IStorage storage
) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        string cacheRoot = options.ResolvedLiveTranscodeCachePath;

        if (!storage.Exists(path: cacheRoot))
        {
            logger.LogDebug(
                message: "LiveTranscodeOrphanSweeper: cache root {Dir} does not exist, nothing to sweep",
                args: cacheRoot
            );
            return Task.CompletedTask;
        }

        IReadOnlyList<StorageEntry> orphans;
        try
        {
            orphans = storage
                .List(path: cacheRoot, pattern: "lts-*", recursive: false)
                .Where(predicate: e => e.IsDirectory)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                exception: ex,
                message: "LiveTranscodeOrphanSweeper: could not enumerate {Dir}",
                args: cacheRoot
            );
            return Task.CompletedTask;
        }

        foreach (StorageEntry entry in orphans)
        {
            try
            {
                storage.DeleteDirectory(path: entry.Path, recursive: true);
                logger.LogInformation(
                    message: "LiveTranscodeOrphanSweeper: deleted orphan {Dir}",
                    args: entry.Path
                );
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    exception: ex,
                    message: "LiveTranscodeOrphanSweeper: could not delete orphan {Dir}",
                    args: entry.Path
                );
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
