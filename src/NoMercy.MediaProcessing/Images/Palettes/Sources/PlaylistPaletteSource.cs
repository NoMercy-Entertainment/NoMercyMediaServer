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

public class PlaylistPaletteSource : IPaletteSource
{
    public string EntityType => "playlist";

    public async Task<string?> CurrentPaletteAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        Guid id = Guid.Parse(input: entityId);
        return await db
            .Playlists.Where(predicate: p => p.Id == id)
            .Select(selector: p => p._colorPalette)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<PaletteResult> GenerateAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        Guid id = Guid.Parse(input: entityId);
        Playlist? playlist = await db.Playlists.FirstOrDefaultAsync(predicate: p => p.Id == id, cancellationToken: ct);
        if (playlist is null)
            return PaletteResult.NoImage();
        if (playlist.Cover is null)
            return PaletteResult.NoImage();

        string filePath = AppFiles.MusicImagesPath + playlist.Cover;
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
            .Playlists.Where(predicate: p => p.Id == id)
            .ExecuteUpdateAsync(setPropertyCalls: s => s.SetProperty(propertyExpression: p => p._colorPalette, valueExpression: json), cancellationToken: ct);
    }
}
