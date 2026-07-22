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

using System.Text.RegularExpressions;

namespace NoMercy.MediaProcessing.Reclaim;

public static partial class ReclaimClassifier
{
    private static readonly string[] OriginalExtensions =
    [
        ".mp4",
        ".mkv",
        ".m4v",
        ".avi",
        ".webm",
        ".mov",
    ];

    public static ReclaimClassification Classify(
        IReadOnlyList<FolderEntry> entries,
        bool isProtected,
        DateTimeOffset now,
        TimeSpan partialStaleAfter
    )
    {
        List<FolderEntry> ladders = entries
            .Where(predicate: entry => entry.IsDirectory && LadderDirectoryRegex().IsMatch(input: entry.Name))
            .ToList();

        List<FolderEntry> masters = entries
            .Where(predicate: entry =>
                !entry.IsDirectory
                && entry.Name.EndsWith(value: ".NoMercy.m3u8", comparisonType: StringComparison.OrdinalIgnoreCase)
            )
            .ToList();

        if (ladders.Count == 0 && masters.Count == 0)
            return new(Kind: ReclaimKind.None, TargetNames: [], ReclaimableBytes: 0);

        if (isProtected)
            return new(Kind: ReclaimKind.None, TargetNames: [], ReclaimableBytes: 0);

        bool hasOriginal = entries.Any(predicate: entry =>
            !entry.IsDirectory
            && OriginalExtensions.Contains(
                value: Path.GetExtension(path: entry.Name),
                comparer: StringComparer.OrdinalIgnoreCase
            )
            && !entry.Name.EndsWith(value: ".m3u8", comparisonType: StringComparison.OrdinalIgnoreCase)
        );

        if (masters.Count > 0 && hasOriginal)
        {
            List<FolderEntry> targets = [.. ladders, .. masters];
            return new(
                Kind: ReclaimKind.ReclaimableHls,
                TargetNames: targets.Select(selector: entry => entry.Name).ToList(),
                ReclaimableBytes: targets.Sum(selector: entry => entry.Size)
            );
        }

        if (masters.Count == 0 && ladders.Count > 0)
        {
            DateTimeOffset staleThreshold = now - partialStaleAfter;
            bool allStale = ladders.All(predicate: entry => entry.LastModified < staleThreshold);

            if (!allStale)
                return new(Kind: ReclaimKind.None, TargetNames: [], ReclaimableBytes: 0);

            return new(
                Kind: ReclaimKind.OrphanPartial,
                TargetNames: ladders.Select(selector: entry => entry.Name).ToList(),
                ReclaimableBytes: ladders.Sum(selector: entry => entry.Size)
            );
        }

        return new(Kind: ReclaimKind.None, TargetNames: [], ReclaimableBytes: 0);
    }

    [GeneratedRegex(pattern: @"^(video_\d+x\d+(_.+)?|audio_[A-Za-z0-9_]+)$")]
    private static partial Regex LadderDirectoryRegex();
}
