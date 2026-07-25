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

public class MoviePaletteSource : IPaletteSource
{
    public string EntityType => "movie";

    public async Task<string?> CurrentPaletteAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(entityId);
        return await db
            .Movies.Where(m => m.Id == id)
            .Select(m => m._colorPalette)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PaletteResult> GenerateAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(entityId);
        Movie? movie = await db.Movies.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (movie is null)
            return PaletteResult.NoImage();
        if (movie.Poster is null && movie.Backdrop is null)
            return PaletteResult.NoImage();

        string json = await MovieDbImageManager.MultiColorPalette([
            new("poster", movie.Poster),
            new("backdrop", movie.Backdrop),
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
            .Movies.Where(m => m.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m._colorPalette, json), ct);
    }
}
