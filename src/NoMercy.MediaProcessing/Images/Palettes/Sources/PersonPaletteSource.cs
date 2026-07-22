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
using NoMercy.Database.Models.People;

namespace NoMercy.MediaProcessing.Images.Palettes.Sources;

public class PersonPaletteSource : IPaletteSource
{
    public string EntityType => "person";

    public async Task<string?> CurrentPaletteAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(s: entityId);
        return await db
            .People.Where(predicate: p => p.Id == id)
            .Select(selector: p => p._colorPalette)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<PaletteResult> GenerateAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(s: entityId);
        Person? person = await db.People.FirstOrDefaultAsync(predicate: p => p.Id == id, cancellationToken: ct);
        if (person is null)
            return PaletteResult.NoImage();
        if (person.Profile is null)
            return PaletteResult.NoImage();

        string json = await MovieDbImageManager.ColorPalette(type: "profile", path: person.Profile);
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
            .People.Where(predicate: p => p.Id == id)
            .ExecuteUpdateAsync(setPropertyCalls: s => s.SetProperty(propertyExpression: p => p._colorPalette, valueExpression: json), cancellationToken: ct);
    }
}
