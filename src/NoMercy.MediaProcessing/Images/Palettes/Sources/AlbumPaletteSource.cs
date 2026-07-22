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
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Information;

namespace NoMercy.MediaProcessing.Images.Palettes.Sources;

public class AlbumPaletteSource : IPaletteSource
{
    public string EntityType => "album";

    public async Task<string?> CurrentPaletteAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        Guid id = Guid.Parse(input: entityId);
        return await db
            .Albums.Where(predicate: a => a.Id == id)
            .Select(selector: a => a._colorPalette)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<PaletteResult> GenerateAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        Guid id = Guid.Parse(input: entityId);
        Album? album = await db.Albums.FirstOrDefaultAsync(predicate: a => a.Id == id, cancellationToken: ct);
        if (album is null)
            return PaletteResult.NoImage();
        if (album.Cover is null)
            return PaletteResult.NoImage();

        string filePath = AppFiles.MusicImagesPath + album.Cover;
        if (!File.Exists(path: filePath))
            return PaletteResult.NoImage();

        string json = await CoverArtImageManagerManager.ColorPalette(type: "cover", url: new(uriString: filePath));
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
        Guid id = Guid.Parse(input: entityId);
        await db
            .Albums.Where(predicate: a => a.Id == id)
            .ExecuteUpdateAsync(setPropertyCalls: s => s.SetProperty(propertyExpression: a => a._colorPalette, valueExpression: json), cancellationToken: ct);
    }
}
