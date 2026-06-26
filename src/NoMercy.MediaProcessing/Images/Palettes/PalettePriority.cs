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

namespace NoMercy.MediaProcessing.Images.Palettes;

public static class PalettePriority
{
    public const int OnDemand = 1;
    public const int BackfillCoordinator = 20;

    private static readonly HashSet<string> MainTypes = new()
    {
        "movie",
        "tv",
        "season",
        "episode",
        "person",
        "collection",
        "artist",
        "album",
    };

    public static bool IsMain(string entityType) => MainTypes.Contains(entityType);

    public static int ForImport(string entityType) => IsMain(entityType) ? 4 : 8;

    public static int ForBackfill(string entityType) => IsMain(entityType) ? 14 : 18;
}
