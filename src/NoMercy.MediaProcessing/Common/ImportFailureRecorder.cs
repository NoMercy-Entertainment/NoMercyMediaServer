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
using NoMercy.Database.Models.Libraries;

namespace NoMercy.MediaProcessing.Common;

/// Persists a dead-letter ImportFailure record when an import job gives up.
/// Idempotent per (JobType, FilePath): re-failing the same item bumps the
/// retry counter instead of piling up duplicate rows.
public static class ImportFailureRecorder
{
    public static async Task RecordAsync(
        MediaContext context,
        string jobType,
        string filePath,
        Ulid? libraryId,
        string errorMessage
    )
    {
        ImportFailure? existing = await context.ImportFailures.FirstOrDefaultAsync(predicate: f =>
            f.JobType == jobType && f.FilePath == filePath && !f.Resolved
        );

        if (existing is not null)
        {
            existing.RetryCount += 1;
            existing.LastAttemptAt = DateTimeOffset.UtcNow;
            existing.ErrorMessage = errorMessage;
            existing.LibraryId = libraryId;
        }
        else
        {
            context.ImportFailures.Add(
                entity: new()
                {
                    JobType = jobType,
                    FilePath = filePath,
                    LibraryId = libraryId,
                    ErrorMessage = errorMessage,
                    LastAttemptAt = DateTimeOffset.UtcNow,
                }
            );
        }

        await context.SaveChangesAsync();
    }
}
