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
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Common;

/// Wraps a single external-metadata fetch in retry-with-exponential-backoff.
/// Only transient transport failures (HttpRequestException) are retried; a
/// fetch that legitimately resolves to null (e.g. TMDB 404) is returned as-is
/// without wasting backoff cycles. After the retries are exhausted the call
/// returns null so the caller can route the item to the import-failure
/// dead-letter queue rather than throwing.
public static class MetadataRetry
{
    public static async Task<T?> FetchAsync<T>(
        Func<Task<T?>> fetch,
        string description,
        int maxRetries = 3
    )
        where T : class
    {
        for (int attempt = 1; attempt <= maxRetries + 1; attempt++)
        {
            try
            {
                return await fetch();
            }
            catch (HttpRequestException ex)
            {
                if (attempt > maxRetries)
                {
                    Logger.App(
                        message: $"Metadata fetch '{description}' failed after {maxRetries} retries: {ex.Message}",
                        level: LogEventLevel.Error
                    );
                    return null;
                }

                TimeSpan delay = TimeSpan.FromSeconds(value: Math.Pow(x: 2, y: attempt));
                Logger.App(
                    message: $"Metadata fetch '{description}' attempt {attempt} failed ({ex.Message}); retrying in {delay.TotalSeconds:0}s",
                    level: LogEventLevel.Warning
                );
                await Task.Delay(delay: delay);
            }
        }

        return null;
    }
}
