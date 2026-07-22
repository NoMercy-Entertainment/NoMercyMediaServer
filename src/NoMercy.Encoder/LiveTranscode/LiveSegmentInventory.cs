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

using Microsoft.Extensions.Logging;
using NoMercy.Storage;

namespace NoMercy.Encoder.LiveTranscode;

public class LiveSegmentInventory(IStorage storage, ILogger<LiveSegmentInventory> logger)
    : ILiveSegmentInventory
{
    private const string SegmentGlob = "seg_*.ts";
    private const string SegmentPrefix = "seg_";

    public IReadOnlySet<int> Snapshot(string scratchDirectory)
    {
        HashSet<int> indices = [];

        IReadOnlyList<StorageEntry> entries;
        try
        {
            entries = storage.List(path: scratchDirectory, pattern: SegmentGlob, recursive: false);
        }
        catch (Exception ex)
        {
            // First-ever spawn: the scratch directory does not exist yet. Treated
            // the same as "nothing on disk" rather than surfaced — a missing
            // directory is the expected state before the first runner ever writes
            // to it, not an error.
            logger.LogDebug(exception: ex, message: "Could not list {Dir} for segment inventory", args: scratchDirectory);
            return indices;
        }

        foreach (StorageEntry entry in entries)
        {
            if (entry.IsDirectory)
                continue;

            int? index = ParseIndex(fileName: storage.GetName(path: entry.Path));
            if (index is int value)
                indices.Add(item: value);
        }

        return indices;
    }

    public void Purge(string scratchDirectory)
    {
        IReadOnlyList<StorageEntry> entries;
        try
        {
            entries = storage.List(path: scratchDirectory, pattern: SegmentGlob, recursive: false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(exception: ex, message: "Could not list {Dir} for segment purge", args: scratchDirectory);
            return;
        }

        foreach (StorageEntry entry in entries)
        {
            if (entry.IsDirectory)
                continue;

            try
            {
                storage.Delete(path: entry.Path);
            }
            catch (Exception ex)
            {
                // Windows can refuse to delete a segment an in-flight HTTP
                // response still holds open. Best-effort: the file is picked up
                // by the next purge, or by scratch-directory teardown on session
                // end.
                logger.LogDebug(exception: ex, message: "Could not delete segment {Path}", args: entry.Path);
            }
        }
    }

    private static int? ParseIndex(string fileName)
    {
        if (!fileName.StartsWith(value: SegmentPrefix, comparisonType: StringComparison.Ordinal))
            return null;

        int dot = fileName.IndexOf(value: '.', startIndex: SegmentPrefix.Length);
        if (dot < 0)
            return null;

        string digits = fileName[SegmentPrefix.Length..dot];
        return int.TryParse(s: digits, result: out int value) ? value : null;
    }
}
