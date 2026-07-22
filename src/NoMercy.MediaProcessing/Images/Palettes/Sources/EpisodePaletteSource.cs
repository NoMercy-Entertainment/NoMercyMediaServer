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
using NoMercy.Database.Models.TvShows;

namespace NoMercy.MediaProcessing.Images.Palettes.Sources;

public class EpisodePaletteSource : IPaletteSource
{
    public string EntityType => "episode";

    public async Task<string?> CurrentPaletteAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(s: entityId);
        return await db
            .Episodes.Where(predicate: e => e.Id == id)
            .Select(selector: e => e._colorPalette)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<PaletteResult> GenerateAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(s: entityId);
        Episode? episode = await db.Episodes.FirstOrDefaultAsync(predicate: e => e.Id == id, cancellationToken: ct);
        if (episode is null)
            return PaletteResult.NoImage();
        if (episode.Still is null)
            return PaletteResult.NoImage();

        string json = await MovieDbImageManager.ColorPalette(type: "still", path: episode.Still);
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
            .Episodes.Where(predicate: e => e.Id == id)
            .ExecuteUpdateAsync(setPropertyCalls: s => s.SetProperty(propertyExpression: e => e._colorPalette, valueExpression: json), cancellationToken: ct);
    }
}
