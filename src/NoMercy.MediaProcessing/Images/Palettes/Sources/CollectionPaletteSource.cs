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
using NoMercy.Database.Models.Movies;

namespace NoMercy.MediaProcessing.Images.Palettes.Sources;

public class CollectionPaletteSource : IPaletteSource
{
    public string EntityType => "collection";

    public async Task<string?> CurrentPaletteAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(s: entityId);
        return await db
            .Collections.Where(predicate: c => c.Id == id)
            .Select(selector: c => c._colorPalette)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<PaletteResult> GenerateAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(s: entityId);
        Collection? collection = await db.Collections.FirstOrDefaultAsync(predicate: c => c.Id == id, cancellationToken: ct);
        if (collection is null)
            return PaletteResult.NoImage();
        if (collection.Poster is null && collection.Backdrop is null)
            return PaletteResult.NoImage();

        string json = await MovieDbImageManager.MultiColorPalette(items:
        [
            new(key: "poster", path: collection.Poster),
            new(key: "backdrop", path: collection.Backdrop),
        ]);
        return string.IsNullOrWhiteSpace(value: json)
            ? PaletteResult.NoImage()
            : PaletteResult.Success(json: json);
    }

    public async Task PersistAsync(
        MediaContext db,
        string entityId,
        string json,
        CancellationToken ct
    )
    {
        int id = int.Parse(s: entityId);
        await db
            .Collections.Where(predicate: c => c.Id == id)
            .ExecuteUpdateAsync(setPropertyCalls: s => s.SetProperty(propertyExpression: c => c._colorPalette, valueExpression: json), cancellationToken: ct);
    }
}
