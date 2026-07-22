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

namespace NoMercy.Encoder.LiveTranscode;

/// <summary>
/// Where a (re)spawned runner should start, and where it must stop. <c>StopAt</c>
/// null means "run to EOF" — there is no already-covered segment ahead of
/// <c>Start</c> within the known range.
/// </summary>
public record LiveGapPlan(TimeSpan Start, TimeSpan? StopAt);

/// <summary>
/// Pure planning for every runner (re)spawn — session start, seek, resume,
/// quality change, and the NVENC-fallback respawn all route through this. Given
/// what is already on disk, decides the first UNCOVERED segment at-or-after the
/// desired position (so a spawn never re-encodes ground already produced by an
/// earlier runner generation) and the next COVERED segment after it (so the new
/// runner stops instead of encoding straight through to EOF over content that is
/// already servable). Segments are absolutely indexed and deterministic per
/// quality, so any earlier generation's output is valid for any later one.
/// </summary>
public static class LiveGapPlanner
{
    public static LiveGapPlan? Plan(
        IReadOnlySet<int> existing,
        int desiredIndex,
        int segmentDurationSeconds,
        int? lastIndex
    )
    {
        int segmentDuration = segmentDurationSeconds > 0 ? segmentDurationSeconds : 6;
        int desired = Math.Max(val1: 0, val2: desiredIndex);

        if (lastIndex is int fileEnd && desired > fileEnd)
            return null;

        // Nothing can be "covered" past the highest segment ever produced, so
        // when the file's total segment count is unknown (lastIndex null) the
        // scan never needs to look beyond it — there is nothing further on disk
        // to find.
        int maxExisting = existing.Count > 0 ? existing.Max() : -1;
        int scanBound = lastIndex ?? maxExisting;

        int firstMissing = desired;
        while (firstMissing <= scanBound && existing.Contains(item: firstMissing))
            firstMissing++;

        if (lastIndex is int coveredThrough && firstMissing > coveredThrough)
            return null;

        int? stopAtIndex = null;
        for (int candidate = firstMissing + 1; candidate <= scanBound; candidate++)
        {
            if (!existing.Contains(item: candidate))
                continue;

            stopAtIndex = candidate;
            break;
        }

        TimeSpan start = TimeSpan.FromSeconds(value: (double)firstMissing * segmentDuration);
        TimeSpan? stopAt = stopAtIndex is int stop
            ? TimeSpan.FromSeconds(value: (double)stop * segmentDuration)
            : null;

        return new LiveGapPlan(Start: start, StopAt: stopAt);
    }
}
