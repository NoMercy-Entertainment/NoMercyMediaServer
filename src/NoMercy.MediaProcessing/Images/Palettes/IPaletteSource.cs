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

namespace NoMercy.MediaProcessing.Images.Palettes;

public interface IPaletteSource
{
    string EntityType { get; }

    Task<string?> CurrentPaletteAsync(MediaContext db, string entityId, CancellationToken ct);

    Task<PaletteResult> GenerateAsync(MediaContext db, string entityId, CancellationToken ct);

    Task PersistAsync(MediaContext db, string entityId, string json, CancellationToken ct);
}
