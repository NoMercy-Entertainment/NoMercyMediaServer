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

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using Image = NoMercy.Database.Models.Media.Image;

namespace NoMercy.MediaProcessing.Images.Palettes.Sources;

public class ImagePaletteSource : IPaletteSource
{
    public string EntityType => "image";

    public async Task<string?> CurrentPaletteAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(s: entityId);
        return await db
            .Images.Where(predicate: i => i.Id == id)
            .Select(selector: i => i._colorPalette)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<PaletteResult> GenerateAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(s: entityId);
        Image? image = await db.Images.FirstOrDefaultAsync(predicate: i => i.Id == id, cancellationToken: ct);
        if (image is null)
            return PaletteResult.NoImage();

        // Guard: only TMDB-hosted images, no SVGs, and matching language
        if (image.Site != "https://image.tmdb.org/t/p/")
            return PaletteResult.NoImage();
        if (image.FilePath.EndsWith(value: ".svg"))
            return PaletteResult.NoImage();

        string lang = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
        bool languageOk =
            image.Iso6391 is null
            || image.Iso6391 == "en"
            || image.Iso6391 == ""
            || image.Iso6391 == lang;
        if (!languageOk)
            return PaletteResult.NoImage();

        string json = await MovieDbImageManager.ColorPalette(type: "image", path: image.FilePath);
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
            .Images.Where(predicate: i => i.Id == id)
            .ExecuteUpdateAsync(setPropertyCalls: s => s.SetProperty(propertyExpression: i => i._colorPalette, valueExpression: json), cancellationToken: ct);
    }
}
