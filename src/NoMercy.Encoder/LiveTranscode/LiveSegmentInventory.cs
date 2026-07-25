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
            entries = storage.List(scratchDirectory, SegmentGlob, recursive: false);
        }
        catch (Exception ex)
        {
            // First-ever spawn: the scratch directory does not exist yet. Treated
            // the same as "nothing on disk" rather than surfaced — a missing
            // directory is the expected state before the first runner ever writes
            // to it, not an error.
            logger.LogDebug(ex, "Could not list {Dir} for segment inventory", scratchDirectory);
            return indices;
        }

        foreach (StorageEntry entry in entries)
        {
            if (entry.IsDirectory)
                continue;

            int? index = ParseIndex(storage.GetName(entry.Path));
            if (index is int value)
                indices.Add(value);
        }

        return indices;
    }

    public void Purge(string scratchDirectory)
    {
        IReadOnlyList<StorageEntry> entries;
        try
        {
            entries = storage.List(scratchDirectory, SegmentGlob, recursive: false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not list {Dir} for segment purge", scratchDirectory);
            return;
        }

        foreach (StorageEntry entry in entries)
        {
            if (entry.IsDirectory)
                continue;

            try
            {
                storage.Delete(entry.Path);
            }
            catch (Exception ex)
            {
                // Windows can refuse to delete a segment an in-flight HTTP
                // response still holds open. Best-effort: the file is picked up
                // by the next purge, or by scratch-directory teardown on session
                // end.
                logger.LogDebug(ex, "Could not delete segment {Path}", entry.Path);
            }
        }
    }

    private static int? ParseIndex(string fileName)
    {
        if (!fileName.StartsWith(SegmentPrefix, StringComparison.Ordinal))
            return null;

        int dot = fileName.IndexOf('.', SegmentPrefix.Length);
        if (dot < 0)
            return null;

        string digits = fileName[SegmentPrefix.Length..dot];
        return int.TryParse(digits, out int value) ? value : null;
    }
}
