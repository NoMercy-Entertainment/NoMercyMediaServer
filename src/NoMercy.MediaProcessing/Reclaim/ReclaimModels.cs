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

namespace NoMercy.MediaProcessing.Reclaim;

public sealed record ReclaimableItem(
    string Id,
    string Title,
    string MediaType,
    string Folder,
    string ServedCopy,
    ReclaimKind Kind,
    IReadOnlyList<string> TargetPaths,
    long ReclaimableBytes
);

public sealed record PartialJunkItem(string Folder, IReadOnlyList<string> TargetPaths, long Bytes);

public enum ReclaimScanState
{
    Idle,
    Scanning,
    Completed,
    Failed,
}

public sealed record ReclaimScanResult(
    IReadOnlyList<ReclaimableItem> Items,
    IReadOnlyList<PartialJunkItem> PartialJunk,
    long TotalReclaimableBytes,
    long TotalPartialJunkBytes
);
