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

public class SimilarPaletteSource : IPaletteSource
{
    public string EntityType => "similar";

    public async Task<string?> CurrentPaletteAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(entityId);
        return await db
            .Similar.Where(s => s.Id == id)
            .Select(s => s._colorPalette)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PaletteResult> GenerateAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(entityId);
        Similar? similar = await db.Similar.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (similar is null)
            return PaletteResult.NoImage();
        if (similar.Poster is null && similar.Backdrop is null)
            return PaletteResult.NoImage();

        string json = await MovieDbImageManager.MultiColorPalette([
            new("poster", similar.Poster),
            new("backdrop", similar.Backdrop),
        ]);
        return string.IsNullOrWhiteSpace(json)
            ? PaletteResult.NoImage()
            : PaletteResult.Success(json);
    }

    public async Task PersistAsync(
        MediaContext db,
        string entityId,
        string json,
        CancellationToken ct
    )
    {
        int id = int.Parse(entityId);
        await db
            .Similar.Where(s => s.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x._colorPalette, json), ct);
    }
}
