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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NoMercy.MediaProcessing.Images.Palettes.Sources;

public class ReleaseGroupPaletteSource : IPaletteSource
{
    public string EntityType => "releasegroup";

    public async Task<string?> CurrentPaletteAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        Guid id = Guid.Parse(input: entityId);
        return await db
            .ReleaseGroups.Where(predicate: r => r.Id == id)
            .Select(selector: r => r._colorPalette)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<PaletteResult> GenerateAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        Guid id = Guid.Parse(input: entityId);
        ReleaseGroup? releaseGroup = await db.ReleaseGroups.FirstOrDefaultAsync(
            predicate: r => r.Id == id,
            cancellationToken: ct
        );
        if (releaseGroup is null)
            return PaletteResult.NoImage();
        if (releaseGroup.Cover is null)
            return PaletteResult.NoImage();

        string filePath = AppFiles.MusicImagesPath + releaseGroup.Cover;
        if (!File.Exists(path: filePath))
            return PaletteResult.NoImage();

        using Image<Rgba32> image = await Image.LoadAsync<Rgba32>(path: filePath, cancellationToken: ct);
        string json = BaseImageManager.GenerateColorPalette(items:
        [
            new() { Key = "cover", ImageData = image },
        ]);
        return PaletteResult.Success(json: json);
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
            .ReleaseGroups.Where(predicate: r => r.Id == id)
            .ExecuteUpdateAsync(setPropertyCalls: s => s.SetProperty(propertyExpression: r => r._colorPalette, valueExpression: json), cancellationToken: ct);
    }
}
