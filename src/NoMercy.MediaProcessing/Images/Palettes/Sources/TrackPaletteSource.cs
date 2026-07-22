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

public class TrackPaletteSource : IPaletteSource
{
    public string EntityType => "track";

    public async Task<string?> CurrentPaletteAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        Guid id = Guid.Parse(input: entityId);
        return await db
            .Tracks.Where(predicate: t => t.Id == id)
            .Select(selector: t => t._colorPalette)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<PaletteResult> GenerateAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        Guid id = Guid.Parse(input: entityId);
        Track? track = await db.Tracks.FirstOrDefaultAsync(predicate: t => t.Id == id, cancellationToken: ct);
        if (track is null)
            return PaletteResult.NoImage();

        AlbumTrack? link = await db
            .AlbumTrack.Include(navigationPropertyPath: at => at.Album)
            .Where(predicate: at => at.TrackId == id)
            .FirstOrDefaultAsync(cancellationToken: ct);
        Album? album = link?.Album;

        // A track almost always shares its album's stored cover file — both are
        // written from the same release artwork on import. When the covers match,
        // or the track carries no cover of its own, reuse the album palette rather
        // than re-decoding tens of thousands of identical images.
        bool sharesAlbumArt =
            album is not null && (track.Cover is null || track.Cover == album.Cover);
        if (
            sharesAlbumArt
            && !string.IsNullOrEmpty(value: album!._colorPalette)
            && album._colorPalette != "{}"
        )
            return PaletteResult.Success(json: album._colorPalette);

        if (track.Cover is null)
            return PaletteResult.NoImage();

        string filePath = AppFiles.MusicImagesPath + track.Cover;
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
            .Tracks.Where(predicate: t => t.Id == id)
            .ExecuteUpdateAsync(setPropertyCalls: s => s.SetProperty(propertyExpression: t => t._colorPalette, valueExpression: json), cancellationToken: ct);
    }
}
